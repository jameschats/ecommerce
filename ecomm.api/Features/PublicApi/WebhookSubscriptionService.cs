using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.PublicApi;

public sealed record WebhookSubscriptionDto(long Id, string Url, IReadOnlyList<string> Events, bool IsActive, DateTime CreatedAt);
public sealed record CreateWebhookSubscriptionRequest(string Url, IReadOnlyList<string> Events);
/// <summary>The signing secret is only ever present in THIS response — like an API key's raw value,
/// it's never recoverable again (it stays encrypted at rest, never re-decrypted for display).</summary>
public sealed record CreatedWebhookSubscriptionDto(long Id, string Url, IReadOnlyList<string> Events, string Secret, DateTime CreatedAt);

public interface IWebhookSubscriptionService
{
    Task<IReadOnlyList<WebhookSubscriptionDto>> ListAsync(CancellationToken ct = default);
    Task<CreatedWebhookSubscriptionDto> CreateAsync(CreateWebhookSubscriptionRequest req, long? userId, CancellationToken ct = default);
    Task SetActiveAsync(long id, bool isActive, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>Reversible — only <see cref="WebhookDispatchService"/> calls this, to sign an
    /// outgoing payload. Never exposed through any DTO/controller response.</summary>
    Task<string?> GetSecretAsync(long subscriptionId, CancellationToken ct = default);
}

/// <summary>
/// Merchant-managed webhook subscriptions (v4 Phase 6 Track A) — "notify my app whenever an order
/// is created." The signing secret is DataProtection-encrypted at rest (needs to be reversible to
/// sign each outgoing delivery), same purpose-string convention as every other encrypted secret in
/// this codebase (<c>PaymentSettingsService</c>, <c>TwoFactorService</c>).
/// </summary>
public sealed class WebhookSubscriptionService(EcommerceDbContext db, IDataProtectionProvider dp) : IWebhookSubscriptionService
{
    public const string ProtectorPurpose = "publicapi.webhook.secret.v1";

    /// <summary>The full set of event types anything can subscribe to. Kept in step with what
    /// <see cref="WebhookDispatchService"/> actually fires — see that class for where each fires from.</summary>
    public static readonly IReadOnlyList<string> ValidEvents = ["order.created", "order.updated", "product.updated", "inventory.updated"];

    private IDataProtector Protector => dp.CreateProtector(ProtectorPurpose);

    public async Task<IReadOnlyList<WebhookSubscriptionDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.WebhookSubscriptions.AsNoTracking().OrderByDescending(s => s.WebhookSubscriptionId).ToListAsync(ct);
        return rows.Select(s => new WebhookSubscriptionDto(s.WebhookSubscriptionId, s.Url, SplitEvents(s.Events), s.IsActive, s.CreatedAt)).ToList();
    }

    public async Task<CreatedWebhookSubscriptionDto> CreateAsync(CreateWebhookSubscriptionRequest req, long? userId, CancellationToken ct = default)
    {
        var url = (req.Url ?? "").Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            throw new AppException("A valid webhook URL is required.", StatusCodes.Status400BadRequest);

        var events = (req.Events ?? []).Select(e => e.Trim().ToLowerInvariant()).Distinct().ToList();
        if (events.Count == 0) throw new AppException("Pick at least one event.", StatusCodes.Status400BadRequest);
        var invalid = events.Where(e => !ValidEvents.Contains(e)).ToList();
        if (invalid.Count > 0) throw new AppException($"Unknown event(s): {string.Join(", ", invalid)}.", StatusCodes.Status400BadRequest);

        var secret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var sub = new WebhookSubscription
        {
            Url = url, Events = string.Join(",", events), EncryptedSecret = Protector.Protect(secret),
            IsActive = true, CreatedByUserId = userId, CreatedAt = DateTime.UtcNow,
        };
        db.WebhookSubscriptions.Add(sub);
        await db.SaveChangesAsync(ct);
        return new CreatedWebhookSubscriptionDto(sub.WebhookSubscriptionId, sub.Url, events, secret, sub.CreatedAt);
    }

    public async Task SetActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        var sub = await db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.WebhookSubscriptionId == id, ct)
                   ?? throw new AppException("Webhook subscription not found.", StatusCodes.Status404NotFound);
        sub.IsActive = isActive;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var sub = await db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.WebhookSubscriptionId == id, ct)
                   ?? throw new AppException("Webhook subscription not found.", StatusCodes.Status404NotFound);
        db.WebhookSubscriptions.Remove(sub);
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetSecretAsync(long subscriptionId, CancellationToken ct = default)
    {
        var sub = await db.WebhookSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.WebhookSubscriptionId == subscriptionId, ct);
        return sub is null ? null : Protector.Unprotect(sub.EncryptedSecret);
    }

    private static IReadOnlyList<string> SplitEvents(string events) =>
        events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
