using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Faqs;

public sealed record FaqDto(long FaqId, string Question, string Answer, string? Category, int DisplayOrder, bool IsPublished);
public sealed record SaveFaqRequest(string Question, string Answer, string? Category, int DisplayOrder, bool IsPublished);

public interface IFaqService
{
    /// <summary>Published entries only — what the storefront and the bot may see.</summary>
    Task<IReadOnlyList<FaqDto>> PublishedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FaqDto>> AllAsync(CancellationToken ct = default);
    Task<FaqDto> CreateAsync(SaveFaqRequest req, CancellationToken ct = default);
    Task<FaqDto> UpdateAsync(long id, SaveFaqRequest req, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Per-tenant FAQs. Previously seven Q&amp;As hardcoded in the Angular component — identical for
/// every store and uneditable, which meant a COD-only shop still told shoppers it accepted cards.
/// </summary>
public sealed class FaqService(EcommerceDbContext db) : IFaqService
{
    public async Task<IReadOnlyList<FaqDto>> PublishedAsync(CancellationToken ct = default) =>
        await Ordered().Where(f => f.IsPublished).Select(Map).ToListAsync(ct);

    public async Task<IReadOnlyList<FaqDto>> AllAsync(CancellationToken ct = default) =>
        await Ordered().Select(Map).ToListAsync(ct);

    public async Task<FaqDto> CreateAsync(SaveFaqRequest req, CancellationToken ct = default)
    {
        var (question, answer) = Validate(req);
        var faq = new Faq
        {
            Question = question, Answer = answer,
            Category = Clean(req.Category, 60), DisplayOrder = req.DisplayOrder,
            IsPublished = req.IsPublished, CreatedAt = DateTime.UtcNow,
        };
        db.Faqs.Add(faq);
        await db.SaveChangesAsync(ct);
        return ToDto(faq);
    }

    public async Task<FaqDto> UpdateAsync(long id, SaveFaqRequest req, CancellationToken ct = default)
    {
        var (question, answer) = Validate(req);
        var faq = await db.Faqs.FirstOrDefaultAsync(f => f.FaqId == id, ct)
                  ?? throw new AppException("FAQ not found.", StatusCodes.Status404NotFound);

        faq.Question = question;
        faq.Answer = answer;
        faq.Category = Clean(req.Category, 60);
        faq.DisplayOrder = req.DisplayOrder;
        faq.IsPublished = req.IsPublished;
        faq.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(faq);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var faq = await db.Faqs.FirstOrDefaultAsync(f => f.FaqId == id, ct)
                  ?? throw new AppException("FAQ not found.", StatusCodes.Status404NotFound);
        db.Faqs.Remove(faq);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Faq> Ordered() =>
        db.Faqs.AsNoTracking().OrderBy(f => f.DisplayOrder).ThenBy(f => f.FaqId);

    private static (string question, string answer) Validate(SaveFaqRequest req)
    {
        var question = Clean(req.Question, 300) ?? "";
        var answer = Clean(req.Answer, 4000) ?? "";
        if (question.Length == 0 || answer.Length == 0)
            throw new AppException("A question and an answer are both required.", StatusCodes.Status400BadRequest);
        return (question, answer);
    }

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }

    private static readonly System.Linq.Expressions.Expression<Func<Faq, FaqDto>> Map =
        f => new FaqDto(f.FaqId, f.Question, f.Answer, f.Category, f.DisplayOrder, f.IsPublished);

    private static FaqDto ToDto(Faq f) =>
        new(f.FaqId, f.Question, f.Answer, f.Category, f.DisplayOrder, f.IsPublished);
}
