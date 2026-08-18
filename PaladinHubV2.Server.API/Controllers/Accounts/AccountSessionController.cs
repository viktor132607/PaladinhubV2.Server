using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountSessionController : ControllerBase
	{
		private readonly ISecurityService _security;

		public AccountSessionController(ISecurityService security)
		{
			_security = security;
		}

		[HttpPost("Logout")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Logout()
		{
			await _security.LogoutCurrentSession();
			return Ok(new { ok = true });
		}
	}
}
