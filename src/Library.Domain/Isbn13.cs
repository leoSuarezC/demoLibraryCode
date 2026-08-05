namespace Library.Domain;

/// <summary>
/// ISBN normalisation and check-digit validation.
/// </summary>
/// <remarks>
/// Catalogues accumulate ISBNs typed by hand, pasted from suppliers and scanned from
/// barcodes, in every combination of hyphens and spacing. Normalising on the way in
/// keeps the uniqueness index meaningful, and validating the check digit catches
/// transposed digits at the point of entry rather than months later.
/// </remarks>
public static class Isbn13
{
    /// <summary>Strips separators and upper-cases the ISBN-10 'X' check digit.</summary>
    public static string Normalise(string raw) =>
        new(raw.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static bool IsValid(string normalised) => normalised.Length switch
    {
        10 => IsValidIsbn10(normalised),
        13 => IsValidIsbn13(normalised),
        _ => false
    };

    private static bool IsValidIsbn10(string s)
    {
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            if (!char.IsDigit(s[i])) return false;
            sum += (s[i] - '0') * (10 - i);
        }

        var check = s[9] switch
        {
            'X' => 10,
            var c when char.IsDigit(c) => c - '0',
            _ => -1
        };
        if (check < 0) return false;

        return (sum + check) % 11 == 0;
    }

    private static bool IsValidIsbn13(string s)
    {
        if (!s.All(char.IsDigit)) return false;

        var sum = 0;
        for (var i = 0; i < 12; i++)
            sum += (s[i] - '0') * (i % 2 == 0 ? 1 : 3);

        var check = (10 - sum % 10) % 10;
        return check == s[12] - '0';
    }
}
