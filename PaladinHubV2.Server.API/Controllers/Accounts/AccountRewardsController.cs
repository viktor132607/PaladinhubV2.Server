using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.Accounts;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class AccountRewardsController : ControllerBase
	{
		private readonly IAccountRewardsService _rewards;

		public AccountRewardsController(
			IAccountRewardsService rewards)
		{
			_rewards = rewards;
		}

		[HttpPost("RedeemCode")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> RedeemCode(
			[FromForm] string code)
		{
			AccountRedeemCodeResult? result =
				await _rewards.RedeemCodeAsync(User, code);

			if (result == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			var response = new
			{
				ok = result.Ok,
				reason = result.Reason,
				message = result.Message,
				amount = result.Amount,
				currency = result.Currency,
				percent = result.Percent
			};

			if (result.Ok)
			{
				return Ok(response);
			}

			return result.Reason == "already-used"
				? Conflict(response)
				: BadRequest(response);
		}

		[HttpPost("DevTopUp")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DevTopUp(
			[FromForm] decimal amount)
		{
			AccountTopUpResult? result =
				await _rewards.TopUpAsync(User, amount);

			if (result == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			if (!result.Ok)
			{
				return BadRequest(new
				{
					message = result.Message
				});
			}

			return Ok(new
			{
				ok = true,
				transactionId = result.TransactionId,
				amount = result.Amount,
				balance = result.Balance,
				currency = result.Currency
			});
		}
	}
}
