using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Account;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountController : ControllerBase
	{
		private readonly IAccountUiService _ui;
		private readonly SignInManager<User> _signInManager;

		public AccountController(
			IAccountUiService ui,
			SignInManager<User> signInManager)
		{
			_ui = ui;
			_signInManager = signInManager;
		}

		[HttpGet("MyAccount")]
		public async Task<IActionResult> MyAccount(
			CancellationToken cancellationToken)
		{
			User? me = await _ui.GetMe(User);

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			MyAccountViewModel model =
				await _ui.BuildMyAccountAsync(
					me,
					cancellationToken);

			return Ok(model);
		}

		[HttpGet("Overview")]
		public async Task<IActionResult> Overview(
			CancellationToken cancellationToken,
			[FromQuery] int page = 1)
		{
			User? me = await _ui.GetMe(User);

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			MyAccountViewModel model =
				await _ui.BuildOverviewAsync(
					me,
					page,
					cancellationToken);

			return Ok(model);
		}

		[HttpGet("Settings")]
		public IActionResult Settings() => NoContent();

		[HttpGet("AccountDetails")]
		public IActionResult AccountDetails() => NoContent();

		[HttpGet("Privacy")]
		public IActionResult Privacy() => NoContent();

		[HttpGet("Connections")]
		public IActionResult Connections() => NoContent();

		[HttpPost("Logout")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Logout()
		{
			await _signInManager.SignOutAsync();
			return Ok(new { ok = true });
		}

		[HttpPost("MarkPhoneVerified")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> MarkPhoneVerified(
			CancellationToken cancellationToken)
		{
			User? me = await _ui.GetMe(User);

			if (me == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			await _ui.MarkPhoneVerifiedAsync(
				me,
				cancellationToken);

			return Ok(new
			{
				ok = true,
				phoneNumberConfirmed = true
			});
		}
	}
}
