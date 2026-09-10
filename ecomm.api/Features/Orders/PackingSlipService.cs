using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ecomm.api.Features.Orders;

public sealed record PackingSlipPdf(byte[] Bytes, string FileName);

/// <summary>
/// The dispatch-floor twin of InvoiceService's PDF — same seller header, Order #, Shipping and
/// Billing Information, and item table, with every price/tax/total column dropped. A packer
/// needs what and how many, never what it cost; printing the price on paper that rides inside
/// the customer's box is the one thing this document must never do.
///
/// Rendered straight from Order + OrderItems rather than a saved snapshot — a packing slip
/// carries no legal/sequential numbering requirement the way a tax invoice does, so there is
/// nothing here that needs a permanent, once-only record the way Invoice does.
/// </summary>
public interface IPackingSlipService
{
    Task<PackingSlipPdf?> RenderPdfAsync(long orderId, CancellationToken ct = default);
}

public sealed class PackingSlipService : IPackingSlipService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;

    public PackingSlipService(EcommerceDbContext db) => _db = db;

    public async Task<PackingSlipPdf?> RenderPdfAsync(long orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return null;

        // By design number, not insertion order — matches InvoiceService's item order, so the
        // two documents printed for the same order always list items in the same sequence.
        var items = await _db.OrderItems.Include(oi => oi.CustomFieldValues).Where(oi => oi.OrderId == orderId)
            .OrderBy(oi => oi.DesignNo == null || oi.DesignNo == "").ThenBy(oi => oi.DesignNo).ThenBy(oi => oi.OrderItemId)
            .ToListAsync(ct);

        // Variant folded into the printed name, same as InvoiceService — a packer reading
        // "10 x 15 ... Finished" with no size/colour has nothing to pack correctly.
        var variantIds = items.Where(i => i.ProductVariantId != null).Select(i => i.ProductVariantId!.Value).Distinct().ToList();
        var variantNames = variantIds.Count == 0
            ? new Dictionary<long, string?>()
            : await _db.ProductVariants.Where(v => variantIds.Contains(v.ProductVariantId))
                .ToDictionaryAsync(v => v.ProductVariantId, v => v.Name, ct);

        var billing = order.BillingAddressId ?? order.ShippingAddressId;
        var billAddr = billing is null ? null : await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.CustomerAddressId == billing, ct);

        // Same fallback as InvoiceService: a blank Shipping block on a dispatch document is
        // ambiguous, so it is always filled in — from the billing address when no separate
        // shipping address was captured, rather than left for the packer to guess.
        var shipId = order.ShippingAddressId ?? billing;
        var shipAddr = shipId is null
            ? billAddr
            : await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.CustomerAddressId == shipId, ct) ?? billAddr;

        var sellerName = await SettingAsync("StoreLegalName", ct) ?? "CalendarShop";
        var sellerContact = await SellerContactLinesAsync(ct);
        // Typed on the order form, same field the buyer's Email comes from (Order.Notes) — the
        // one thing on this document a courier/loading crew actually needs that a Bill To/Ship
        // To block does not carry.
        var transportName = OrderNotes.Field(order.Notes, "Transport");

        var bytes = BuildPdf(order, items, variantNames, billAddr, shipAddr, sellerName, sellerContact, transportName);
        return new PackingSlipPdf(bytes, $"PackingSlip-{order.OrderNumber}.pdf");
    }

    /// <summary>Address + phone(s) + email — the same Store.* settings the invoice, contact
    /// page and footer all read, so this never drifts from what they show.</summary>
    private async Task<List<string>> SellerContactLinesAsync(CancellationToken ct)
    {
        var address = await SettingAsync("Store.AddressLine", ct);
        var mobile1 = await SettingAsync("Store.Mobile1", ct);
        var mobile2 = await SettingAsync("Store.Mobile2", ct);
        var landline1 = await SettingAsync("Store.Landline1", ct);
        var email = await SettingAsync("Store.Email", ct);

        var phones = new[] { mobile1, mobile2, landline1 }.Where(p => !string.IsNullOrWhiteSpace(p));
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(address)) lines.Add(address!);
        if (phones.Any()) lines.Add("Ph: " + string.Join(", ", phones));
        if (!string.IsNullOrWhiteSpace(email)) lines.Add(email!);
        return lines;
    }

    private static byte[] BuildPdf(
        Order order, List<OrderItem> items, Dictionary<long, string?> variantNames,
        CustomerAddress? billAddr, CustomerAddress? shipAddr,
        string sellerName, List<string> sellerContact, string? transportName)
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
                            c.Item().Text(sellerName).FontSize(15).Bold().FontColor(Colors.Black);
                            foreach (var line in sellerContact) c.Item().Text(line).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(180).Column(c =>
                        {
                            c.Item().AlignRight().Text("PACKING LIST").FontSize(13).Bold().FontColor(Colors.Black);
                            c.Item().AlignRight().Text($"Order #{order.OrderNumber}");
                            c.Item().AlignRight().Text($"Order placed: {order.CreatedAt:dd MMM yyyy hh:mm tt}");
                            if (!string.IsNullOrWhiteSpace(transportName))
                                c.Item().AlignRight().Text($"Transport: {transportName}").Bold();
                        });
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    if (billAddr is not null || shipAddr is not null)
                    {
                        col.Item().PaddingBottom(10).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Shipping Information").Bold();
                                if (shipAddr is not null) WriteAddress(c, shipAddr);
                            });
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Billing Information").Bold();
                                if (billAddr is not null) WriteAddress(c, billAddr);
                            });
                        });
                    }

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(26);    // S.No
                            c.RelativeColumn(1.6f);  // Design No
                            c.RelativeColumn(5f);    // Product Name
                            c.RelativeColumn(0.9f);  // Qty
                        });
                        table.Header(h =>
                        {
                            void Hd(string t, bool right = false)
                            {
                                var cell = h.Cell().Background(Colors.Grey.Lighten3).Padding(4);
                                (right ? cell.AlignRight() : cell.AlignLeft()).Text(t).Bold().FontSize(8);
                            }
                            Hd("#"); Hd("Design No"); Hd("Product Name"); Hd("Qty", true);
                        });
                        var lineNo = 0;
                        foreach (var it in items)
                        {
                            lineNo++;
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(lineNo.ToString());
                            // Blank, not "-", where no design number was captured — a dash reads
                            // as a value (same rule InvoiceService follows for this column).
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(it.DesignNo ?? "");
                            var variantName = it.ProductVariantId is { } vid && variantNames.TryGetValue(vid, out var n) ? n : null;
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Column(c =>
                            {
                                c.Item().Text(string.IsNullOrWhiteSpace(variantName) ? it.ProductName : $"{it.ProductName} ({variantName})");
                                // Custom-text answers (e.g. "Mention Correct Design number") —
                                // the one thing on this document that isn't a standard product
                                // attribute, and the reason a packer would need to read it at all.
                                foreach (var a in it.CustomFieldValues)
                                    c.Item().Text($"{a.Label}: {a.Value}").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                            });
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(it.Quantity.ToString());
                        }
                    });

                    col.Item().PaddingTop(14).Row(row =>
                    {
                        row.RelativeItem().Text($"Total items: {items.Count}").FontSize(8).FontColor(Colors.Grey.Darken1);
                        row.RelativeItem().AlignRight().Text($"Total quantity: {items.Sum(i => i.Quantity)}").Bold();
                    });
                });

                page.Footer().AlignCenter()
                    .Text("This is a computer-generated packing list — no price information is included.")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
            });
        });
        return doc.GeneratePdf();
    }

    private static void WriteAddress(ColumnDescriptor c, CustomerAddress a)
    {
        if (!string.IsNullOrWhiteSpace(a.RecipientName)) c.Item().Text(a.RecipientName);
        if (!string.IsNullOrWhiteSpace(a.CompanyName)) c.Item().Text(a.CompanyName);
        c.Item().Text(a.Line1);
        if (!string.IsNullOrWhiteSpace(a.Line2)) c.Item().Text(a.Line2!);
        c.Item().Text($"{a.City}, {a.State} {a.Pincode}".Trim().TrimEnd(','));
        if (!string.IsNullOrWhiteSpace(a.Country)) c.Item().Text(a.Country!);
        if (!string.IsNullOrWhiteSpace(a.Phone)) c.Item().Text(a.Phone!);
    }

    private Task<string?> SettingAsync(string key, CancellationToken ct) =>
        _db.Settings.Where(s => s.TenantId == Tenant && s.SettingKey == key).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
}
