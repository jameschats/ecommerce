using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Checkout;

public interface IQuickOrderService
{
    Task<QuickOrderConfigDto> GetConfigAsync(CancellationToken ct = default);
    /// <summary>userId is 0 for the anonymous pricing call — a per-user coupon limit simply
    /// can't be checked yet. It is re-checked for real, against the signed-in buyer, at Place.</summary>
    Task<QuickOrderQuoteDto> QuoteAsync(QuickOrderQuoteRequest req, long userId = 0, CancellationToken ct = default);
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
    private readonly Inventory.IInventoryService _inventory;
    private readonly Coupons.ICouponService _coupons;

    public QuickOrderService(
        EcommerceDbContext db,
        Notifications.IOrderMailer mailer,
        Inventory.IInventoryService inventory,
        Coupons.ICouponService coupons)
    {
        _db = db;
        _mailer = mailer;
        _inventory = inventory;
        _coupons = coupons;
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

    public async Task<QuickOrderQuoteDto> QuoteAsync(QuickOrderQuoteRequest req, long userId = 0, CancellationToken ct = default)
    {
        var warnings = new List<string>();

        // Collapse duplicates and drop non-positive quantities before touching the database.
        // Keyed by (ProductId, VariantId) rather than ProductId alone, so two variants of the
        // same product price and stock-check independently instead of merging into one line.
        var wanted = (req.Lines ?? [])
            .Where(l => l.Quantity > 0)
            .GroupBy(l => (l.ProductId, l.VariantId))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var lines = new List<QuickOrderQuoteLineDto>();

        if (wanted.Count > 0)
        {
            var ids = wanted.Keys.Select(k => k.ProductId).Distinct().ToList();
            var products = await _db.Products
                .Where(p => p.TenantId == Tenant && !p.IsDeleted && p.IsActive
                            && p.Status == "Active" && ids.Contains(p.ProductId))
                .Select(p => new { p.ProductId, p.Sku, p.DesignNo, p.Name, p.Price, p.CompareAtPrice })
                .ToListAsync(ct);
            var found = products.ToDictionary(p => p.ProductId);

            var variantIds = wanted.Keys.Where(k => k.VariantId.HasValue).Select(k => k.VariantId!.Value).Distinct().ToList();
            var variants = variantIds.Count == 0
                ? new Dictionary<long, ProductVariant>()
                : await _db.ProductVariants.Where(v => variantIds.Contains(v.ProductVariantId)).ToDictionaryAsync(v => v.ProductVariantId, ct);

            // Stock per (product, variant): a plain product's row has ProductVariantId = null
            // and is summed as before; a variant's row is looked up by its own id specifically
            // — per-variant stock, not the whole product's combined total.
            var invRows = await _db.Inventory
                .Where(i => i.TenantId == Tenant && ids.Contains(i.ProductId))
                .Select(i => new { i.ProductId, i.ProductVariantId, i.AvailableQty })
                .ToListAsync(ct);

            // A product/option combination the buyer had in their basket but that is no
            // longer purchasable is dropped from the quote and named, rather than silently
            // priced at zero.
            foreach (var key in wanted.Keys.ToList())
            {
                if (!found.ContainsKey(key.ProductId))
                {
                    warnings.Add($"An item is no longer available and was removed (product {key.ProductId}).");
                    wanted.Remove(key);
                }
                else if (key.VariantId is { } vid && (!variants.TryGetValue(vid, out var v) || v.ProductId != key.ProductId || !v.IsActive))
                {
                    warnings.Add($"A selected option for {found[key.ProductId].Name} is no longer available and was removed.");
                    wanted.Remove(key);
                }
            }

            foreach (var kv in wanted.OrderBy(kv => found[kv.Key.ProductId].Name))
            {
                var (key, qty) = (kv.Key, kv.Value);
                var p = found[key.ProductId];
                var variant = key.VariantId is { } vid ? variants[vid] : null;
                var unitPrice = p.Price + (variant?.PriceAdjustment ?? 0m);
                var compareAt = p.CompareAtPrice is { } m ? m + (variant?.PriceAdjustment ?? 0m) : (decimal?)null;

                var available = variant is not null
                    ? invRows.Where(i => i.ProductId == key.ProductId && i.ProductVariantId == variant.ProductVariantId).Sum(i => i.AvailableQty)
                    : invRows.Where(i => i.ProductId == key.ProductId && i.ProductVariantId == null).Sum(i => i.AvailableQty);
                var inStock = available > 0;

                lines.Add(new QuickOrderQuoteLineDto(
                    p.ProductId, p.Sku, p.Name, qty,
                    unitPrice, compareAt,
                    Round(unitPrice * qty), inStock, p.DesignNo,
                    variant?.ProductVariantId, variant?.Name));

                if (!inStock) warnings.Add($"{p.Name}{(variant?.Name is { } vn ? $" ({vn})" : "")} is currently out of stock.");
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

        // Evaluated against the pre-coupon subtotal, matching the cart checkout's rule.
        // userId is 0 for the anonymous pricing call, so a per-user usage limit reads as
        // "not yet used" here — it is enforced for real, against the actual buyer, at Place.
        var coupon = await _coupons.EvaluateAsync(req.CouponCode, userId, subTotal, ct);
        var couponDiscount = coupon.Ok ? coupon.Discount : 0m;

        var beforeRounding = subTotal - couponDiscount + packingCharges;
        var roundOffEnabled = Text(settings, "QuickOrder.RoundOffEnabled") != "false";
        var overall = roundOffEnabled ? Math.Round(beforeRounding, 0, MidpointRounding.AwayFromZero) : beforeRounding;
        if (overall < 0m) overall = 0m;
        var roundOff = Round(overall - beforeRounding);

        if (lines.Count > 0 && subTotal < minOrder)
            warnings.Add($"Minimum order for {req.State ?? "this state"} is ₹{minOrder:N0}.");

        return new QuickOrderQuoteDto(
            Lines: lines,
            ItemCount: lines.Count,
            TotalUnits: lines.Sum(l => l.Quantity),
            NetTotal: netTotal,
            DiscountTotal: discountTotal,
            SubTotal: subTotal,
            MinOrderAmount: minOrder,
            PackingChargePct: packingPct,
            PackingCharges: packingCharges,
            CouponDiscount: couponDiscount,
            CouponCode: coupon.Ok ? coupon.Code : (string.IsNullOrWhiteSpace(req.CouponCode) ? null : req.CouponCode.Trim().ToUpperInvariant()),
            CouponMessage: coupon.Ok ? null : coupon.Error,
            CouponApplied: coupon.Ok,
            RoundOff: roundOff,
            OverallAmount: overall,
            MeetsMinimum: lines.Count > 0 && subTotal >= minOrder,
            Warnings: warnings);
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

        var quote = await QuoteAsync(new QuickOrderQuoteRequest(req.Lines, req.State, req.CouponCode), userId, ct);

        if (quote.Lines.Count == 0) throw new AppException("Your order is empty.");
        if (!quote.MeetsMinimum)
            throw new AppException($"Minimum order for {req.State} is ₹{quote.MinOrderAmount:N0}.");
        // Re-validated against the real, signed-in buyer (the pricing call above only knew
        // userId 0), so never trust the client-side flag — a coupon that stopped applying
        // between quote and place (limit reached, expired) must not silently be dropped.
        if (!string.IsNullOrWhiteSpace(req.CouponCode) && !quote.CouponApplied)
            throw new AppException(quote.CouponMessage ?? "That coupon can't be applied.");

        var now = DateTime.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Real address rows, resolved before the order so it can point at them. Until now
        // the delivery details were serialised into Orders.Notes as prose — unqueryable, and
        // useless for an invoice "Bill To" block — while BillingAddressId and
        // ShippingAddressId sat NULL despite having existed since 005.
        var billingId = await ResolveAddressAsync(
            userId, "Billing", req.Name, req.BusinessName, req.Gstin, mobile,
            req.Address, req.City, req.State, now, ct);

        // Ship-to falls back to bill-to, which is the common case: one address, entered once.
        var shippingId = req.ShipToDifferent && !string.IsNullOrWhiteSpace(req.ShipAddress)
            ? await ResolveAddressAsync(
                userId, "Shipping",
                string.IsNullOrWhiteSpace(req.ShipName) ? req.Name : req.ShipName,
                req.BusinessName, null,
                string.IsNullOrWhiteSpace(req.ShipMobile) ? mobile : req.ShipMobile,
                req.ShipAddress,
                string.IsNullOrWhiteSpace(req.ShipCity) ? req.City : req.ShipCity,
                string.IsNullOrWhiteSpace(req.ShipState) ? req.State : req.ShipState,
                now, ct)
            : billingId;

        var order = new Order
        {
            BillingAddressId = billingId,
            ShippingAddressId = shippingId,
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
            // Addresses live in CustomerAddresses now, linked above. Notes keeps only what
            // has nowhere else to go: the contact email, and the rounding applied to the
            // total, which is otherwise unrecoverable from the stored amounts.
            Notes = $"Email: {req.Email?.Trim()}\nRound off: {quote.RoundOff:0.00}",
            PlacedAt = now,
            CreatedAt = now,
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        // Cost and design number at the moment of sale, read here rather than carried on the
        // quote: the quote is sent to the browser, and cost price is nobody's business but the
        // shop's.
        //
        // Both are snapshotted for the same reason. A ProductId is not a stable description of
        // what was sold — this catalogue was edited in place from its demo seed, so an id from
        // July names a different item today. Reading either value back from Products at report
        // or print time attributes it to the wrong goods.
        var lineProductIds = quote.Lines.Select(l => l.ProductId).ToList();
        var snapshot = await _db.Products
            .Where(p => p.TenantId == Tenant && lineProductIds.Contains(p.ProductId))
            .Select(p => new { p.ProductId, p.CostPrice, p.DesignNo, p.HsnCode })
            .ToDictionaryAsync(x => x.ProductId, x => x, ct);

        foreach (var line in quote.Lines)
        {
            _db.OrderItems.Add(new OrderItem
            {
                OrderId = order.OrderId,
                ProductId = line.ProductId,
                ProductVariantId = line.VariantId,
                Sku = line.Sku,
                ProductName = line.Name,
                DesignNo = snapshot.TryGetValue(line.ProductId, out var snap) ? snap.DesignNo : null,
                HsnCode = snap?.HsnCode,
                UnitCost = snap?.CostPrice,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountAmount = Round(((line.CompareAtPrice ?? line.UnitPrice) - line.UnitPrice) * line.Quantity),
                TaxRate = 0m,
                TaxAmount = 0m,
                LineTotal = line.LineTotal,
            });

            // Reserve rather than deduct: stock is committed when the payment is confirmed
            // and released if the order is cancelled, matching the platform's existing model.
            //
            // Through IInventoryService rather than mutating Inventory here, so the movement
            // lands in InventoryTransactions and the low-stock alert can fire. Doing it inline
            // left the shop's primary order path invisible to both — the stock ledger recorded
            // nothing for the orders that actually happen, and nobody was ever told to reorder.
            //
            // Only what exists is reserved: Phase 1 does not refuse an order for want of stock,
            // so a short line reserves the remainder instead of failing the sale. Matched by
            // variant, not just product — two variants of the same product hold separate stock.
            var available = await _db.Inventory
                .Where(i => i.TenantId == Tenant && i.ProductId == line.ProductId && i.ProductVariantId == line.VariantId)
                .SumAsync(i => (int?)i.AvailableQty, ct) ?? 0;

            var take = Math.Min(available, line.Quantity);
            if (take > 0)
                await _inventory.ReserveAsync(line.ProductId, line.VariantId, take, "Order", order.OrderId, ct);
        }

        await SyncProfileAsync(userId, req, mobile, now, ct);

        // The mirrored estimate became a sale, so it must stop showing as abandoned. Done
        // inline rather than by a job: the one moment we know for certain it converted is
        // right here.
        var openCarts = await _db.Carts
            .Where(c => c.TenantId == Tenant && c.UserId == userId && c.Status == "Active")
            .ToListAsync(ct);
        foreach (var cart in openCarts)
        {
            cart.Status = "Converted";
            cart.UpdatedAt = now;
        }

        if (quote.CouponApplied)
        {
            // Re-evaluated rather than threaded through the quote DTO, so a coupon's internal
            // id never has to leave the server. Safe to call twice — EvaluateAsync only reads.
            var coupon = await _coupons.EvaluateAsync(req.CouponCode, userId, quote.SubTotal, ct);
            if (coupon.Ok && coupon.CouponId is { } couponId)
                await _coupons.RecordUsageAsync(couponId, userId, order.OrderId, quote.CouponDiscount, ct);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // After the commit, deliberately: the order exists whether or not the email goes
        // out, and the mailer swallows its own failures so a mail problem can never fail
        // an order the buyer has already been charged for in their own mind.
        await _mailer.SendOrderPlacedAsync(order.OrderId, req.Email, ct);

        return new PlaceQuickOrderResult(order.OrderId, order.OrderNumber, order.TotalAmount, order.Status);
    }

    /// <summary>
    /// Copies what the order form told us into the customer's profile.
    ///
    /// A mobile-OTP account is created with nothing but a phone number, so "My account"
    /// sits empty even though the buyer has just typed their name and email into the order
    /// form. This fills those in. The address is no longer handled here — it is a real
    /// linked row now, resolved by ResolveAddressAsync before the order is created.
    ///
    /// Blanks only — never overwrite something the customer has set themselves. A dealer
    /// ordering on behalf of a shop may put the shop's name on the order, and that should
    /// not silently rename their account.
    /// </summary>
    private async Task SyncProfileAsync(
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
    }

    /// <summary>
    /// Finds the customer's matching address or creates one, and returns its id so the order
    /// can point at it.
    ///
    /// Matched on the address itself — line, city, state and purpose — so a dealer ordering
    /// every week reuses one row instead of accumulating fifty identical ones. When a match
    /// is found its company name and GSTIN are refreshed from what was just typed, because
    /// the buyer correcting their own GST number should take effect, not be quietly ignored.
    ///
    /// Returns null only when there is no address line at all, which the caller has already
    /// rejected — the order simply ends up unlinked rather than the placement failing.
    /// </summary>
    private async Task<long?> ResolveAddressAsync(
        long userId, string addressType,
        string? recipientName, string? companyName, string? gstin, string? phone,
        string? address, string? city, string? state,
        DateTime now, CancellationToken ct)
    {
        var line1 = address?.Trim() ?? string.Empty;
        if (line1.Length == 0) return null;

        var theCity = city?.Trim() ?? string.Empty;
        var theState = state?.Trim() ?? string.Empty;
        var company = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
        // Uppercased: GSTINs are conventionally written that way, and it keeps the same
        // number typed in two cases from reading as two different registrations.
        var gst = string.IsNullOrWhiteSpace(gstin) ? null : gstin.Trim().ToUpperInvariant();

        var existing = await _db.CustomerAddresses.FirstOrDefaultAsync(
            a => a.UserId == userId && !a.IsDeleted && a.AddressType == addressType
                 && a.Line1 == line1 && a.City == theCity && a.State == theState, ct);

        if (existing is not null)
        {
            if (company is not null) existing.CompanyName = company;
            if (gst is not null) existing.Gstin = gst;
            existing.UpdatedAt = now;
            return existing.CustomerAddressId;
        }

        var isFirst = !await _db.CustomerAddresses.AnyAsync(a => a.UserId == userId && !a.IsDeleted, ct);

        var created = new CustomerAddress
        {
            TenantId = Tenant,
            UserId = userId,
            Label = addressType == "Shipping" ? "Delivery" : "Billing",
            RecipientName = string.IsNullOrWhiteSpace(recipientName) ? null : recipientName.Trim(),
            CompanyName = company,
            Gstin = gst,
            Phone = phone,
            Line1 = line1,
            City = theCity,
            State = theState,
            // Phase 1 ships by transport to the buyer's city, so no pincode is collected
            // (design.md §14.3). The column is non-null, hence the empty string.
            Pincode = string.Empty,
            Country = "India",
            AddressType = addressType,
            IsDefault = isFirst,
            CreatedAt = now,
        };
        _db.CustomerAddresses.Add(created);

        // Saved here rather than with the order: the order carries this row's id as a foreign
        // key, and an unsaved row has no id to carry.
        await _db.SaveChangesAsync(ct);
        return created.CustomerAddressId;
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
