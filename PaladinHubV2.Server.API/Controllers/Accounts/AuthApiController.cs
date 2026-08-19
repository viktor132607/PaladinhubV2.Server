using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Common.Requests.Auth;
using PaladinHubV2.Server.Domain.Services.Auth;

namespace PaladinHubV2.Server.API.Controllers.Accounts;

[ApiController]
[Route("api/auth")]
public sealed class AuthApiController : ControllerBase
{
	private readonly IAntiforgery _antiforgery;
	private readonly IAuthService _auth;

	public AuthApiController(
		IAntiforgery antiforgery,
		IAuthService auth)
	{
		_antiforgery = antiforgery;
		_auth = auth;
	}

	[AllowAnonymous]
	[HttpGet("csrf")]
	[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
	public IActionResult GetCsrfToken()
	{
		AntiforgeryTokenSet tokens = _antiforgery.GetAndStoreTokens(HttpContext);
		return Ok(new { token = tokens.RequestToken });
	}

	[AllowAnonymous]
	[HttpGet("me")]
	[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
	public async Task<IActionResult> GetCurrentUser()
		=> ToActionResult(await _auth.GetCurrentUserAsync(User));

	[AllowAnonymous]
	[HttpPost("register")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Register([FromBody] RegisterRequest request)
		=> ToActionResult(await _auth.RegisterAsync(request));

	[AllowAnonymous]
	[HttpPost("login")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Login([FromBody] LoginRequest request)
		=> ToActionResult(await _auth.LoginAsync(request));

	[AllowAnonymous]
	[HttpPost("2fa")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> LoginWithTwoFactor(
		[FromBody] TwoFactorLoginRequest request)
		=> ToActionResult(await _auth.LoginWithTwoFactorAsync(request));

	[AllowAnonymous]
	[HttpPost("recovery-code")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> LoginWithRecoveryCode(
		[FromBody] RecoveryCodeLoginRequest request)
		=> ToActionResult(await _auth.LoginWithRecoveryCodeAsync(request));

	[Authorize]
	[HttpPost("change-password")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> ChangePassword(
		[FromBody] ChangePasswordRequest request)
		=> ToActionResult(await _auth.ChangePasswordAsync(User, request));

	[Authorize]
	[HttpPost("logout")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Logout()
		=> ToActionResult(await _auth.LogoutAsync());

	private IActionResult ToActionResult(AuthOperationResult result)
	{
		return result.Status switch
		{
			AuthOperationStatus.Ok => Ok(result.Payload),
			AuthOperationStatus.Accepted => Accepted(result.Payload),
			AuthOperationStatus.BadRequest => BadRequest(result.Payload),
			AuthOperationStatus.Unauthorized => Unauthorized(result.Payload),
			AuthOperationStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result.Payload),
			AuthOperationStatus.Conflict => Conflict(result.Payload),
			AuthOperationStatus.Locked => StatusCode(StatusCodes.Status423Locked, result.Payload),
			AuthOperationStatus.InternalError => StatusCode(StatusCodes.Status500InternalServerError, result.Payload),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result.Payload)
		};
	}
}
