using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Account;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountController : ControllerBase
	{
		private readonly IAccountOverviewService _overview;

		public AccountController(IAccountOverviewService overview)
		{
			_overview = overview;
		}

		[HttpGet("MyAccount")]
		public async Task<IActionResult> MyAccount(
			CancellationToken cancellationToken)
		{
			MyAccountViewModel? model =
				await _overview.GetMyAccountAsync(
					User,
					cancellationToken);

			return model == null
				? Unauthorized(new
				{
					message = "Authentication required."
				})
				: Ok(model);
		}

		[HttpGet("Overview")]
		public async Task<IActionResult> Overview(
			[FromQuery] int page = 1,
			CancellationToken cancellationToken = default)
		{
			MyAccountViewModel? model =
				await _overview.GetOverviewAsync(
					User,
					page,
					cancellationToken);

			return model == null
				? Unauthorized(new
				{
					message = "Authentication required."
				})
				: Ok(model);
		}
	}
}
