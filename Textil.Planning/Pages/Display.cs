using Textil.Planning.Data;
using Textil.Planning.Domain;

namespace Textil.Planning.Pages;

/// <summary>UI labels shared by pages.</summary>
public static class Display
{
    public static string Label(CycleStatus status) => status switch
    {
        CycleStatus.Open => "Otvoren",
        CycleStatus.Locked => "Zaključan",
        CycleStatus.Finalized => "Finalizovan",
        _ => status.ToString(),
    };

    public static string PillClass(CycleStatus status) => status switch
    {
        CycleStatus.Open => "open",
        CycleStatus.Locked => "locked",
        CycleStatus.Finalized => "final",
        _ => "",
    };

    public static string Period(IReadOnlyList<string> months) =>
        months.Count == 0 ? "određuje prvo slanje" : $"{Month.Label(months[0])} – {Month.Label(months[^1])}";

    /// <summary>Stores a rule violation for the next request; rendered as a list by _StatusMessage.</summary>
    public static void SetErrors(this Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary tempData, DomainException ex) =>
        tempData["Error"] = string.Join("\n", ex.Errors);
}
