using System.Text.RegularExpressions;

namespace ecomm.api.Common;

public static partial class Slug
{
    public static string From(string input)
    {
        var s = (input ?? string.Empty).Trim().ToLowerInvariant();
        s = NonSlugChars().Replace(s, "");
        s = Whitespace().Replace(s, "-");
        s = MultiDash().Replace(s, "-").Trim('-');
        return s.Length == 0 ? "item" : s;
    }

    [GeneratedRegex(@"[^a-z0-9\s-]")] private static partial Regex NonSlugChars();
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"-+")] private static partial Regex MultiDash();
}
