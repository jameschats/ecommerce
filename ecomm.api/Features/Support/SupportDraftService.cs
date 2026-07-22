using System.Text;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Ai;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Support;

public sealed record SupportDraftDto(string Draft, IReadOnlyList<string> GroundedOn);

public interface ISupportDraftService
{
    Task<SupportDraftDto> DraftReplyAsync(long conversationId, CancellationToken ct = default);
}

/// <summary>
/// Suggests a reply for the merchant to edit and send (C3). Deliberately merchant-in-the-loop:
/// nothing here is ever sent automatically, so a wrong draft costs a few seconds of editing rather
/// than the store's credibility with a customer.
///
/// The model only ever sees facts we assembled — the thread, the linked order's real status and
/// tracking, the merchant's own published FAQs and policies. It is told to refuse rather than guess,
/// because the alternative (a confident invented delivery date) is the one failure mode that would
/// make this feature worse than useless.
/// </summary>
public sealed class SupportDraftService(EcommerceDbContext db, IAiCreditService credits) : ISupportDraftService
{
    private const string SystemPrompt =
        "You draft replies for a small Indian online store's customer-support inbox. " +
        "You are writing FOR the merchant, to their customer — warm, plain, and brief (2-4 sentences). " +
        "CRITICAL RULES:\n" +
        "1. Use ONLY the facts in the CONTEXT below. Never invent order status, dates, prices or stock.\n" +
        "2. Never promise a delivery date. If an estimate exists in the context you may repeat it, " +
        "described as an estimate; otherwise say you'll confirm.\n" +
        "3. Never offer a refund, discount, replacement or exception — those are the merchant's to decide. " +
        "If the customer asks for one, acknowledge it and say the team will confirm.\n" +
        "4. If the context doesn't answer the question, say so plainly and state what you'll check. " +
        "A short honest reply is correct; a confident wrong one is not.\n" +
        "5. No placeholders like [name] or [date]. Write text the merchant can send as-is.\n" +
        "Return only the reply body — no subject line, no signature block.";

    public async Task<SupportDraftDto> DraftReplyAsync(long conversationId, CancellationToken ct = default)
    {
        var convo = await db.SupportTickets.AsNoTracking()
            .FirstOrDefaultAsync(c => c.SupportTicketId == conversationId && c.Axis == ConversationAxis.ShopperMerchant, ct)
            ?? throw new AppException("Conversation not found.", StatusCodes.Status404NotFound);

        var (context, grounding) = await BuildContextAsync(convo, ct);

        var draft = await credits.MeterAsync(AiCreditPricing.SupportDraft, async ai =>
        {
            var c = await ai.CompleteAsync(new AiPrompt(SystemPrompt, context, Json: false, MaxTokens: 400), ct);
            return (c.Text.Trim(), c);
        }, ct);

        return new SupportDraftDto(draft, grounding);
    }

    /// <summary>
    /// Assembles the prompt and, alongside it, the list of what the draft was grounded on — shown to
    /// the merchant so they can judge the suggestion instead of trusting it.
    /// </summary>
    private async Task<(string context, List<string> grounding)> BuildContextAsync(SupportTicket convo, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var grounding = new List<string>();

        sb.AppendLine("CONTEXT");
        sb.AppendLine("=======");
        sb.AppendLine($"Subject: {convo.Subject}");

        // --- the conversation itself ---
        var messages = await db.SupportMessages.AsNoTracking()
            .Where(m => m.SupportTicketId == convo.SupportTicketId && !m.IsInternalNote)
            .OrderBy(m => m.SupportMessageId)
            .Select(m => new { m.AuthorType, m.Body })
            .ToListAsync(ct);

        sb.AppendLine().AppendLine("Conversation so far:");
        foreach (var m in messages)
            sb.AppendLine($"  {(m.AuthorType == MessageAuthorType.Shopper ? "Customer" : "Store")}: {m.Body}");
        grounding.Add($"{messages.Count} message{(messages.Count == 1 ? "" : "s")} in this thread");

        // --- the linked order, if any ---
        if (convo.OrderId is { } orderId)
        {
            var order = await db.Orders.AsNoTracking()
                .Where(o => o.OrderId == orderId)
                .Select(o => new { o.OrderNumber, o.Status, o.PlacedAt })
                .FirstOrDefaultAsync(ct);

            if (order is not null)
            {
                sb.AppendLine().AppendLine("Their order:");
                sb.AppendLine($"  Number: {order.OrderNumber}");
                sb.AppendLine($"  Status: {order.Status}");
                if (order.PlacedAt is { } placed) sb.AppendLine($"  Placed: {placed:d MMM yyyy}");

                var shipment = await db.Shipments.AsNoTracking()
                    .Where(s => s.OrderId == orderId).OrderByDescending(s => s.ShipmentId)
                    .Select(s => new { s.ShipmentId, s.Courier, s.TrackingNumber, s.EstimatedDeliveryDate })
                    .FirstOrDefaultAsync(ct);

                if (shipment is not null)
                {
                    sb.AppendLine($"  Courier: {shipment.Courier} (tracking {shipment.TrackingNumber})");
                    if (shipment.EstimatedDeliveryDate is { } eta)
                        sb.AppendLine($"  Estimated delivery: {eta:d MMM yyyy} (an estimate, not a promise)");

                    var scans = await db.ShipmentCheckpoints.AsNoTracking()
                        .Where(c => c.ShipmentId == shipment.ShipmentId)
                        .OrderByDescending(c => c.ShipmentCheckpointId).Take(4)
                        .Select(c => new { c.RawStatus, c.Location, c.OccurredAt, c.CreatedAt })
                        .ToListAsync(ct);

                    if (scans.Count > 0)
                    {
                        sb.AppendLine("  Latest courier scans (newest first):");
                        foreach (var s in scans)
                            sb.AppendLine($"    {(s.OccurredAt ?? s.CreatedAt):d MMM HH:mm} — {s.RawStatus}{(s.Location is null ? "" : $" at {s.Location}")}");
                        grounding.Add($"{scans.Count} courier scan{(scans.Count == 1 ? "" : "s")}");
                    }
                }
                grounding.Add($"order {order.OrderNumber} ({order.Status})");
            }
        }

        // --- the merchant's own words ---
        var faqs = await db.Faqs.AsNoTracking()
            .Where(f => f.IsPublished).OrderBy(f => f.DisplayOrder).Take(20)
            .Select(f => new { f.Question, f.Answer }).ToListAsync(ct);
        if (faqs.Count > 0)
        {
            sb.AppendLine().AppendLine("Store FAQs:");
            foreach (var f in faqs) sb.AppendLine($"  Q: {f.Question}\n  A: {f.Answer}");
            grounding.Add($"{faqs.Count} published FAQ{(faqs.Count == 1 ? "" : "s")}");
        }

        var policies = await db.StorePolicies.AsNoTracking()
            .Where(p => (p.Handle == "refund" || p.Handle == "shipping") && p.BodyHtml != null && p.BodyHtml != "")
            .Select(p => new { p.Handle, p.BodyHtml }).ToListAsync(ct);
        if (policies.Count > 0)
        {
            sb.AppendLine().AppendLine("Store policies (plain text):");
            foreach (var p in policies)
                sb.AppendLine($"  {p.Handle}: {StripHtml(p.BodyHtml!, 1200)}");
            grounding.Add($"{policies.Count} store polic{(policies.Count == 1 ? "y" : "ies")}");
        }

        sb.AppendLine().AppendLine("Draft the store's next reply to the customer.");
        return (sb.ToString(), grounding);
    }

    /// <summary>Policies are stored as sanitized HTML; the model wants prose, not markup.</summary>
    private static string StripHtml(string html, int max)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        // Tags become spaces, so "<b>7 days</b>." would otherwise read "7 days ." — tidy it up
        // rather than feed the model punctuation noise.
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+([.,;:!?])", "$1").Trim();
        return text.Length <= max ? text : text[..max] + "…";
    }
}
