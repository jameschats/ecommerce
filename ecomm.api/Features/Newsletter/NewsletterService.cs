using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Newsletter;

public sealed record NewsletterSubscriberDto(long Id, string Email, string? Source, DateTime CreatedAt);

public interface INewsletterService
{
    Task SubscribeAsync(string email, string? source, CancellationToken ct = default);
    Task<IReadOnlyList<NewsletterSubscriberDto>> ListAsync(int take = 1000, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}

public sealed class NewsletterService(EcommerceDbContext db) : INewsletterService
{
    public async Task SubscribeAsync(string email, string? source, CancellationToken ct = default)
    {
        var addr = (email ?? "").Trim().ToLowerInvariant();
        if (addr.Length < 3 || !addr.Contains('@')) throw new AppException("Enter a valid email address.", StatusCodes.Status400BadRequest);

        if (!await db.NewsletterSubscribers.AnyAsync(s => s.Email == addr, ct))
            db.NewsletterSubscribers.Add(new NewsletterSubscriber { Email = addr, Source = source, CreatedAt = DateTime.UtcNow });

        // If the email already belongs to a customer account, record their email-marketing consent so
        // they show as opted-in in the admin Customers list (reuses the existing consent flag).
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == addr.ToUpperInvariant() && !u.IsDeleted, ct);
        if (user is not null)
        {
            var profile = await db.CustomerProfiles.FirstOrDefaultAsync(p => p.UserId == user.UserId, ct);
            if (profile is null)
            {
                profile = new CustomerProfile { UserId = user.UserId, CreatedAt = DateTime.UtcNow };
                db.CustomerProfiles.Add(profile);
            }
            profile.AcceptsEmailMarketing = true;
            profile.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NewsletterSubscriberDto>> ListAsync(int take = 1000, CancellationToken ct = default) =>
        await db.NewsletterSubscribers.AsNoTracking()
            .OrderByDescending(s => s.NewsletterSubscriberId)
            .Take(Math.Clamp(take, 1, 5000))
            .Select(s => new NewsletterSubscriberDto(s.NewsletterSubscriberId, s.Email, s.Source, s.CreatedAt))
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => db.NewsletterSubscribers.CountAsync(ct);
}
