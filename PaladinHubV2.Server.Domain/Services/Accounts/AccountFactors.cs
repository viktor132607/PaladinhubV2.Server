using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts;

public static class AccountFactors
{
    public const string Store = "PaladinHub.Security";
    public static async Task<bool> EmailAsync(UserManager<User> users, User user) =>
        user.EmailConfirmed && await users.GetAuthenticationTokenAsync(user, Store, "Email2FA") == "true";
    public static async Task<bool> AuthenticatorAsync(UserManager<User> users, User user)
    {
        string? state = await users.GetAuthenticationTokenAsync(user, Store, "Authenticator2FA");
        return user.TwoFactorEnabled && (state == "true" || state is null);
    }
    public static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
