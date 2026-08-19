using System.Security.Claims;
using PaladinHubV2.Common.Responses.Auth;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Auth
{
	public sealed partial class AuthService
	{
		public async Task<AuthOperationResult> GetCurrentUserAsync(ClaimsPrincipal principal)
		{
			if (principal.Identity?.IsAuthenticated != true)
				return Ok(AuthSessionResponse.Anonymous);

			User? user = await _userManager.GetUserAsync(principal);
			if (user is null)
			{
				await _signInManager.SignOutAsync();
				return Ok(AuthSessionResponse.Anonymous);
			}

			return Ok(await CreateSessionAsync(user));
		}

		public async Task<AuthOperationResult> LogoutAsync()
		{
			await _signInManager.SignOutAsync();
			return Ok(AuthSessionResponse.Anonymous);
		}
	}
}
