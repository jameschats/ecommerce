namespace ecomm.api.Features.Catalog.Services;

/// <summary>
/// Orders design numbers the way the trade reads them: 9 before 10, 225PAM before 1005FC.
///
/// Design numbers are alphanumeric — a number with a suffix ("1005FC", "225PAM", "10 Wt Col")
/// — so a plain string sort compares them digit by digit and puts 1005 ahead of 225, which
/// makes a price list look shuffled to anyone scanning for a number. Digit runs are compared
/// as numbers and everything else as text, case-insensitively.
/// </summary>
public sealed class DesignNoComparer : IComparer<string?>
{
    public static readonly DesignNoComparer Instance = new();

    public int Compare(string? a, string? b)
    {
        a ??= "";
        b ??= "";

        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                var startA = i;
                var startB = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;

                // Leading zeros trimmed first, so "007" and "7" are the same number; after
                // that the longer run is the larger number, and equal lengths compare digit
                // by digit. Avoids parsing, which would overflow on a long enough run.
                var numA = a.AsSpan(startA, i - startA).TrimStart('0');
                var numB = b.AsSpan(startB, j - startB).TrimStart('0');
                if (numA.Length != numB.Length) return numA.Length - numB.Length;

                var digits = numA.SequenceCompareTo(numB);
                if (digits != 0) return digits;
            }
            else
            {
                var chars = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (chars != 0) return chars;
                i++;
                j++;
            }
        }

        // One ran out: the shorter of two otherwise-equal design numbers sorts first.
        return (a.Length - i) - (b.Length - j);
    }
}
