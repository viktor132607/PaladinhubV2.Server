using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Transactions;

namespace PaladinHubV2.Server.API.Controllers.Accounts
{
	[ApiController]
	[Authorize]
	[Route("api/account")]
	[Route("Account")]
	public sealed class TransactionsController : ControllerBase
	{
		private const int DefaultPageSize = 10;
		private const string DefaultRegion = "Europe";

		private readonly ITransactionsService _transactionsService;
		private readonly IAccountUiService _accountUiService;

		public TransactionsController(
			ITransactionsService transactionsService,
			IAccountUiService accountUiService)
		{
			_transactionsService = transactionsService;
			_accountUiService = accountUiService;
		}

		[HttpGet("TransactionHistory")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public Task<IActionResult> TransactionHistory(
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = DefaultPageSize,
			[FromQuery] string region = DefaultRegion)
		{
			return GetHistory(
				page,
				pageSize,
				region);
		}

		[HttpGet("Transactions")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public Task<IActionResult> Transactions(
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = DefaultPageSize,
			[FromQuery] string region = DefaultRegion)
		{
			return GetHistory(
				page,
				pageSize,
				region);
		}

		private async Task<IActionResult> GetHistory(
			int page,
			int pageSize,
			string region)
		{
			string? userId =
				_accountUiService.GetUserId(User);

			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			var history =
				await _transactionsService.GetHistoryForRequest(
					userId,
					region,
					page,
					pageSize);

			return Ok(history);
		}
	}
}
