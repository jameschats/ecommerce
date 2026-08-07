namespace ecomm.api.Features.Orders;

/// <summary>
/// Reads the "Field: value" lines the quick-order path writes into <c>Order.Notes</c>.
///
/// Quick orders capture an email and mobile on the form itself, which need not match the
/// account the order is attached to — a dealer may order for a shop under a profile created
/// from an earlier phone number. Anything sent about the order goes to what they typed.
///
/// Shared rather than copied: OrderMailer and OrderService both need it, and two copies of a
/// rule about where a customer's email comes from would eventually disagree.
/// </summary>
internal static class OrderNotes
{
    public static string? Field(string? notes, string field)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;

        var prefix = field + ":";
        foreach (var line in notes.Split('\n'))
        {
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var value = line[prefix.Length..].Trim();
            if (value.Length == 0) return null;

            // An email field that is not an email is worse than none — it would send a
            // customer's order details to whatever they mistyped.
            return field.Equals("Email", StringComparison.OrdinalIgnoreCase) && !value.Contains('@')
                ? null
                : value;
        }
        return null;
    }
}
