using System.Globalization;
using ClosedXML.Excel;
using Textil.Planning.Data;

namespace Textil.Planning.Domain;

/// <summary>SKU codes in file order (duplicates included) and the months from the header.</summary>
public sealed record SkuFile(IReadOnlyList<string> Months, IReadOnlyList<string> Codes);

/// <summary>
/// Reads the procurement xlsx: first sheet, row 1 = "SKU | YYYY-MM | YYYY-MM | ...", then one code per row in
/// column A. Month cells below the header are ignored.
/// </summary>
public static class SkuFileParser
{
    private const int MaxReportedErrors = 20;

    public static SkuFile Parse(Stream xlsx)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(xlsx);
        }
        catch (Exception)
        {
            throw new DomainException("Fajl nije moguće pročitati. Očekuje se Excel fajl u formatu .xlsx.");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.First();
            var lastColumn = sheet.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
            if (lastColumn == 0 && lastRow == 0)
                throw new DomainException("Fajl je prazan.");

            var errors = new List<string>();
            var months = ReadMonths(sheet, lastColumn, errors);
            var codes = ReadCodes(sheet, lastRow, errors);
            if (errors.Count == 0 && codes.Count == 0)
                errors.Add("Fajl ne sadrži nijednu šifru.");

            if (errors.Count > MaxReportedErrors)
                errors = [.. errors.Take(MaxReportedErrors), $"… i još {errors.Count - MaxReportedErrors} grešaka."];
            if (errors.Count > 0)
                throw new DomainException(errors);
            return new SkuFile(months, codes);
        }
    }

    private static List<string> ReadMonths(IXLWorksheet sheet, int lastColumn, List<string> errors)
    {
        if (!string.Equals(Text(sheet.Cell(1, 1)), "SKU", StringComparison.OrdinalIgnoreCase))
            errors.Add("Prva ćelija zaglavlja (A1) mora biti \"SKU\".");

        var months = new List<string>();
        var headerValid = true;
        for (var column = 2; column <= lastColumn; column++)
        {
            var cell = sheet.Cell(1, column);
            // Excel turns a typed "2027-01" into a date; accept it as that month.
            var text = cell.Value.IsDateTime ? cell.Value.GetDateTime().ToString("yyyy-MM", CultureInfo.InvariantCulture) : Text(cell);
            if (text.Length == 0)
                errors.Add($"Prazna ćelija u zaglavlju ({cell.Address}).");
            else if (!Month.IsValid(text))
                errors.Add($"Zaglavlje {cell.Address}: \"{text}\" nije mesec u formatu YYYY-MM.");
            else
            {
                months.Add(text);
                continue;
            }
            headerValid = false;
        }

        if (!headerValid)
            return months;
        if (months.Count == 0)
            errors.Add("Zaglavlje ne sadrži nijedan mesec (kolone posle SKU, u formatu YYYY-MM).");
        for (var i = 1; i < months.Count; i++)
        {
            var expected = Month.Next(months[i - 1]);
            if (months[i] != expected)
            {
                errors.Add($"Meseci u zaglavlju moraju biti hronološki i bez rupa: posle {months[i - 1]} očekuje se {expected}, a u fajlu je {months[i]}.");
                break;
            }
        }
        return months;
    }

    private static List<string> ReadCodes(IXLWorksheet sheet, int lastRow, List<string> errors)
    {
        var codes = new List<string>();
        for (var row = 2; row <= lastRow; row++)
        {
            var code = Text(sheet.Cell(row, 1));
            if (code.Length == 0)
            {
                if (!sheet.Row(row).IsEmpty())
                    errors.Add($"Red {row}: šifra je prazna.");
                continue;
            }
            if (code.Length > CycleSku.CodeMaxLength)
                errors.Add($"Red {row}: šifra je duža od {CycleSku.CodeMaxLength} znakova.");
            else
                codes.Add(code);
        }
        return codes;
    }

    private static string Text(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank)
            return "";
        if (value.IsNumber)
            return value.GetNumber().ToString(CultureInfo.InvariantCulture);
        return cell.GetFormattedString().Trim();
    }
}
