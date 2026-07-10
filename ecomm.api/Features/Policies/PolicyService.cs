using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Ganss.Xss;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Policies;

public sealed record PolicyDto(string Handle, string Title, string? BodyHtml, bool HasContent);
public sealed record PolicyLinkDto(string Handle, string Title);
public sealed record SavePolicyRequest(string? Title, string? BodyHtml);

public interface IPolicyService
{
    Task<IReadOnlyList<PolicyDto>> ListAsync(CancellationToken ct = default);
    Task<PolicyDto> GetAsync(string handle, CancellationToken ct = default);
    Task<PolicyDto> SaveAsync(string handle, SavePolicyRequest req, CancellationToken ct = default);
    Task<PolicyDto?> GetPublicAsync(string handle, CancellationToken ct = default);
    Task<IReadOnlyList<PolicyLinkDto>> PublicLinksAsync(CancellationToken ct = default);
}

/// <summary>
/// Store legal pages (a fixed set of handles). Body HTML is sanitized on save (strip scripts/handlers).
/// Public reads only return policies that actually have content; those drive the storefront footer links.
/// </summary>
public sealed class PolicyService(EcommerceDbContext db) : IPolicyService
{
    private readonly HtmlSanitizer _sanitizer = new();
    private static readonly (string Handle, string Title)[] Known =
    {
        ("refund", "Refund policy"), ("privacy", "Privacy policy"), ("terms", "Terms of service"),
        ("shipping", "Shipping policy"), ("contact", "Contact information"), ("legal", "Legal notice"),
    };

    public async Task<IReadOnlyList<PolicyDto>> ListAsync(CancellationToken ct = default)
    {
        var existing = await db.StorePolicies.ToDictionaryAsync(p => p.Handle, ct);
        return Known.Select(k =>
        {
            var p = existing.GetValueOrDefault(k.Handle);
            return new PolicyDto(k.Handle, p?.Title ?? k.Title, p?.BodyHtml, !string.IsNullOrWhiteSpace(p?.BodyHtml));
        }).ToList();
    }

    public async Task<PolicyDto> GetAsync(string handle, CancellationToken ct = default)
    {
        var known = Require(handle);
        var p = await db.StorePolicies.FirstOrDefaultAsync(x => x.Handle == known.Handle, ct);
        return new PolicyDto(known.Handle, p?.Title ?? known.Title, p?.BodyHtml, !string.IsNullOrWhiteSpace(p?.BodyHtml));
    }

    public async Task<PolicyDto> SaveAsync(string handle, SavePolicyRequest req, CancellationToken ct = default)
    {
        var known = Require(handle);
        var p = await db.StorePolicies.FirstOrDefaultAsync(x => x.Handle == known.Handle, ct);
        if (p is null)
        {
            p = new StorePolicy { Handle = known.Handle, Title = known.Title, CreatedAt = DateTime.UtcNow };
            db.StorePolicies.Add(p);
        }
        p.Title = string.IsNullOrWhiteSpace(req.Title) ? known.Title : req.Title.Trim();
        p.BodyHtml = string.IsNullOrWhiteSpace(req.BodyHtml) ? null : _sanitizer.Sanitize(req.BodyHtml);
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new PolicyDto(p.Handle, p.Title, p.BodyHtml, !string.IsNullOrWhiteSpace(p.BodyHtml));
    }

    public async Task<PolicyDto?> GetPublicAsync(string handle, CancellationToken ct = default)
    {
        var known = Known.FirstOrDefault(k => k.Handle == handle?.Trim().ToLowerInvariant());
        if (known.Handle is null) return null;
        var p = await db.StorePolicies.FirstOrDefaultAsync(x => x.Handle == known.Handle, ct);
        return string.IsNullOrWhiteSpace(p?.BodyHtml) ? null : new PolicyDto(p.Handle, p.Title, p.BodyHtml, true);
    }

    public async Task<IReadOnlyList<PolicyLinkDto>> PublicLinksAsync(CancellationToken ct = default)
    {
        var withContent = await db.StorePolicies.Where(p => p.BodyHtml != null && p.BodyHtml != "")
            .Select(p => new { p.Handle, p.Title }).ToListAsync(ct);
        // Preserve the canonical order.
        return Known.Where(k => withContent.Any(w => w.Handle == k.Handle))
            .Select(k => new PolicyLinkDto(k.Handle, withContent.First(w => w.Handle == k.Handle).Title)).ToList();
    }

    private static (string Handle, string Title) Require(string handle)
    {
        var known = Known.FirstOrDefault(k => k.Handle == handle?.Trim().ToLowerInvariant());
        return known.Handle is null ? throw new AppException("Unknown policy.", StatusCodes.Status404NotFound) : known;
    }
}
