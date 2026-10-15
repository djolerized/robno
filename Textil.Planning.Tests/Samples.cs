using ClosedXML.Excel;

namespace Textil.Planning.Tests;

/// <summary>
/// The procurement sample (doc/primer-EXPORT_nabavka-salje-prodaji.xlsx): sheet "Plan", header SKU + 2027-01..2027-06,
/// eight codes.
/// </summary>
public static class Samples
{
    public static readonly string[] Months = ["2027-01", "2027-02", "2027-03", "2027-04", "2027-05", "2027-06"];

    public static readonly string[] Codes =
        ["TX-10231", "TX-10232", "TX-10555", "TX-11002", "TX-11310", "TX-12001", "TX-12002", "TX-12088"];

    public static byte[] SkuUpload() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", "sku-upload.xlsx"));

    /// <summary>The sample with the first sheet changed by <paramref name="change"/>.</summary>
    public static byte[] SkuUpload(Action<IXLWorksheet> change)
    {
        using var workbook = new XLWorkbook(new MemoryStream(SkuUpload()));
        change(workbook.Worksheet(1));
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }
}
