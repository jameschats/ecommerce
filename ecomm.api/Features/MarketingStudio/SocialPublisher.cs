using ecomm.api.Data.Entities;

namespace ecomm.api.Features.MarketingStudio;

public sealed record PublishResult(bool Success, string? ExternalPostId, string? Error)
{
    public static PublishResult Ok(string externalPostId) => new(true, externalPostId, null);
    public static PublishResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// Publishes one creative to one social platform on a merchant's behalf. Real per-platform
/// implementations (Meta Graph, LinkedIn, Pinterest, YouTube) are wired when their app keys + the
/// posting calls land; until then <see cref="LoggingSocialPublisher"/> is the default — the same
/// dev-provider convention Email/SMS/WhatsApp use in this codebase.
/// </summary>
public interface ISocialPublisher
{
    Task<PublishResult> PublishAsync(SocialConnection connection, MarketingCreative creative, string? caption, CancellationToken ct = default);
}

/// <summary>Dev/default publisher — logs instead of calling a live API and reports success with a
/// synthetic id, so the schedule→publish loop is exercisable before real connectors exist. Selected
/// whenever no live publisher is registered (mirrors LoggingWhatsAppProvider / ConsoleSmsSender).</summary>
public sealed class LoggingSocialPublisher(ILogger<LoggingSocialPublisher> log) : ISocialPublisher
{
    public Task<PublishResult> PublishAsync(SocialConnection connection, MarketingCreative creative, string? caption, CancellationToken ct = default)
    {
        log.LogWarning("[DEV SOCIAL PUBLISH] {Platform} ← creative {CreativeId} ({Type}). Caption: {Caption}",
            connection.Platform, creative.MarketingCreativeId, creative.Type,
            (caption ?? creative.Body ?? "").Length > 80 ? (caption ?? creative.Body)![..80] + "…" : caption ?? creative.Body);
        return Task.FromResult(PublishResult.Ok($"log:{Guid.NewGuid():N}"));
    }
}
