using Microsoft.Extensions.Configuration;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>One scene to render: a still image, how long it's on screen, and its burned-in caption.</summary>
public sealed record RenderScene(string ImagePath, int DurationSeconds, string Caption);

/// <summary>Everything the FFmpeg step needs to assemble one reel.</summary>
public sealed record RenderSpec(
    int Width, int Height, IReadOnlyList<RenderScene> Scenes,
    string? VoiceoverPath, string? MusicPath, string OutputPath,
    string PrimaryColor = "#111827", string AccentColor = "#2563eb");

/// <summary>An FFmpeg invocation: the argument list to hand to the ffmpeg process.</summary>
public sealed record FfmpegCommand(IReadOnlyList<string> Args);

/// <summary>Supplies a background music track for a reel, by mood. Music is OPTIONAL — a null result
/// means "render without music". Today: a local seed pack of cleared/CC0 files (LocalMusicProvider);
/// later, an AI-music or stock API behind the same interface (Mubert/Beatoven/Soundstripe).</summary>
public interface IMusicProvider
{
    /// <summary>A local file path to a track for <paramref name="mood"/>, or null for no music.</summary>
    string? Pick(string mood);
}

/// <summary>
/// Music from a local seed pack — files under <c>Marketing:MusicDir</c> named <c>{mood}.mp3</c> (falls
/// back to any track, then to none). Keeps music optional and zero-licensing-risk until a paid music
/// API is wired behind <see cref="IMusicProvider"/>.
/// </summary>
public sealed class LocalMusicProvider(IConfiguration config) : IMusicProvider
{
    public string? Pick(string mood)
    {
        var dir = config["Marketing:MusicDir"];
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return null;
        var byMood = Path.Combine(dir, $"{Sanitize(mood)}.mp3");
        if (File.Exists(byMood)) return byMood;
        return Directory.EnumerateFiles(dir, "*.mp3").FirstOrDefault();   // any track, or null
    }

    private static string Sanitize(string s) => new(s.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
}

/// <summary>
/// Builds the deterministic FFmpeg command that assembles a reel: each scene image gets a slow
/// Ken-Burns zoom, is scaled/padded to the target frame and has its caption burned in; scenes are
/// concatenated; voiceover and (optional, ducked) music are mixed. Pure — it only produces the argument
/// list, so it's fully unit-testable without running FFmpeg. The render worker executes it (next slice).
/// </summary>
public static class FfmpegReelCommandBuilder
{
    public static FfmpegCommand Build(RenderSpec spec)
    {
        var args = new List<string> { "-y" };
        var (w, h) = (spec.Width, spec.Height);

        // One looped image input per scene.
        foreach (var scene in spec.Scenes)
        {
            args.Add("-loop"); args.Add("1");
            args.Add("-t"); args.Add(scene.DurationSeconds.ToString());
            args.Add("-i"); args.Add(scene.ImagePath);
        }

        var audioInputStart = spec.Scenes.Count;
        var hasVoice = !string.IsNullOrWhiteSpace(spec.VoiceoverPath);
        var hasMusic = !string.IsNullOrWhiteSpace(spec.MusicPath);
        if (hasVoice) { args.Add("-i"); args.Add(spec.VoiceoverPath!); }
        if (hasMusic) { args.Add("-i"); args.Add(spec.MusicPath!); }

        // Per-scene: Ken Burns (zoompan) → scale/pad to frame → burn caption.
        var filter = new System.Text.StringBuilder();
        var fps = 30;
        for (var i = 0; i < spec.Scenes.Count; i++)
        {
            var s = spec.Scenes[i];
            var frames = Math.Max(1, s.DurationSeconds * fps);
            filter.Append($"[{i}:v]");
            filter.Append($"scale={w * 2}:{h * 2},");
            filter.Append($"zoompan=z='min(zoom+0.0008,1.2)':d={frames}:s={w}x{h}:fps={fps},");
            filter.Append($"setsar=1,");
            filter.Append($"drawtext=text='{EscapeDrawText(s.Caption)}':fontcolor=white:fontsize={h / 18}:x=(w-text_w)/2:y=h-(h/6):box=1:boxcolor=black@0.45:boxborderw=18");
            filter.Append($"[v{i}];");
        }
        for (var i = 0; i < spec.Scenes.Count; i++) filter.Append($"[v{i}]");
        filter.Append($"concat=n={spec.Scenes.Count}:v=1:a=0[vout];");

        // Audio: voiceover full, music ducked under it; either alone; or silent.
        string? audioLabel = null;
        if (hasVoice && hasMusic)
        {
            filter.Append($"[{audioInputStart + 1}:a]volume=0.18[bg];");
            filter.Append($"[{audioInputStart}:a][bg]amix=inputs=2:duration=first:dropout_transition=0[aout]");
            audioLabel = "[aout]";
        }
        else if (hasVoice) { audioLabel = $"{audioInputStart}:a"; }
        else if (hasMusic) { filter.Append($"[{audioInputStart}:a]volume=0.6[aout]"); audioLabel = "[aout]"; }

        args.Add("-filter_complex"); args.Add(filter.ToString().TrimEnd(';'));
        args.Add("-map"); args.Add("[vout]");
        if (audioLabel is not null) { args.Add("-map"); args.Add(audioLabel); args.Add("-shortest"); }

        args.Add("-c:v"); args.Add("libx264");
        args.Add("-pix_fmt"); args.Add("yuv420p");
        args.Add("-r"); args.Add(fps.ToString());
        if (audioLabel is not null) { args.Add("-c:a"); args.Add("aac"); }
        args.Add(spec.OutputPath);

        return new FfmpegCommand(args);
    }

    /// <summary>Escape a caption for FFmpeg drawtext (special chars: \ : ' %).</summary>
    private static string EscapeDrawText(string text) =>
        (text ?? "").Replace("\\", "\\\\").Replace(":", "\\:").Replace("'", "’").Replace("%", "\\%");
}
