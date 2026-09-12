using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Auth;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts;

[ApiController]
[Route("api/auth")]
public sealed class AuthApiController : ControllerBase
{
	private readonly IAntiforgery _antiforgery;
	private readonly SignInManager<User> _signInManager;
	private readonly UserManager<User> _userManager;
	private readonly AuthSessionService _sessionService;

	public AuthApiController(
		IAntiforgery antiforgery,
		SignInManager<User> signInManager,
		UserManager<User> userManager)
	{
		_antiforgery = antiforgery;
		_signInManager = signInManager;
		_userManager = userManager;
		_sessionService = new AuthSessionService(userManager);
	}

	[AllowAnonymous]
	[HttpGet("csrf")]
	[ResponseCache(
		NoStore = true,
		Location = ResponseCacheLocation.None)]
	public IActionResult GetCsrfToken()
	{
		AntiforgeryTokenSet tokens =
			_antiforgery.GetAndStoreTokens(HttpContext);

		return Ok(new
		{
			token = tokens.RequestToken
		});
	}

	[AllowAnonymous]
	[HttpGet("me")]
	[ResponseCache(
		NoStore = true,
		Location = ResponseCacheLocation.None)]
	public async Task<IActionResult> GetCurrentUser()
	{
		if (User.Identity?.IsAuthenticated != true)
		{
			return Ok(AuthSessionResponse.Anonymous);
		}

		User? user = await _userManager.GetUserAsync(User);

		if (user is null)
		{
			await _signInManager.SignOutAsync();
			return Ok(AuthSessionResponse.Anonymous);
		}

		return Ok(await _sessionService.CreateAsync(user));
	}
}
