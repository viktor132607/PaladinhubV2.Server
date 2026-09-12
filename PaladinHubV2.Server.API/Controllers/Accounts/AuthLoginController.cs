using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Auth;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts;

[ApiController]
[Route("api/auth")]
public sealed class AuthLoginController : ControllerBase
{
	private readonly AuthLoginService _login;

	public AuthLoginController(AuthLoginService login)
	{
		_login = login;
	}

	[AllowAnonymous]
	[HttpPost("login")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Login(
		[FromBody] LoginRequest request)
	{
		AuthLoginResult result = await _login.PasswordAsync(
			request.Identifier,
			request.Password,
			request.RememberMe);

		return result.Error switch
		{
			AuthLoginError.None => Ok(result.Session),
			AuthLoginError.RequiresTwoFactor => Accepted(new
			{
				requiresTwoFactor = true,
				rememberMe = result.RememberMe
			}),
			AuthLoginError.LockedOut => StatusCode(
				StatusCodes.Status423Locked,
				new AuthErrorResponse(
					"Your account is temporarily locked.")),
			AuthLoginError.NotAllowed => StatusCode(
				StatusCodes.Status403Forbidden,
				new AuthErrorResponse(
					"Login is not allowed for this account.")),
			_ => Unauthorized(
				new AuthErrorResponse(
					"Email/username or password is incorrect."))
		};
	}
}
