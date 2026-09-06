using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ecomm.api.Features.Checkout;

public sealed record QuotePdf(byte[] Content, string FileName);

public interface IQuoteDocumentService
{
    Task<QuotePdf> RenderAsync(QuickOrderQuoteRequest req, string? customerName, CancellationToken ct = default);
}

/// <summary>
/// A printable quote for the basket on screen (Anna's "generating quote from the available
/// products").
///
/// Priced through QuoteAsync — the same call the order form uses — so the paper and the
/// screen cannot disagree. Nothing is persisted: a quote is a statement of today's prices,
/// and storing one would invite it being treated as a commitment the shop never made. The
/// validity date printed on it comes from the same setting the storefront already shows.
/// </summary>
public sealed class QuoteDocumentService : IQuoteDocumentService
{
    private const long Tenant = 1;

    private readonly EcommerceDbContext _db;
    private readonly IQuickOrderService _quickOrder;

    public QuoteDocumentService(EcommerceDbContext db, IQuickOrderService quickOrder)
    {
        _db = db;
        _quickOrder = quickOrder;
    }

    public async Task<QuotePdf> RenderAsync(
        QuickOrderQuoteRequest req, string? customerName, CancellationToken ct = default)
    {
        var quote = await _quickOrder.QuoteAsync(req, ct: ct);

        var sellerName = await SettingAsync("StoreLegalName", ct) ?? "CalendarShop";
        var sellerState = await SettingAsync("StoreState", ct);
        var sellerGstin = await SettingAsync("StoreGstin", ct);
        var validUpto = await SettingAsync("QuickOrder.PriceValidUpto", ct);
        var announcement = await SettingAsync("QuickOrder.AnnouncementText", ct);

        // Same one-source-of-truth Store.* settings the contact page, footer and invoice read.
        var sellerAddress = await SettingAsync("Store.AddressLine", ct);
        var mobile1 = await SettingAsync("Store.Mobile1", ct);
        var mobile2 = await SettingAsync("Store.Mobile2", ct);
        var landline1 = await SettingAsync("Store.Landline1", ct);
        var sellerEmail = await SettingAsync("Store.Email", ct);
        var phones = new[] { mobile1, mobile2, landline1 }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

        var date = DateTime.UtcNow;
        var reference = $"Q-{date:yyyyMMdd}-{date:HHmmss}";

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(sellerName).FontSize(15).Bold().FontColor(Colors.Black);
                            if (!string.IsNullOrWhiteSpace(sellerGstin)) c.Item().Text($"GSTIN: {sellerGstin}");
                            if (!string.IsNullOrWhiteSpace(sellerState)) c.Item().Text($"State: {sellerState}");
                            if (!string.IsNullOrWhiteSpace(sellerAddress)) c.Item().Text(sellerAddress!).FontSize(8).FontColor(Colors.Grey.Darken1);
                            if (phones.Count > 0) c.Item().Text("Ph: " + string.Join(", ", phones)).FontSize(8).FontColor(Colors.Grey.Darken1);
                            if (!string.IsNullOrWhiteSpace(sellerEmail)) c.Item().Text(sellerEmail!).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(180).Column(c =>
                        {
                            c.Item().AlignRight().Text("QUOTATION").FontSize(13).Bold().FontColor(Colors.Black);
                            c.Item().AlignRight().Text($"Ref: {reference}");
                            c.Item().AlignRight().Text($"Date: {date:dd MMM yyyy}");
                        });
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    if (!string.IsNullOrWhiteSpace(customerName))
                    {
                        col.Item().PaddingBottom(8).Column(c =>
                        {
                            c.Item().Text("Prepared for").Bold();
                            c.Item().Text(customerName);
                        });
                    }

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(70);   // design no
                            cd.RelativeColumn();     // product
                            cd.ConstantColumn(45);   // qty
                            cd.ConstantColumn(60);   // MRP
                            cd.ConstantColumn(60);   // rate
                            cd.ConstantColumn(70);   // amount
                        });

                        table.Header(h =>
                        {
                            void Head(string t, bool right = false)
                            {
                                var cell = h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingVertical(4);
                                (right ? cell.AlignRight() : cell).Text(t).Bold();
                            }
                            Head("Design No"); Head("Product"); Head("Qty", true);
                            Head("MRP", true); Head("Rate", true); Head("Amount", true);
                        });

                        foreach (var l in quote.Lines)
                        {
                            void Cell(string t, bool right = false)
                            {
                                var c = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);
                                (right ? c.AlignRight() : c).Text(t);
                            }
                            // The design number, not the SKU. They are often the same string,
                            // which is what let the SKU sit under this heading unnoticed — but
                            // the SKU is internal and the design number is what a dealer orders
                            // by, so the two drift apart and the quote stops matching the invoice.
                            Cell(l.DesignNo ?? "");
                            Cell(l.Name);
                            Cell(l.Quantity.ToString(), true);
                            Cell(l.CompareAtPrice is { } m ? Money(m) : "—", true);
                            Cell(Money(l.UnitPrice), true);
                            Cell(Money(l.LineTotal), true);
                        }
                    });

                    col.Item().PaddingTop(10).AlignRight().Column(c =>
                    {
                        void Line(string label, string val, bool bold = false)
                        {
                            c.Item().Row(r =>
                            {
                                var left = r.ConstantItem(130).Text(label);
                                if (bold) left.Bold();
                                var right = r.ConstantItem(90).AlignRight().Text(val);
                                if (bold) right.Bold();
                            });
                        }

                        // Currency on the totals, bare figures on the lines above — the same rule
                        // the invoice follows, so a dealer holding both documents is not left
                        // wondering why one states a currency and the other does not.
                        Line("Net Total", Total(quote.NetTotal));
                        if (quote.DiscountTotal > 0) Line("Discount", $"− {Total(quote.DiscountTotal)}");
                        Line("Sub Total", Total(quote.SubTotal));
                        if (quote.PackingCharges > 0)
                            Line($"Packing ({quote.PackingChargePct:0.##}%)", Total(quote.PackingCharges));
                        if (quote.RoundOff != 0) Line("Round off", Total(quote.RoundOff));
                        c.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        Line("Total", Total(quote.OverallAmount), bold: true);
                    });

                    // Says plainly what a quote is. Without it a printed price list is easily
                    // read as a promise that outlives the prices it was built from.
                    col.Item().PaddingTop(16).Column(c =>
                    {
                        c.Item().Text("Terms").Bold();
                        c.Item().Text(string.IsNullOrWhiteSpace(validUpto)
                            ? "This is a quotation, not an invoice. Prices are subject to change."
                            : $"This is a quotation, not an invoice. Prices valid up to {validUpto}.");
                        c.Item().Text("All prices are inclusive of GST.");
                        if (quote.MinOrderAmount > 0)
                            // A figure in a sentence needs its currency; only the aligned column
                            // of line amounts can safely go without one.
                            c.Item().Text($"Minimum order for {req.State}: {Total(quote.MinOrderAmount)}.");
                        if (!string.IsNullOrWhiteSpace(announcement))
                            c.Item().PaddingTop(2).Text(announcement);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        });

        return new QuotePdf(doc.GeneratePdf(), $"quotation-{date:yyyyMMdd-HHmmss}.pdf");
    }

    /// <summary>Line figures — bare, matching the invoice's item rows.</summary>
    private static string Money(decimal v) => v.ToString("N2");

    /// <summary>Totals — where the currency is stated, matching the invoice.</summary>
    private static string Total(decimal v) => "Rs. " + v.ToString("N2");

    private Task<string?> SettingAsync(string key, CancellationToken ct) =>
        _db.Settings.Where(s => s.TenantId == Tenant && s.SettingKey == key)
            .Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
}
