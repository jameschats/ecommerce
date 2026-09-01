using ecomm.api.Features.Growth;

namespace ecomm.api.Features.MarketingStudio;

/// <summary>
/// The Marketing Studio's port for AI copy. Wraps the existing (credit-metered) Growth generator so
/// the studio doesn't own a second LLM integration, and stays decoupled behind an interface for
/// testing + eventual extraction. <paramref name="kind"/> is a Growth content-type key
/// (instagram-caption | facebook-post | whatsapp | google-ads | …).
/// </summary>
public interface IMarketingCopywriter
{
    Task<string> WriteAsync(string kind, long? productId, string brief, long? userId, CancellationToken ct = default);
}

public sealed class GrowthCopywriter(IGrowthGenerationService gen) : IMarketingCopywriter
{
    public async Task<string> WriteAsync(string kind, long? productId, string brief, long? userId, CancellationToken ct = default)
    {
        var dto = await gen.GenerateAsync(new GenerateRequest(kind, productId, null, brief), userId, campaignId: null, ct);
        return dto.Body;
    }
}
