using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Checkout;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ecomm.api.Features.Orders;

public sealed record InvoicePdf(byte[] Bytes, string FileName);

public interface IInvoiceService
{
    Task<(long invoiceId, string invoiceNumber)> GenerateForOrderAsync(long orderId, CancellationToken ct = default);
    Task<InvoicePdf?> RenderPdfAsync(long orderId, long? userId, bool isAdmin, CancellationToken ct = default);
}

public sealed class InvoiceService : IInvoiceService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;
    private readonly ITaxService _tax;

    public InvoiceService(EcommerceDbContext db, ITaxService tax)
    {
        _db = db;
        _tax = tax;
    }

    public async Task<(long invoiceId, string invoiceNumber)> GenerateForOrderAsync(long orderId, CancellationToken ct = default)
    {
        var existing = await _db.Invoices.FirstOrDefaultAsync(i => i.OrderId == orderId, ct);
        if (existing is not null) return (existing.InvoiceId, existing.InvoiceNumber);

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found for invoicing.");
        var items = await _db.OrderItems.Include(oi => oi.CustomFieldValues)
            .Where(oi => oi.OrderId == orderId).OrderBy(oi => oi.OrderItemId).ToListAsync(ct);

        var billing = order.BillingAddressId ?? order.ShippingAddressId;
        var addr = billing is null ? null : await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.CustomerAddressId == billing, ct);

        // Always snapshotted, even when it matches the billing address. A blank Ship To on a
        // dispatch document is ambiguous — it reads equally as "same address" and "we never
        // captured one", and the person packing the box cannot tell which.
        var shipId = order.ShippingAddressId ?? billing;
        var shipAddr = shipId is null
            ? addr
            : await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.CustomerAddressId == shipId, ct) ?? addr;

        var interState = await _tax.IsInterStateAsync(addr?.State, ct);

        var cgst = interState ? 0m : Math.Round(order.TaxAmount / 2m, 2, MidpointRounding.AwayFromZero);
        var sgst = interState ? 0m : order.TaxAmount - cgst;
        var igst = interState ? order.TaxAmount : 0m;
        // Shown regardless of whether GST was broken out as a separate line — prices here are
        // GST-inclusive by store policy even on a Bill of Supply, so the registration number
        // is still meaningful to print.
        var sellerGstin = await SettingAsync("StoreGstin", ct);

        var invoice = new Invoice
        {
            TenantId = Tenant,
            OrderId = orderId,
            InvoiceNumber = $"TMP-{Guid.NewGuid():N}".Substring(0, 20),
            InvoiceDate = DateTime.UtcNow.Date,
            BillingName = addr?.RecipientName,
            BuyerCompanyName = addr?.CompanyName,
            BillingAddress = addr is null ? null : FormatAddress(addr),
            ShippingAddress = shipAddr is null ? null : FormatShipTo(shipAddr),
            GstNumber = sellerGstin,
            // Snapshotted, like the name and address beside it: correcting a GST number in
            // the address book must not rewrite a document already issued.
            BuyerGstin = addr?.Gstin,
            Subtotal = order.Subtotal,
            TaxAmount = order.TaxAmount,
            CgstAmount = cgst,
            SgstAmount = sgst,
            IgstAmount = igst,
            TotalAmount = order.TotalAmount,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        invoice.InvoiceNumber = $"INV-{invoice.InvoiceDate:yyyy}-{invoice.InvoiceId:D5}";

        // Variant is folded into the printed name here rather than added as its own column —
        // InvoiceItem is a frozen snapshot of what shipped, same idea as DesignNo/HsnCode below.
        var variantIds = items.Where(i => i.ProductVariantId != null).Select(i => i.ProductVariantId!.Value).Distinct().ToList();
        var variantNames = variantIds.Count == 0
            ? new Dictionary<long, string?>()
            : await _db.ProductVariants.Where(v => variantIds.Contains(v.ProductVariantId))
                .ToDictionaryAsync(v => v.ProductVariantId, v => v.Name, ct);

        // Custom-field values need the InvoiceItemId, assigned onto these same tracked entities
        // only once SaveChangesAsync below runs — so they're copied across in a second pass.
        var itemsWithAnswers = new List<(InvoiceItem InvoiceItem, ICollection<OrderItemCustomFieldValue> Answers)>();

        foreach (var oi in items)
        {
            var variantName = oi.ProductVariantId is { } vid && variantNames.TryGetValue(vid, out var n) ? n : null;
            var invoiceItem = new InvoiceItem
            {
                InvoiceId = invoice.InvoiceId,
                ProductId = oi.ProductId,
                ProductName = string.IsNullOrWhiteSpace(variantName) ? oi.ProductName : $"{oi.ProductName} ({variantName})",
                DesignNo = oi.DesignNo,
                HsnCode = oi.HsnCode,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                TaxRate = oi.TaxRate,
                TaxAmount = oi.TaxAmount,
                LineTotal = oi.LineTotal,
                CreatedAt = DateTime.UtcNow,
            };
            _db.InvoiceItems.Add(invoiceItem);
            if (oi.CustomFieldValues.Count > 0) itemsWithAnswers.Add((invoiceItem, oi.CustomFieldValues));
        }
        await _db.SaveChangesAsync(ct);

        foreach (var (invoiceItem, answers) in itemsWithAnswers)
        {
            foreach (var a in answers)
            {
                _db.InvoiceItemCustomFieldValues.Add(new InvoiceItemCustomFieldValue
                {
                    InvoiceItemId = invoiceItem.InvoiceItemId, Label = a.Label, Value = a.Value, CreatedAt = DateTime.UtcNow,
                });
            }
        }
        if (itemsWithAnswers.Count > 0) await _db.SaveChangesAsync(ct);

        return (invoice.InvoiceId, invoice.InvoiceNumber);
    }

    public async Task<InvoicePdf?> RenderPdfAsync(long orderId, long? userId, bool isAdmin, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null) return null;
        if (!isAdmin && order.UserId != userId) return null;

        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.OrderId == orderId, ct);
        if (invoice is null) return null;
        var items = await _db.InvoiceItems.Include(i => i.CustomFieldValues)
            .Where(i => i.InvoiceId == invoice.InvoiceId).OrderBy(i => i.InvoiceItemId).ToListAsync(ct);
        var sellerName = await SettingAsync("StoreLegalName", ct) ?? "CalendarShop";
        var sellerState = await SettingAsync("StoreState", ct) ?? "";
        var sellerContact = await SellerContactLinesAsync(ct);

        var bytes = BuildPdf(invoice, items, order, sellerName, sellerState, sellerContact);
        return new InvoicePdf(bytes, $"{invoice.InvoiceNumber}.pdf");
    }

    /// <summary>Address + phone(s) + email, in the same one-source-of-truth settings the
    /// contact page and footer already read (Store.* keys).</summary>
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
        Invoice inv, List<InvoiceItem> items, Order order, string sellerName, string sellerState, List<string> sellerContact)
    {
        // Inferred from the document's own stored amounts, not the current TaxMode setting, so
        // an invoice issued under an older setting still prints the way it was charged.
        var billOfSupply = inv.TaxAmount <= 0m;
        var docTitle = billOfSupply ? "BILL OF SUPPLY" : "TAX INVOICE";
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
                            if (!string.IsNullOrEmpty(inv.GstNumber)) c.Item().Text($"GSTIN: {inv.GstNumber}");
                            if (!string.IsNullOrEmpty(sellerState)) c.Item().Text($"State: {sellerState}");
                            foreach (var line in sellerContact) c.Item().Text(line).FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(180).Column(c =>
                        {
                            c.Item().AlignRight().Text(docTitle).FontSize(13).Bold().FontColor(Colors.Black);
                            c.Item().AlignRight().Text($"No: {inv.InvoiceNumber}");
                            c.Item().AlignRight().Text($"Date: {inv.InvoiceDate:dd MMM yyyy}");
                            c.Item().AlignRight().Text($"Order: {order.OrderNumber}");
                        });
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Bill To and, only when it differs, Ship To — side by side so a
                    // dispatcher can read the delivery address without hunting for it.
                    if (!string.IsNullOrEmpty(inv.BillingName) || !string.IsNullOrEmpty(inv.BillingAddress))
                    {
                        col.Item().PaddingBottom(8).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Bill To").Bold();
                                // Trading name first when there is one: the bill belongs to
                                // the shop, with the person named under it.
                                if (!string.IsNullOrEmpty(inv.BuyerCompanyName)) c.Item().Text(inv.BuyerCompanyName).Bold();
                                if (!string.IsNullOrEmpty(inv.BillingName)) c.Item().Text(inv.BillingName);
                                if (!string.IsNullOrEmpty(inv.BillingAddress)) c.Item().Text(inv.BillingAddress);
                                if (!string.IsNullOrEmpty(inv.BuyerGstin)) c.Item().PaddingTop(2).Text($"GSTIN: {inv.BuyerGstin}").Bold();
                            });

                            // Falls back to the billing block for invoices issued before Ship To
                            // was always snapshotted, so reprints of those are not left blank.
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Ship To").Bold();
                                if (!string.IsNullOrEmpty(inv.ShippingAddress))
                                {
                                    c.Item().Text(inv.ShippingAddress);
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty(inv.BillingName)) c.Item().Text(inv.BillingName);
                                    if (!string.IsNullOrEmpty(inv.BillingAddress)) c.Item().Text(inv.BillingAddress);
                                }
                            });
                        });
                    }

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(26);   // S.No
                            c.RelativeColumn(4);
                            c.RelativeColumn(1.3f);
                            c.RelativeColumn(0.8f);
                            c.RelativeColumn(1.3f);
                            // No GST% column on a Bill of Supply: a document that charges no GST
                            // should not have a tax column at all, and one reading "0%" on every
                            // line invites the question of why it is there.
                            if (!billOfSupply) c.RelativeColumn(1f);
                            c.RelativeColumn(1.5f);
                        });
                        table.Header(h =>
                        {
                            void Hd(string t, bool right = false)
                            {
                                var cell = h.Cell().Background(Colors.Grey.Lighten3).Padding(4);
                                (right ? cell.AlignRight() : cell.AlignLeft()).Text(t).Bold().FontSize(8);
                            }
                            // Design No. rather than HSN: this trade sells by design number, it is
                            // what the customer orders by, and a Bill of Supply charges no GST for
                            // an HSN code to classify.
                            Hd("#"); Hd("Item"); Hd("Design No"); Hd("Qty"); Hd("Rate", true);
                            if (!billOfSupply) Hd("GST%", true);
                            Hd("Amount", true);
                        });
                        var lineNo = 0;
                        foreach (var it in items)
                        {
                            lineNo++;
                            // Amounts on the lines are bare numbers; the currency is stated once
                            // on the totals below rather than repeated on every row.
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(lineNo.ToString());
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Column(c =>
                            {
                                c.Item().Text(it.ProductName);
                                // Custom-text answers (e.g. "Mention Correct Design number")
                                // printed under the item so whoever packs the order sees them —
                                // otherwise this is the one place the buyer's own words never
                                // reach paper.
                                foreach (var a in it.CustomFieldValues)
                                    c.Item().Text($"{a.Label}: {a.Value}").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                            });
                            // Blank, not "-", where no design number was captured: a dash reads as
                            // a value. Lines predating the snapshot genuinely have nothing to show.
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(it.DesignNo ?? "");
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(it.Quantity.ToString());
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(Amount(it.UnitPrice));
                            if (!billOfSupply)
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text($"{it.TaxRate:0.##}%");
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(Amount(it.LineTotal));
                        }
                    });

                    var shipping = order.ShippingAmount;
                    var taxAddedOnTop = Math.Abs(order.TotalAmount - (inv.Subtotal + inv.TaxAmount + shipping)) < 0.01m;
                    var inclusive = !billOfSupply && !taxAddedOnTop;

                    col.Item().PaddingTop(10).AlignRight().Column(c =>
                    {
                        void Line(string label, string val, bool bold = false)
                        {
                            c.Item().Row(r =>
                            {
                                var left = r.ConstantItem(120).Text(label);
                                if (bold) left.Bold();
                                var right = r.ConstantItem(90).AlignRight().Text(val);
                                if (bold) right.Bold();
                            });
                        }
                        // DiscountAmount is the saving against MRP, and it is *already* inside
                        // Subtotal — the line prices are what was charged. Printing "Subtotal"
                        // and then subtracting the discount from it took it off twice on the
                        // page: an invoice reading 7,200 − 2,290 = 7,200, which a customer can
                        // only read as an error. The build-up runs the other way, as the
                        // quotation already does it: gross, less the saving, giving the subtotal.
                        //
                        // Nothing new is stored. Net Total is derived at render, so reprinting an
                        // old invoice corrects it too.
                        if (order.DiscountAmount > 0)
                        {
                            Line("Net Total", Money(inv.Subtotal + order.DiscountAmount));
                            Line("Discount", "-" + Money(order.DiscountAmount));
                        }
                        Line("Subtotal", Money(inv.Subtotal));
                        if (taxAddedOnTop && !billOfSupply)   // Exclusive — GST added on top
                        {
                            if (inv.IgstAmount > 0) Line("IGST", Money(inv.IgstAmount));
                            else { Line("CGST", Money(inv.CgstAmount)); Line("SGST", Money(inv.SgstAmount)); }
                        }
                        Line("Shipping", Money(shipping));
                        c.Item().PaddingVertical(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                        Line("Grand Total", Money(inv.TotalAmount), true);
                        if (inclusive)
                        {
                            var note = inv.IgstAmount > 0
                                ? $"Inclusive of IGST {Money(inv.IgstAmount)}"
                                : $"Inclusive of all taxes (CGST {Money(inv.CgstAmount)} + SGST {Money(inv.SgstAmount)})";
                            c.Item().PaddingTop(3).Text(note).FontSize(8).FontColor(Colors.Grey.Darken1);
                        }
                        // A Bill of Supply carries no separate tax line by definition, but prices
                        // are still GST-inclusive by store policy — said plainly rather than left
                        // for the customer to wonder whether GST was charged at all.
                        if (billOfSupply)
                            c.Item().PaddingTop(3).Text("All prices are inclusive of GST.").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });

                page.Footer().AlignCenter().Text("Thank you for shopping with CalendarShop · This is a computer-generated invoice.")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
            });
        });
        return doc.GeneratePdf();
    }

    /// <summary>Totals — the currency is stated here, where it is read once.</summary>
    private static string Money(decimal v) => "Rs. " + v.ToString("N2");

    /// <summary>Line amounts — bare, so the column of figures reads as a column of figures.</summary>
    private static string Amount(decimal v) => v.ToString("N2");

    private static string FormatAddress(CustomerAddress a)
    {
        var parts = new[] { a.Line1, a.Line2, $"{a.City}, {a.State} {a.Pincode}".Trim().TrimEnd(','), a.Country };
        return string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>
    /// Ship To reads as a delivery label, so it leads with who is receiving it and ends with
    /// a number to call — the two things a courier actually needs and Bill To does not carry.
    /// </summary>
    private static string FormatShipTo(CustomerAddress a)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(a.RecipientName)) parts.Add(a.RecipientName!);
        parts.Add(FormatAddress(a));
        if (!string.IsNullOrWhiteSpace(a.Phone)) parts.Add($"Ph: {a.Phone}");
        return string.Join("\n", parts);
    }

    private Task<string?> SettingAsync(string key, CancellationToken ct) =>
        _db.Settings.Where(s => s.TenantId == Tenant && s.SettingKey == key).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
}
