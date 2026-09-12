using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Route("api/merchandise")]
	[Route("Merchandise")]
	public sealed class MerchandiseController : ControllerBase
	{
		private readonly MerchandiseService _merchandise;
		private readonly IProductService _productService;

		public MerchandiseController(
			IProductService productService,
			AppDbContext db)
		{
			_productService = productService;
			_merchandise = new MerchandiseService(
				productService,
				db);
		}

		[AllowAnonymous]
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

		[AllowAnonymous]
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

		[AllowAnonymous]
		[HttpGet("~/Home/Merchandise")]
		public async Task<IActionResult> LegacyMerchandise()
		{
			ICollection<ProductViewModel> products =
				await _productService.GetAll();

			return Ok(products);
		}

		[Authorize]
		[HttpGet("~/Home/IndexLoggedIn")]
		public async Task<IActionResult> IndexLoggedIn()
		{
			ICollection<ProductViewModel> products =
				await _productService.GetAll();

			return Ok(products);
		}
	}
}
