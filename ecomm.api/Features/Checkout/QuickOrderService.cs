using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Checkout;

public interface IQuickOrderService
{
    Task<QuickOrderConfigDto> GetConfigAsync(CancellationToken ct = default);
    Task<QuickOrderQuoteDto> QuoteAsync(QuickOrderQuoteRequest req, CancellationToken ct = default);
    Task<PlaceQuickOrderResult> PlaceAsync(long userId, PlaceQuickOrderRequest req, CancellationToken ct = default);
}

/// <summary>
/// Prices a quick-order basket server-side (design.md §7.1).
///
/// The browser computes running totals as the buyer types — it has to, since a round trip
/// per keystroke would be unusable. This service is the authority: it re-reads every price
/// from the database and ignores whatever the client believed. A client that sends a
/// tampered or merely stale price gets the correct one back, not an error.
/// </summary>
public sealed class QuickOrderService : IQuickOrderService
{
    private const long Tenant = 1;

    /// <summary>Delivery states, as listed on the reference site's order form.</summary>
    private static readonly string[] DeliveryStates =
    [
        "Andhra Pradesh", "Assam", "Bihar", "Chhattisgarh", "Delhi", "Goa", "Gujarat",
        "Haryana", "Himachal Pradesh", "Jharkhand", "Karnataka", "Kerala", "Madhya Pradesh",
        "Maharashtra", "Odisha", "Puducherry", "Punjab", "Rajasthan", "Tamil Nadu",
        "Telangana", "Uttar Pradesh", "Uttarakhand", "West Bengal",
    ];

    private readonly EcommerceDbContext _db;
    private readonly Notifications.IOrderMailer _mailer;

    public QuickOrderService(EcommerceDbContext db, Notifications.IOrderMailer mailer)
    {
        _db = db;
        _mailer = mailer;
    }

    public async Task<QuickOrderConfigDto> GetConfigAsync(CancellationToken ct = default)
    {
        var settings = await LoadSettingsAsync(ct);

        var stateMins = await _db.StateMinOrderAmounts
            .Where(s => s.TenantId == Tenant && s.IsActive)
            .OrderBy(s => s.StateName)
            .Select(s => new StateMinOrderDto(s.StateName, s.MinOrderAmount))
            .ToListAsync(ct);

        return new QuickOrderConfigDto(
            Decimal(settings, "QuickOrder.MinOrderAmount"),
            Decimal(settings, "QuickOrder.PackingChargePct"),
            stateMins,
            DeliveryStates,
            Text(settings, "QuickOrder.AnnouncementText"),
            Text(settings, "QuickOrder.PriceValidUpto"));
    }

    public async Task<QuickOrderQuoteDto> QuoteAsync(QuickOrderQuoteRequest req, CancellationToken ct = default)
    {
        var warnings = new List<string>();

        // Collapse duplicates and drop non-positive quantities before touching the database.
        var wanted = (req.Lines ?? [])
            .Where(l => l.Quantity > 0)
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var lines = new List<QuickOrderQuoteLineDto>();

        if (wanted.Count > 0)
        {
            var ids = wanted.Keys.ToList();
            var products = await _db.Products
                .Where(p => p.TenantId == Tenant && !p.IsDeleted && p.IsActive
                            && p.Status == "Active" && ids.Contains(p.ProductId))
                .Select(p => new
                {
                    p.ProductId,
                    p.Sku,
                    p.Name,
                    p.Price,
                    p.CompareAtPrice,
                    InStock = p.InventoryRecords.Sum(i => i.AvailableQty) > 0,
                })
                .ToListAsync(ct);

            var found = products.ToDictionary(p => p.ProductId);

            // A product the buyer had in their basket but that is no longer purchasable is
            // dropped from the quote and named, rather than silently priced at zero.
            foreach (var id in ids.Where(id => !found.ContainsKey(id)))
            {
                warnings.Add($"An item is no longer available and was removed (product {id}).");
                wanted.Remove(id);
            }

            foreach (var p in products.OrderBy(p => p.Name))
            {
                var qty = wanted[p.ProductId];
                lines.Add(new QuickOrderQuoteLineDto(
                    p.ProductId, p.Sku, p.Name, qty,
                    p.Price, p.CompareAtPrice,
                    Round(p.Price * qty), p.InStock));

                if (!p.InStock) warnings.Add($"{p.Name} is currently out of stock.");
            }
        }

        var settings = await LoadSettingsAsync(ct);

        var subTotal = Round(lines.Sum(l => l.LineTotal));
        var netTotal = Round(lines.Sum(l => (l.CompareAtPrice ?? l.UnitPrice) * l.Quantity));
        // Never negative, even if an MRP is mis-keyed below the selling price.
        var discountTotal = Math.Max(0m, Round(netTotal - subTotal));

        var minOrder = await ResolveMinOrderAsync(req.State, settings, ct);
        var packingPct = Decimal(settings, "QuickOrder.PackingChargePct");
        var packingCharges = Round(subTotal * packingPct / 100m);

        var beforeRounding = subTotal + packingCharges;
        var roundOffEnabled = Text(settings, "QuickOrder.RoundOffEnabled") != "false";
        var overall = roundOffEnabled ? Math.Round(beforeRounding, 0, MidpointRounding.AwayFromZero) : beforeRounding;
        var roundOff = Round(overall - beforeRounding);

        if (lines.Count > 0 && subTotal < minOrder)
            warnings.Add($"Minimum order for {req.State ?? "this state"} is ₹{minOrder:N0}.");

        return new QuickOrderQuoteDto(
            lines,
            lines.Count,
            lines.Sum(l => l.Quantity),
            netTotal,
            discountTotal,
            subTotal,
            minOrder,
            packingPct,
            packingCharges,
            roundOff,
            overall,
            lines.Count > 0 && subTotal >= minOrder,
            warnings);
    }

    /// <summary>
    /// Creates the order from a re-priced basket (design.md §7).
    ///
    /// Re-quotes rather than trusting anything the client sent: prices, the minimum-order
    /// rule and the total are all recomputed here, so a basket edited in flight — or simply
    /// stale because prices changed while the buyer was typing — cannot produce an order at
    /// the wrong amount.
    /// </summary>
    public async Task<PlaceQuickOrderResult> PlaceAsync(long userId, PlaceQuickOrderRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Name is required.");
        if (string.IsNullOrWhiteSpace(req.Address)) throw new AppException("Delivery address is required.");
        if (string.IsNullOrWhiteSpace(req.State)) throw new AppException("Delivery state is required.");

        // The reference site's rule, and worth keeping — it keeps a lot of bad data out.
        var mobile = new string((req.Mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        if (mobile.Length != 10) throw new AppException("Enter a valid 10-digit mobile number.");

        var quote = await QuoteAsync(new QuickOrderQuoteRequest(req.Lines, req.State), ct);

        if (quote.Lines.Count == 0) throw new AppException("Your order is empty.");
        if (!quote.MeetsMinimum)
            throw new AppException($"Minimum order for {req.State} is ₹{quote.MinOrderAmount:N0}.");

        var now = DateTime.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var order = new Order
        {
            TenantId = Tenant,
            UserId = userId,
            OrderNumber = await NextOrderNumberAsync(ct),
            // Awaiting a manual UPI/bank transfer that an admin confirms (design.md §8).
            Status = "Pending",
            Currency = "INR",
            Subtotal = quote.SubTotal,
            DiscountAmount = quote.DiscountTotal,
            TaxAmount = 0m,                       // Prices are tax-inclusive in Phase 1 (design.md §14.1)
            ShippingAmount = quote.PackingCharges,
            TotalAmount = quote.OverallAmount,
            // Free-text delivery details: Phase 1 ships by transport to the buyer's city,
            // so the platform's address book and pincode serviceability are not used.
            Notes = $"Name: {req.Name.Trim()}\nMobile: {mobile}\nEmail: {req.Email?.Trim()}\n"
                  + $"State: {req.State}\nCity: {req.City?.Trim()}\nAddress: {req.Address.Trim()}\n"
                  + $"Round off: {quote.RoundOff:0.00}",
            PlacedAt = now,
            CreatedAt = now,
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        foreach (var line in quote.Lines)
        {
            _db.OrderItems.Add(new OrderItem
            {
                OrderId = order.OrderId,
                ProductId = line.ProductId,
                Sku = line.Sku,
                ProductName = line.Name,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountAmount = Round(((line.CompareAtPrice ?? line.UnitPrice) - line.UnitPrice) * line.Quantity),
                TaxRate = 0m,
                TaxAmount = 0m,
                LineTotal = line.LineTotal,
            });

            // Reserve rather than deduct: stock is committed when the payment is confirmed
            // and released if the order is cancelled, matching the platform's existing model.
            var inventory = await _db.Inventory
                .Where(i => i.TenantId == Tenant && i.ProductId == line.ProductId)
                .OrderByDescending(i => i.AvailableQty)
                .FirstOrDefaultAsync(ct);

            if (inventory is not null)
            {
                var take = Math.Min(inventory.AvailableQty, line.Quantity);
                inventory.AvailableQty -= take;
                inventory.ReservedQty += take;
            }
        }

        await SyncProfileAndAddressAsync(userId, req, mobile, now, ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // After the commit, deliberately: the order exists whether or not the email goes
        // out, and the mailer swallows its own failures so a mail problem can never fail
        // an order the buyer has already been charged for in their own mind.
        await _mailer.SendOrderPlacedAsync(order.OrderId, req.Email, ct);

        return new PlaceQuickOrderResult(order.OrderId, order.OrderNumber, order.TotalAmount, order.Status);
    }

    /// <summary>
    /// Copies what the order form told us into the customer's profile and address book.
    ///
    /// A mobile-OTP account is created with nothing but a phone number, so "My account"
    /// sits empty even though the buyer has just typed their name, email and address into
    /// the order form. This fills those in.
    ///
    /// Blanks only — never overwrite something the customer has set themselves. A dealer
    /// ordering on behalf of a shop may put the shop's name on the order, and that should
    /// not silently rename their account.
    /// </summary>
    private async Task SyncProfileAndAddressAsync(
        long userId, PlaceQuickOrderRequest req, string mobile, DateTime now, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null) return;

        var name = req.Name?.Trim();
        if (string.IsNullOrWhiteSpace(user.FullName) && !string.IsNullOrWhiteSpace(name))
            user.FullName = name;

        // Email and phone are unique per tenant (uq_users_tenant_email, uq_users_tenant_phone),
        // so neither can be claimed here without first checking nobody else holds it. Filling
        // in a blank profile is a convenience; a duplicate throws on SaveChanges inside the
        // order transaction and takes the whole order down with it. That is exactly what
        // happened to a customer signed in by email OTP who typed the mobile number their
        // older account already owned: every attempt to order failed with "An unexpected
        // error occurred" and nothing explained why.
        //
        // When the value is taken we simply leave the profile field blank. The order itself
        // carries the name, email and mobile from the form regardless, so nothing the buyer
        // typed is lost — only the optional profile back-fill is skipped.
        var email = req.Email?.Trim();
        if (string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(email) && email.Contains('@'))
        {
            var normalized = email.ToUpperInvariant();
            var emailTaken = await _db.Users.AnyAsync(
                u => u.TenantId == Tenant && u.UserId != userId && u.NormalizedEmail == normalized, ct);
            if (!emailTaken)
            {
                user.Email = email;
                user.NormalizedEmail = normalized;
                // Deliberately NOT marked verified: they typed it into a form, they did not
                // prove they can receive mail at it. Only an OTP against that address does.
            }
        }

        if (string.IsNullOrWhiteSpace(user.PhoneNumber) && !string.IsNullOrWhiteSpace(mobile))
        {
            var phoneTaken = await _db.Users.AnyAsync(
                u => u.TenantId == Tenant && u.UserId != userId && u.PhoneNumber == mobile, ct);
            if (!phoneTaken) user.PhoneNumber = mobile;
        }

        user.UpdatedAt = now;

        // Save the delivery address, unless the same one is already on file. Re-ordering
        // every week should not leave a customer with fifty identical address rows.
        var line1 = req.Address?.Trim() ?? string.Empty;
        var city = req.City?.Trim() ?? string.Empty;
        var state = req.State?.Trim() ?? string.Empty;
        if (line1.Length == 0) return;

        var exists = await _db.CustomerAddresses.AnyAsync(
            a => a.UserId == userId && !a.IsDeleted
                 && a.Line1 == line1 && a.City == city && a.State == state, ct);
        if (exists) return;

        var isFirst = !await _db.CustomerAddresses.AnyAsync(a => a.UserId == userId && !a.IsDeleted, ct);

        _db.CustomerAddresses.Add(new CustomerAddress
        {
            TenantId = Tenant,
            UserId = userId,
            Label = "Delivery",
            RecipientName = name,
            Phone = mobile,
            Line1 = line1,
            City = city,
            State = state,
            // Phase 1 ships by transport to the buyer's city, so no pincode is collected
            // (design.md §14.3). The column is non-null, hence the empty string.
            Pincode = string.Empty,
            Country = "India",
            AddressType = "Both",
            IsDefault = isFirst,
            CreatedAt = now,
        });
    }

    /// <summary>
    /// Sequential per-day order number. Generated inside the placement transaction, so two
    /// concurrent orders cannot read the same count and collide.
    /// </summary>
    private async Task<string> NextOrderNumberAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow;
        var prefix = $"DCS{today:yyMMdd}";
        var todayCount = await _db.Orders.CountAsync(o => o.OrderNumber.StartsWith(prefix), ct);
        return $"{prefix}{(todayCount + 1):D4}";
    }

    /// <summary>Per-state override if one exists, otherwise the global minimum.</summary>
    private async Task<decimal> ResolveMinOrderAsync(
        string? state, Dictionary<string, string?> settings, CancellationToken ct)
    {
        var fallback = Decimal(settings, "QuickOrder.MinOrderAmount");
        if (string.IsNullOrWhiteSpace(state)) return fallback;

        var match = await _db.StateMinOrderAmounts
            .Where(s => s.TenantId == Tenant && s.IsActive && s.StateName == state)
            .Select(s => (decimal?)s.MinOrderAmount)
            .FirstOrDefaultAsync(ct);

        return match ?? fallback;
    }

    private async Task<Dictionary<string, string?>> LoadSettingsAsync(CancellationToken ct)
        => await _db.Settings
            .Where(s => s.SettingKey.StartsWith("QuickOrder.") || s.SettingKey.StartsWith("Payment."))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);

    private static decimal Decimal(Dictionary<string, string?> settings, string key)
        => settings.TryGetValue(key, out var raw) && decimal.TryParse(raw, out var value) ? value : 0m;

    private static string? Text(Dictionary<string, string?> settings, string key)
        => settings.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw : null;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
