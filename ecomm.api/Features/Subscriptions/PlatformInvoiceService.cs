using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace ecomm.api.Features.Subscriptions;

public sealed record PlatformInvoiceDto(long Id, string InvoiceNumber, DateTime InvoiceDate, decimal TotalAmount, long BillingHistoryId, string DocumentType);
public sealed record PlatformInvoicePdf(byte[] Bytes, string FileName);

public interface IPlatformInvoiceService
{
    /// <summary>Issue a GST tax invoice for one subscription charge. Idempotent per billing-history row.</summary>
    Task GenerateForChargeAsync(long tenantId, long billingHistoryId, decimal grossAmount, CancellationToken ct = default);
    /// <summary>Issue a GST credit note reversing (part of) the invoice for a refunded charge.</summary>
    Task GenerateCreditNoteAsync(long originalChargeBillingHistoryId, long refundBillingHistoryId, decimal refundAmount, string? reason, CancellationToken ct = default);
    Task<IReadOnlyList<PlatformInvoiceDto>> ListForTenantAsync(long tenantId, CancellationToken ct = default);
    Task<PlatformInvoicePdf?> RenderPdfAsync(long tenantId, long invoiceId, CancellationToken ct = default);
}

/// <summary>
/// Issues the platform's GST tax invoice to a merchant for their subscription fee (D1). Numbering is
/// per the platform GSTIN, gap-free per financial year, across all merchants. Prices are treated as
/// GST-inclusive; the taxable value + CGST/SGST (intra-state) or IGST (inter-state) are back-calculated
/// from the seller's vs buyer's state. Buyer details come from the merchant's own store settings.
/// </summary>
public sealed class PlatformInvoiceService(EcommerceDbContext db, ILogger<PlatformInvoiceService> log) : IPlatformInvoiceService
{
    public async Task GenerateForChargeAsync(long tenantId, long billingHistoryId, decimal grossAmount, CancellationToken ct = default)
    {
        if (grossAmount <= 0) return;
        if (await db.PlatformInvoices.AnyAsync(i => i.TenantBillingHistoryId == billingHistoryId, ct)) return;   // idempotent

        var settings = await db.PlatformBillingSettings.FirstOrDefaultAsync(ct) ?? new PlatformBillingSettings();
        var rate = settings.GstRatePercent <= 0 ? 18m : settings.GstRatePercent;

        // Buyer = the merchant's business (from their store settings), tenant-scoped rows read cross-tenant.
        var s = await db.Settings.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && (x.SettingKey == "StoreLegalName" || x.SettingKey == "StoreGstin" || x.SettingKey == "StoreState"))
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, ct);
        var tenantName = await db.Tenants.IgnoreQueryFilters().Where(t => t.TenantId == tenantId).Select(t => t.Name).FirstOrDefaultAsync(ct);
        var buyerName = s.GetValueOrDefault("StoreLegalName") is { Length: > 0 } bn ? bn : tenantName;
        var buyerGstin = s.GetValueOrDefault("StoreGstin");
        var buyerState = s.GetValueOrDefault("StoreState");

        var sellerState = settings.SellerState;
        var interState = !string.IsNullOrWhiteSpace(sellerState) && !string.IsNullOrWhiteSpace(buyerState)
                         && !string.Equals(sellerState.Trim(), buyerState.Trim(), StringComparison.OrdinalIgnoreCase);

        var taxable = Math.Round(grossAmount / (1 + rate / 100m), 2, MidpointRounding.AwayFromZero);
        var gst = grossAmount - taxable;
        decimal cgst = 0, sgst = 0, igst = 0;
        if (interState) igst = gst;
        else { cgst = Math.Round(gst / 2m, 2, MidpointRounding.AwayFromZero); sgst = gst - cgst; }

        var now = DateTime.UtcNow;
        var fy = FinancialYear(now);
        var prefix = string.IsNullOrWhiteSpace(settings.InvoicePrefix) ? "INV" : settings.InvoicePrefix.Trim();
        var (seq, number) = await NextNumberAsync(fy, "Invoice", prefix, ct);

        db.PlatformInvoices.Add(new PlatformInvoice
        {
            TenantId = tenantId, TenantBillingHistoryId = billingHistoryId, DocumentType = "Invoice",
            FinancialYear = fy, SequenceNumber = seq, InvoiceNumber = number, InvoiceDate = now,
            SellerName = settings.SellerLegalName, SellerGstin = settings.SellerGstin, SellerState = sellerState,
            BuyerName = buyerName, BuyerGstin = buyerGstin, BuyerState = buyerState,
            PlaceOfSupply = buyerState ?? "", IsInterState = interState, GstRatePercent = rate,
            TaxableValue = taxable, CgstAmount = cgst, SgstAmount = sgst, IgstAmount = igst, TotalAmount = grossAmount,
            CreatedAt = now,
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex)
        {
            // Lost the number race (unique on FY+number) — the charge itself is already safe; skip the invoice.
            log.LogWarning(ex, "Platform invoice for charge {Charge} not written (likely a numbering race).", billingHistoryId);
        }
    }

    public async Task GenerateCreditNoteAsync(long originalChargeBillingHistoryId, long refundBillingHistoryId, decimal refundAmount, string? reason, CancellationToken ct = default)
    {
        if (refundAmount <= 0) return;
        if (await db.PlatformInvoices.AnyAsync(i => i.TenantBillingHistoryId == refundBillingHistoryId && i.DocumentType == "CreditNote", ct)) return;

        var orig = await db.PlatformInvoices
            .FirstOrDefaultAsync(i => i.TenantBillingHistoryId == originalChargeBillingHistoryId && i.DocumentType == "Invoice", ct);
        if (orig is null) return;   // no original invoice to reverse (e.g. charge predates invoicing)

        var rate = orig.GstRatePercent <= 0 ? 18m : orig.GstRatePercent;
        var taxable = Math.Round(refundAmount / (1 + rate / 100m), 2, MidpointRounding.AwayFromZero);
        var gst = refundAmount - taxable;
        decimal cgst = 0, sgst = 0, igst = 0;
        if (orig.IsInterState) igst = gst;
        else { cgst = Math.Round(gst / 2m, 2, MidpointRounding.AwayFromZero); sgst = gst - cgst; }

        var settings = await db.PlatformBillingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var prefix = (string.IsNullOrWhiteSpace(settings?.InvoicePrefix) ? "INV" : settings!.InvoicePrefix.Trim()) + "-CN";
        var now = DateTime.UtcNow;
        var fy = FinancialYear(now);
        var (seq, number) = await NextNumberAsync(fy, "CreditNote", prefix, ct);

        db.PlatformInvoices.Add(new PlatformInvoice
        {
            TenantId = orig.TenantId, TenantBillingHistoryId = refundBillingHistoryId,
            DocumentType = "CreditNote", OriginalInvoiceId = orig.PlatformInvoiceId, Notes = reason,
            FinancialYear = fy, SequenceNumber = seq, InvoiceNumber = number, InvoiceDate = now,
            SellerName = orig.SellerName, SellerGstin = orig.SellerGstin, SellerState = orig.SellerState,
            BuyerName = orig.BuyerName, BuyerGstin = orig.BuyerGstin, BuyerState = orig.BuyerState,
            PlaceOfSupply = orig.PlaceOfSupply, IsInterState = orig.IsInterState, GstRatePercent = rate,
            TaxableValue = taxable, CgstAmount = cgst, SgstAmount = sgst, IgstAmount = igst, TotalAmount = refundAmount,
            CreatedAt = now,
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) { log.LogWarning(ex, "Credit note for refund {Refund} not written (numbering race).", refundBillingHistoryId); }
    }

    /// <summary>Next gap-free sequence + formatted number for a document type in a financial year.</summary>
    private async Task<(int seq, string number)> NextNumberAsync(string fy, string docType, string prefix, CancellationToken ct)
    {
        var last = await db.PlatformInvoices.Where(i => i.FinancialYear == fy && i.DocumentType == docType)
            .MaxAsync(i => (int?)i.SequenceNumber, ct) ?? 0;
        var seq = last + 1;
        return (seq, $"{prefix}/{fy}/{seq:0000}");
    }

    public async Task<IReadOnlyList<PlatformInvoiceDto>> ListForTenantAsync(long tenantId, CancellationToken ct = default) =>
        await db.PlatformInvoices.AsNoTracking().Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.PlatformInvoiceId)
            .Select(i => new PlatformInvoiceDto(i.PlatformInvoiceId, i.InvoiceNumber, i.InvoiceDate, i.TotalAmount, i.TenantBillingHistoryId, i.DocumentType))
            .ToListAsync(ct);

    public async Task<PlatformInvoicePdf?> RenderPdfAsync(long tenantId, long invoiceId, CancellationToken ct = default)
    {
        var inv = await db.PlatformInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.PlatformInvoiceId == invoiceId && i.TenantId == tenantId, ct);
        if (inv is null) return null;
        var bytes = BuildPdf(inv);
        return new PlatformInvoicePdf(bytes, $"invoice-{inv.InvoiceNumber.Replace('/', '-')}.pdf");
    }

    private static string FinancialYear(DateTime d)
    {
        var startYear = d.Month >= 4 ? d.Year : d.Year - 1;
        return $"{startYear}-{(startYear + 1) % 100:00}";
    }

    private static byte[] BuildPdf(PlatformInvoice inv)
    {
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
                            c.Item().Text(inv.SellerName ?? "WavCommerce").FontSize(15).Bold().FontColor(Colors.Black);
                            if (!string.IsNullOrEmpty(inv.SellerGstin)) c.Item().Text($"GSTIN: {inv.SellerGstin}");
                            if (!string.IsNullOrEmpty(inv.SellerState)) c.Item().Text($"State: {inv.SellerState}");
                        });
                        row.ConstantItem(190).Column(c =>
                        {
                            c.Item().AlignRight().Text(inv.DocumentType == "CreditNote" ? "CREDIT NOTE" : "TAX INVOICE").FontSize(13).Bold().FontColor(Colors.Black);
                            c.Item().AlignRight().Text($"No: {inv.InvoiceNumber}");
                            c.Item().AlignRight().Text($"Date: {inv.InvoiceDate:dd MMM yyyy}");
                            if (inv.DocumentType == "CreditNote" && !string.IsNullOrEmpty(inv.Notes)) c.Item().AlignRight().Text($"Reason: {inv.Notes}").FontSize(8);
                        });
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().PaddingBottom(8).Column(c =>
                    {
                        c.Item().Text("Bill To").Bold();
                        if (!string.IsNullOrEmpty(inv.BuyerName)) c.Item().Text(inv.BuyerName);
                        if (!string.IsNullOrEmpty(inv.BuyerGstin)) c.Item().Text($"GSTIN: {inv.BuyerGstin}");
                        if (!string.IsNullOrEmpty(inv.BuyerState)) c.Item().Text($"State: {inv.BuyerState}");
                        if (!string.IsNullOrEmpty(inv.PlaceOfSupply)) c.Item().Text($"Place of supply: {inv.PlaceOfSupply}");
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(1); c.RelativeColumn(1.5f); });
                        table.Header(h =>
                        {
                            void Hd(string t, bool right = false)
                            {
                                var cell = h.Cell().Background(Colors.Grey.Lighten3).Padding(4);
                                (right ? cell.AlignRight() : cell.AlignLeft()).Text(t).Bold().FontSize(8);
                            }
                            Hd("Description"); Hd("GST%", true); Hd("Amount", true);
                        });
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text("Subscription fee (SaaS)");
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"{inv.GstRatePercent:0.##}%");
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(Money(inv.TaxableValue));
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
                        Line("Taxable value", Money(inv.TaxableValue));
                        if (inv.IgstAmount > 0) Line($"IGST ({inv.GstRatePercent:0.##}%)", Money(inv.IgstAmount));
                        else { Line($"CGST ({inv.GstRatePercent / 2:0.##}%)", Money(inv.CgstAmount)); Line($"SGST ({inv.GstRatePercent / 2:0.##}%)", Money(inv.SgstAmount)); }
                        c.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                        Line("Total", Money(inv.TotalAmount), true);
                    });
                });

                page.Footer().AlignCenter().Text("This is a computer-generated tax invoice.")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
            });
        });
        return doc.GeneratePdf();
    }

    private static string Money(decimal v) => "Rs. " + v.ToString("N2");
}
