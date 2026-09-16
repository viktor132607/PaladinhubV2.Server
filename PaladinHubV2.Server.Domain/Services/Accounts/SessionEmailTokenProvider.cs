using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Accounts;

// Bind email login codes to the pending browser login, so a code from a
// previous login or another browser cannot complete this challenge.
public sealed class SessionEmailTokenProvider(IHttpContextAccessor http) : EmailTokenProvider<User>
{
    public const string NonceKey = "PaladinHub.EmailLoginNonce";
    public override async Task<string> GetUserModifierAsync(string purpose, UserManager<User> manager, User user) =>
        await base.GetUserModifierAsync(purpose, manager, user) + ":" + http.HttpContext?.Session.GetString(NonceKey);

    public override Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user) =>
        string.IsNullOrWhiteSpace(http.HttpContext?.Session.GetString(NonceKey))
            ? Task.FromResult(false)
            : base.ValidateAsync(purpose, token, manager, user);
}
