using System.Security.Claims;

namespace Textil.Planning.Services;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Id of the signed-in user; throws when nobody is signed in.</summary>
    string Id { get; }

    string DisplayName { get; }
    bool IsInRole(string role);
}

public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string Id => Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No signed-in user.");

    public string DisplayName => Principal?.FindFirstValue(AppClaimTypes.DisplayName) ?? Principal?.Identity?.Name ?? "";

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
