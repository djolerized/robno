using System.Text.RegularExpressions;

namespace Textil.Planning.Domain;

/// <summary>Helpers for months kept as "YYYY-MM" strings.</summary>
public static partial class Month
{
    private static readonly string[] ShortNames = ["jan", "feb", "mar", "apr", "maj", "jun", "jul", "avg", "sep", "okt", "nov", "dec"];

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex Pattern();

    public static bool IsValid(string month) => Pattern().IsMatch(month);

    public static string Next(string month)
    {
        var (year, m) = Split(month);
        return m == 12 ? $"{year + 1:0000}-01" : $"{year:0000}-{m + 1:00}";
    }

    /// <summary>"2027-03" → "mar 27".</summary>
    public static string Label(string month)
    {
        var (year, m) = Split(month);
        return $"{ShortNames[m - 1]} {year % 100:00}";
    }

    private static (int Year, int Month) Split(string month) => (int.Parse(month[..4]), int.Parse(month[5..]));
}
