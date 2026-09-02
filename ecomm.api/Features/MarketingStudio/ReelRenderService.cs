using System.Diagnostics;
using System.Text.Json;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Media;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ecomm.api.Features.MarketingStudio;

public sealed record RenderReelRequest(long? ProductId, string Goal, string Platform, string? LanguageCode, bool IncludeMusic);
public sealed record RenderStatusDto(long Id, string Status, string? OutputMediaUrl, string? Error);

public interface IReelRenderService
{
    Task<long> EnqueueAsync(RenderReelRequest req, long? userId, CancellationToken ct = default);
    Task<RenderStatusDto?> StatusAsync(long jobId, CancellationToken ct = default);
    /// <summary>Hangfire target — renders one queued job. Runs FFmpeg in-process (ffmpeg is on the image).</summary>
    Task RenderAsync(long jobId);
}

/// <summary>Seam over the background-job queue so enqueuing is testable (real impl = Hangfire).</summary>
public interface IReelRenderQueue { void Enqueue(long jobId); }

public sealed class HangfireReelRenderQueue : IReelRenderQueue
{
    public void Enqueue(long jobId) => Hangfire.BackgroundJob.Enqueue<IReelRenderService>(s => s.RenderAsync(jobId));
}

/// <summary>
/// Reel rendering (MS3·c). Enqueue builds the plan + resolves scene images and stores a queued job;
/// a Hangfire background job then generates the voiceover (Sarvam), picks optional music, downloads the
/// scene images, runs FFmpeg (via <see cref="FfmpegReelCommandBuilder"/>) and stores the MP4. FFmpeg
/// lives on the API image; rendering is occasional + bounded. Music is optional (silent if none).
/// </summary>
public sealed class ReelRenderService(
    EcommerceDbContext db, IVideoPlanService plans, ICatalogReader catalog, IMarketingBrandService brand,
    ITextToSpeech tts, IMusicProvider music, IMediaStorage media, IHttpClientFactory httpFactory,
    ICurrentTenantService tenant, IConfiguration config, IReelRenderQueue queue, ILogger<ReelRenderService> log) : IReelRenderService
{
    private sealed record SceneJson(string ImageUrl, int Duration, string Caption);

    public async Task<long> EnqueueAsync(RenderReelRequest req, long? userId, CancellationToken ct = default)
    {
        var plan = await plans.BuildAsync(new VideoPlanRequest(req.ProductId, req.Goal, req.Platform), userId, ct);
        var product = req.ProductId is { } pid ? await catalog.GetAsync(pid, ct) : null;
        var b = await brand.GetAsync(ct);
        var productImg = product?.ImageUrl;
        var logo = b.LogoUrl;
        if (string.IsNullOrWhiteSpace(productImg) && string.IsNullOrWhiteSpace(logo))
            throw new AppException("Add a product photo (or upload a logo in the brand kit) to make a reel.", StatusCodes.Status400BadRequest);

        var scenes = plan.Scenes.Select(s => new SceneJson(
            s.Visual == "brand_logo" ? (logo ?? productImg)! : (productImg ?? logo)!, s.DurationSeconds, s.Text)).ToList();
        var (w, h) = Dim(plan.Aspect);

        var job = new MarketingVideoRender
        {
            Status = "queued", Aspect = plan.Aspect, Width = w, Height = h,
            ScenesJson = JsonSerializer.Serialize(scenes), Narration = plan.Narration,
            LanguageCode = string.IsNullOrWhiteSpace(req.LanguageCode) ? "en-IN" : req.LanguageCode!,
            MusicMood = plan.MusicVertical, IncludeMusic = req.IncludeMusic, CreatedAt = DateTime.UtcNow,
        };
        db.MarketingVideoRenders.Add(job);
        await db.SaveChangesAsync(ct);

        queue.Enqueue(job.MarketingVideoRenderId);
        return job.MarketingVideoRenderId;
    }

    public async Task<RenderStatusDto?> StatusAsync(long jobId, CancellationToken ct = default)
    {
        var j = await db.MarketingVideoRenders.AsNoTracking().FirstOrDefaultAsync(x => x.MarketingVideoRenderId == jobId, ct);
        return j is null ? null : new RenderStatusDto(j.MarketingVideoRenderId, j.Status, j.OutputMediaUrl, j.Error);
    }

    public async Task RenderAsync(long jobId)
    {
        // Runs in a Hangfire worker with no tenant scope — load past the filter, then scope to the job's tenant.
        var job = await db.MarketingVideoRenders.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.MarketingVideoRenderId == jobId);
        if (job is null) return;

        var tempDir = Path.Combine(Path.GetTempPath(), $"reel-{jobId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            job.Status = "rendering"; job.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            using var scope = tenant.BeginScope(job.TenantId);
            var scenes = JsonSerializer.Deserialize<List<SceneJson>>(job.ScenesJson) ?? [];
            if (scenes.Count == 0) throw new InvalidOperationException("No scenes to render.");

            var renderScenes = new List<RenderScene>();
            for (var i = 0; i < scenes.Count; i++)
            {
                var path = await DownloadAsync(scenes[i].ImageUrl, tempDir, $"scene{i}");
                renderScenes.Add(new RenderScene(path, Math.Clamp(scenes[i].Duration, 2, 10), scenes[i].Caption));
            }

            string? voicePath = null;
            if (!string.IsNullOrWhiteSpace(job.Narration) && tts.Enabled)
            {
                var audio = await tts.SynthesizeAsync(job.Narration!, job.LanguageCode, null);
                if (audio is not null) { voicePath = Path.Combine(tempDir, $"vo.{audio.Extension}"); await File.WriteAllBytesAsync(voicePath, audio.Bytes); }
            }

            var musicPath = job.IncludeMusic ? music.Pick(job.MusicMood) : null;
            var outPath = Path.Combine(tempDir, "reel.mp4");
            var cmd = FfmpegReelCommandBuilder.Build(new RenderSpec(job.Width, job.Height, renderScenes, voicePath, musicPath, outPath));

            await RunFfmpegAsync(cmd);
            if (!File.Exists(outPath) || new FileInfo(outPath).Length == 0) throw new InvalidOperationException("FFmpeg produced no output.");

            await using var mp4 = File.OpenRead(outPath);
            var stored = await media.SaveAsync(mp4, "reel.mp4", "video/mp4");
            job.OutputMediaUrl = stored.Url; job.Status = "done"; job.Error = null;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Reel render {Job} failed.", jobId);
            job.Status = "failed"; job.Error = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
        }
        finally
        {
            job.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private async Task<string> DownloadAsync(string url, string dir, string name)
    {
        var ext = url.Contains(".png", StringComparison.OrdinalIgnoreCase) ? "png"
            : url.Contains(".webp", StringComparison.OrdinalIgnoreCase) ? "webp" : "jpg";
        var path = Path.Combine(dir, $"{name}.{ext}");

        var local = await media.OpenReadAsync(url);   // our own media → direct disk read
        if (local is not null) { await using var f = File.Create(path); await local.CopyToAsync(f); return path; }

        var http = httpFactory.CreateClient();
        var bytes = await http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    private async Task RunFfmpegAsync(FfmpegCommand cmd)
    {
        var ffmpeg = config["Marketing:FfmpegPath"] ?? "ffmpeg";
        var psi = new ProcessStartInfo(ffmpeg) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var a in cmd.Args) psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg exited {proc.ExitCode}: {(stderr.Length > 500 ? stderr[^500..] : stderr)}");
    }

    private static (int, int) Dim(string aspect) => aspect switch
    {
        "1:1" => (1080, 1080),
        "16:9" => (1920, 1080),
        "2:3" => (1080, 1620),
        _ => (1080, 1920),   // 9:16
    };
}
