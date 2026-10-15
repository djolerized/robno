namespace Textil.Planning.Domain;

public sealed record UserNode(string Id, string DisplayName, IReadOnlySet<string> Roles, string? ManagerId);

/// <summary>
/// Role combinations and the approval hierarchy: Sales reports to a TeamLead, a TeamLead reports to the Director,
/// Director and Procurement have no manager.
/// </summary>
public static class HierarchyRules
{
    public static string? RequiredManagerRole(IReadOnlySet<string> roles) =>
        roles.Contains(Roles.TeamLead) ? Roles.Director
        : roles.Contains(Roles.Sales) ? Roles.TeamLead
        : null;

    /// <summary>Validates <paramref name="candidate"/> against all other users. Returns an empty list when valid.</summary>
    public static IReadOnlyList<string> Validate(UserNode candidate, IEnumerable<UserNode> allUsers)
    {
        var errors = new List<string>();
        var roles = candidate.Roles;
        var others = allUsers.Where(u => u.Id != candidate.Id).ToDictionary(u => u.Id);

        if (roles.Count == 0)
            errors.Add("Korisnik mora imati bar jednu ulogu.");
        foreach (var unknown in roles.Except(Roles.All))
            errors.Add($"Nepoznata uloga: {unknown}.");
        if (roles.Contains(Roles.TeamLead) && !roles.Contains(Roles.Sales))
            errors.Add("Vođa prodaje mora imati i ulogu komercijaliste, jer pravi sopstveni plan.");
        if (roles.Contains(Roles.Director) && roles.Contains(Roles.Sales))
            errors.Add("Direktor prodaje ne može istovremeno biti komercijalista ili vođa prodaje.");

        var requiredManagerRole = RequiredManagerRole(roles);
        if (candidate.ManagerId is null)
        {
            if (requiredManagerRole is not null)
                errors.Add($"Nadređeni je obavezan ({Roles.Label(requiredManagerRole).ToLowerInvariant()}).");
        }
        else if (candidate.ManagerId == candidate.Id)
            errors.Add("Korisnik ne može biti sam sebi nadređeni.");
        else if (requiredManagerRole is null)
            errors.Add("Nadređenog imaju samo komercijalisti i vođe prodaje.");
        else if (!others.TryGetValue(candidate.ManagerId, out var manager))
            errors.Add("Izabrani nadređeni ne postoji.");
        else if (!manager.Roles.Contains(requiredManagerRole))
            errors.Add($"Nadređeni mora imati ulogu: {Roles.Label(requiredManagerRole).ToLowerInvariant()}.");

        foreach (var subordinate in others.Values.Where(u => u.ManagerId == candidate.Id).OrderBy(u => u.DisplayName))
        {
            var needed = RequiredManagerRole(subordinate.Roles);
            if (needed is null || !roles.Contains(needed))
                errors.Add($"{subordinate.DisplayName} je podređen ovom korisniku — prvo mu promenite nadređenog.");
        }

        return errors;
    }
}
