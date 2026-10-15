namespace Textil.Planning.Domain;

public static class Roles
{
    public const string Procurement = "Procurement";
    public const string Sales = "Sales";
    public const string TeamLead = "TeamLead";
    public const string Director = "Director";

    public static readonly IReadOnlyList<string> All = [Procurement, Sales, TeamLead, Director];

    public static string Label(string role) => role switch
    {
        Procurement => "Nabavka",
        Sales => "Komercijalista",
        TeamLead => "Vođa prodaje",
        Director => "Direktor prodaje",
        _ => role,
    };
}
