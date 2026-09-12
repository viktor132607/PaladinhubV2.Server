using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Auth;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts;

[ApiController]
[Route("api/auth")]
public sealed class AuthMultiFactorController : ControllerBase
{
	private readonly AuthLoginService _login;

	public AuthMultiFactorController(AuthLoginService login)
	{
		_login = login;
	}

	[AllowAnonymous]
	[HttpPost("2fa")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> LoginWithTwoFactor(
		[FromBody] TwoFactorLoginRequest request)
	{
		AuthLoginResult result = await _login.TwoFactorAsync(
			request.Code,
			request.RememberMe,
			request.RememberMachine);

		return result.Error switch
		{
			AuthLoginError.None => Ok(result.Session),
			AuthLoginError.TwoFactorSessionExpired =>
				Unauthorized(new AuthErrorResponse(
					"The two-factor login session has expired.")),
			AuthLoginError.InvalidTwoFactorCodeFormat =>
				BadRequest(new AuthErrorResponse(
					"Enter a valid 6-digit authenticator code.")),
			AuthLoginError.LockedOut => StatusCode(
				StatusCodes.Status423Locked,
				new AuthErrorResponse(
					"Your account is temporarily locked.")),
			_ => Unauthorized(new AuthErrorResponse(
				"Invalid authenticator code."))
		};
	}

	[AllowAnonymous]
	[HttpPost("recovery-code")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> LoginWithRecoveryCode(
		[FromBody] RecoveryCodeLoginRequest request)
	{
		AuthLoginResult result =
			await _login.RecoveryCodeAsync(request.RecoveryCode);

		return result.Error switch
		{
			AuthLoginError.None => Ok(result.Session),
			AuthLoginError.RecoverySessionExpired =>
				Unauthorized(new AuthErrorResponse(
					"The recovery-code login session has expired.")),
			AuthLoginError.RecoveryCodeRequired =>
				BadRequest(new AuthErrorResponse(
					"Recovery code is required.")),
			AuthLoginError.LockedOut => StatusCode(
				StatusCodes.Status423Locked,
				new AuthErrorResponse(
					"Your account is temporarily locked.")),
			_ => Unauthorized(new AuthErrorResponse(
				"Invalid recovery code."))
		};
	}
}
