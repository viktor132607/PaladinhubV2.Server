using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/cart")]
	[Route("Cart")]
	[AutoValidateAntiforgeryToken]
	public sealed class CartsController : ControllerBase
	{
		private readonly ICartApplicationService _cart;
		private readonly UserManager<User> _userManager;

		public CartsController(
			ICartApplicationService cart,
			UserManager<User> userManager)
		{
			_cart = cart;
			_userManager = userManager;
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

			return Ok(await _cart.GetCartAsync(
				user,
				cancellationToken));
		}

		[AllowAnonymous]
		[HttpGet("Mini")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Mini(
			CancellationToken cancellationToken)
		{
			User? user = await CurrentUserAsync();

			return Ok(await _cart.GetMiniCartAsync(
				user,
				cancellationToken));
		}

		[AllowAnonymous]
		[HttpGet("CountJson")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> CountJson(
			CancellationToken cancellationToken)
		{
			int count = await _cart.GetCountAsync(
				OwnerKey(),
				cancellationToken);

			return Ok(count);
		}

		private Task<User?> CurrentUserAsync()
		{
			return _userManager.GetUserAsync(User);
		}

		private string OwnerKey()
		{
			return User.FindFirstValue(
					ClaimTypes.NameIdentifier) ??
				$"anon:{HttpContext.Session.Id}";
		}
	}
}
