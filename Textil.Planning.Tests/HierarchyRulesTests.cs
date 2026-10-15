using Textil.Planning.Domain;

namespace Textil.Planning.Tests;

public class HierarchyRulesTests
{
    private static UserNode Node(string id, string? managerId, params string[] roles) =>
        new(id, id, roles.ToHashSet(), managerId);

    private static readonly UserNode Pop = Node("pop", null, Roles.Director);
    private static readonly UserNode Milan = Node("milan", "pop", Roles.Sales, Roles.TeamLead);
    private static readonly UserNode Tijana = Node("tijana", "milan", Roles.Sales);
    private static readonly UserNode Mikula = Node("mikula", null, Roles.Procurement);
    private static readonly UserNode[] All = [Pop, Milan, Tijana, Mikula];

    [Fact]
    public void Seeded_hierarchy_is_valid()
    {
        foreach (var user in All)
            Assert.Empty(HierarchyRules.Validate(user, All));
    }

    [Fact]
    public void Sales_reports_to_team_lead()
    {
        Assert.Empty(HierarchyRules.Validate(Node("new", "milan", Roles.Sales), All));
        Assert.NotEmpty(HierarchyRules.Validate(Node("new", "pop", Roles.Sales), All));
        Assert.NotEmpty(HierarchyRules.Validate(Node("new", "tijana", Roles.Sales), All));
    }

    [Fact]
    public void Sales_without_manager_is_rejected()
    {
        Assert.NotEmpty(HierarchyRules.Validate(Node("new", null, Roles.Sales), All));
    }

    [Fact]
    public void Team_lead_reports_to_director()
    {
        Assert.Empty(HierarchyRules.Validate(Node("new", "pop", Roles.Sales, Roles.TeamLead), All));
        Assert.NotEmpty(HierarchyRules.Validate(Node("new", "milan", Roles.Sales, Roles.TeamLead), All));
    }

    [Fact]
    public void Team_lead_must_also_be_sales()
    {
        Assert.NotEmpty(HierarchyRules.Validate(Node("new", "pop", Roles.TeamLead), All));
    }

    [Theory]
    [InlineData(Roles.Director)]
    [InlineData(Roles.Procurement)]
    public void Director_and_procurement_have_no_manager(string role)
    {
        Assert.Empty(HierarchyRules.Validate(Node("new", null, role), All));
        Assert.NotEmpty(HierarchyRules.Validate(Node("new", "pop", role), All));
    }

    [Fact]
    public void Director_cannot_be_sales()
    {
        Assert.NotEmpty(HierarchyRules.Validate(Node("new", null, Roles.Director, Roles.Sales), All));
    }

    [Fact]
    public void User_without_roles_is_rejected()
    {
        Assert.NotEmpty(HierarchyRules.Validate(Node("new", null), All));
    }

    [Fact]
    public void Unknown_role_is_rejected()
    {
        Assert.NotEmpty(HierarchyRules.Validate(Node("new", null, "Admin"), All));
    }

    [Fact]
    public void Self_as_manager_is_rejected()
    {
        Assert.NotEmpty(HierarchyRules.Validate(Milan with { ManagerId = "milan" }, All));
    }

    [Fact]
    public void Missing_manager_is_rejected()
    {
        Assert.NotEmpty(HierarchyRules.Validate(Node("new", "nobody", Roles.Sales), All));
    }

    [Fact]
    public void Team_lead_role_cannot_be_removed_while_team_reports_to_them()
    {
        var errors = HierarchyRules.Validate(Node("milan", "pop", Roles.Sales), All);
        Assert.Contains(errors, e => e.Contains("tijana"));
    }

    [Fact]
    public void Director_role_cannot_be_removed_while_team_leads_report_to_them()
    {
        var errors = HierarchyRules.Validate(Node("pop", null, Roles.Procurement), All);
        Assert.Contains(errors, e => e.Contains("milan"));
    }
}
