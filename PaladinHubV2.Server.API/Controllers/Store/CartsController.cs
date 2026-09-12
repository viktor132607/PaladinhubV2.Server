using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Carts;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/cart")]
	[Route("Cart")]
	[AutoValidateAntiforgeryToken]
	public sealed class CartsController : ControllerBase
	{
		private readonly IProductService _productService;
		private readonly UserManager<User> _userManager;
		private readonly ICartSessionService _cartSession;
		private readonly CartFlowService _cartFlow;

		public CartsController(
			IProductService productService,
			UserManager<User> userManager,
			ICartSessionService cartSession)
		{
			_productService = productService;
			_userManager = userManager;
			_cartSession = cartSession;
			_cartFlow = new CartFlowService(
				cartSession,
				productService);
		}

		[HttpGet("my-cart")]
		[HttpGet("MyCart")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> MyCart(
			CancellationToken cancellationToken)
		{
			User? user = await CurrentUserAsync();

			if (user == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			MyCartViewModel model =
				await _cartFlow.GetCartViewModelAsync(
					user,
					cancellationToken);

			return Ok(model);
		}

		[AllowAnonymous]
		[HttpGet("Mini")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Mini()
		{
			User? user = await CurrentUserAsync();

			if (user == null)
			{
				return Ok(new MyCartViewModel
				{
					TotalPrice = 0m
				});
			}

			MyCartViewModel model =
				await _productService.GetMyProducts(user);

			return Ok(model);
		}

		[AllowAnonymous]
		[HttpGet("CountJson")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> CountJson(
			CancellationToken cancellationToken)
		{
			int count = await _cartSession.GetCount(
				OwnerKey(),
				cancellationToken);

			return Ok(count);
		}

		private string? CurrentUserId()
		{
			return User.FindFirstValue(
				ClaimTypes.NameIdentifier);
		}

		private Task<User?> CurrentUserAsync()
		{
			return _userManager.GetUserAsync(User);
		}

		private string OwnerKey()
		{
			return CurrentUserId() ??
				$"anon:{HttpContext.Session.Id}";
		}
	}
}
