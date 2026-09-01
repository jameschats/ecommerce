using ecomm.api.Features.MarketingStudio;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ecomm.tests;

public class FfmpegReelCommandBuilderTests
{
    private static RenderSpec Spec(string? voice, string? music) => new(
        1080, 1920,
        new[] { new RenderScene("/tmp/1.jpg", 4, "You'll love this"), new RenderScene("/tmp/2.jpg", 6, "₹2499") },
        voice, music, "/tmp/out.mp4");

    private static string Args(FfmpegCommand c) => string.Join(" ", c.Args);

    [Fact]
    public void Builds_per_scene_kenburns_captions_and_concat()
    {
        var cmd = FfmpegReelCommandBuilder.Build(Spec(voice: null, music: null));
        var a = Args(cmd);
        Assert.Contains("-filter_complex", a);
        Assert.Contains("zoompan=", a);                       // Ken Burns
        Assert.Contains("drawtext=", a);                      // burned captions
        Assert.Contains("concat=n=2", a);                     // two scenes joined
        Assert.Contains("libx264", a);
        Assert.EndsWith("/tmp/out.mp4", a);
        Assert.Equal(2, cmd.Args.Count(x => x == "-loop"));   // one looped image per scene
    }

    [Fact]
    public void Mixes_voiceover_and_ducked_music_when_both_present()
    {
        var a = Args(FfmpegReelCommandBuilder.Build(Spec("/tmp/vo.mp3", "/tmp/bg.mp3")));
        Assert.Contains("amix=inputs=2", a);                  // voice + music mixed
        Assert.Contains("volume=0.18", a);                    // music ducked under the voice
        Assert.Contains("-c:a aac", a);
        Assert.Contains("-shortest", a);
    }

    [Fact]
    public void Uses_voiceover_only_when_no_music()
    {
        var a = Args(FfmpegReelCommandBuilder.Build(Spec("/tmp/vo.mp3", null)));
        Assert.DoesNotContain("amix", a);
        Assert.Contains("-c:a aac", a);                       // still has an audio track
    }

    [Fact]
    public void Has_no_audio_mapping_when_silent()
    {
        var a = Args(FfmpegReelCommandBuilder.Build(Spec(null, null)));
        Assert.DoesNotContain("-c:a", a);
        Assert.DoesNotContain("amix", a);
    }

    [Fact]
    public void Escapes_caption_special_characters_for_drawtext()
    {
        var spec = new RenderSpec(1080, 1080, new[] { new RenderScene("/x.jpg", 3, "50% off: today") }, null, null, "/o.mp4");
        var a = Args(FfmpegReelCommandBuilder.Build(spec));
        Assert.Contains("50\\% off\\: today", a);             // % and : escaped
    }

    [Fact]
    public void Local_music_provider_returns_null_when_no_directory_configured()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        Assert.Null(new LocalMusicProvider(cfg).Pick("festive"));
    }
}
