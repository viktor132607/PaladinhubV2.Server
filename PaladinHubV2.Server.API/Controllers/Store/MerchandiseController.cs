using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[AllowAnonymous]
	[Route("api/merchandise")]
	[Route("Merchandise")]
	public sealed class MerchandiseController : ControllerBase
	{
		private readonly MerchandiseService _merchandise;

		public MerchandiseController(
			IProductService productService,
			AppDbContext db)
		{
			_merchandise = new MerchandiseService(
				productService,
				db);
		}

		[HttpGet]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public Task<IActionResult> Index(
			[FromQuery] ProductQueryOptions options,
			CancellationToken cancellationToken = default)
		{
			return Merchandise(options, cancellationToken);
		}

		[HttpGet("List")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Merchandise(
			[FromQuery] ProductQueryOptions options,
			CancellationToken cancellationToken = default)
		{
			MerchandisePageViewModel model =
				await _merchandise.BuildPageAsync(
					options,
					cancellationToken);

			return Ok(model);
		}
	}
}
