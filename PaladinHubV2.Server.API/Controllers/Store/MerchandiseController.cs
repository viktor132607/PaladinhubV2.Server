using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[AllowAnonymous]
	[Route("api/merchandise")]
	[Route("Merchandise")]
	public sealed class MerchandiseController : ControllerBase
	{
		private readonly IMerchandiseService _merchandise;

		public MerchandiseController(
			IMerchandiseService merchandise)
		{
			_merchandise = merchandise;
		}

		[HttpGet]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public Task<IActionResult> Index(
			[FromQuery] ProductQueryOptions options,
			CancellationToken cancellationToken = default)
		{
			return GetMerchandiseAsync(
				options,
				cancellationToken);
		}

		[HttpGet("List")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public Task<IActionResult> Merchandise(
			[FromQuery] ProductQueryOptions options,
			CancellationToken cancellationToken = default)
		{
			return GetMerchandiseAsync(
				options,
				cancellationToken);
		}

		private async Task<IActionResult> GetMerchandiseAsync(
			ProductQueryOptions options,
			CancellationToken cancellationToken)
		{
			MerchandisePageViewModel model =
				await _merchandise.GetPageAsync(
					options,
					cancellationToken);

			return Ok(model);
		}
	}
}
