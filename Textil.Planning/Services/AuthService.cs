using Microsoft.AspNetCore.Identity;
using Textil.Planning.Data;

namespace Textil.Planning.Services;

public enum SignInOutcome { Success, Failed, LockedOut }

/// <summary>
/// The single entry point for signing in and out. Local Identity accounts for now; B2B sign-in (V2) replaces this
/// implementation.
/// </summary>
public interface IAuthService
{
    Task<SignInOutcome> SignInAsync(string login, string password, bool rememberMe);
    Task SignOutAsync();
}

public class IdentityAuthService(SignInManager<User> signInManager, UserManager<User> userManager) : IAuthService
{
    public async Task<SignInOutcome> SignInAsync(string login, string password, bool rememberMe)
    {
        login = login.Trim();
        var user = login.Contains('@')
            ? await userManager.FindByEmailAsync(login)
            : await userManager.FindByNameAsync(login);
        if (user is null)
            return SignInOutcome.Failed;

        var result = await signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
        return result.Succeeded ? SignInOutcome.Success
            : result.IsLockedOut ? SignInOutcome.LockedOut
            : SignInOutcome.Failed;
    }

    public Task SignOutAsync() => signInManager.SignOutAsync();
}
