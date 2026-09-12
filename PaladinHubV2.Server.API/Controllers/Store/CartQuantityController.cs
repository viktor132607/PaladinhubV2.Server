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
	public sealed class CartQuantityController : CartControllerBase
	{
		private readonly ICartSessionService _cartSession;

		public CartQuantityController(
			UserManager<User> userManager,
			ICartSessionService cartSession,
			CartFlowService cartFlow)
			: base(userManager, cartFlow)
		{
			_cartSession = cartSession;
		}

		[AllowAnonymous]
		[HttpPost("Increase")]
		public async Task<IActionResult> Increase(
			[FromQuery] string id,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return CartError("Product ID is required.");
			}

			string productId = id.Trim();
			bool updated = await _cartSession.IncreaseProduct(
				productId,
				OwnerKey(),
				cancellationToken);

			if (!updated)
			{
				return CartError(
					"The product quantity could not be increased.");
			}

			return await CartDeltaAsync(
				productId,
				removed: false,
				cancellationToken);
		}

		[AllowAnonymous]
		[HttpPost("Decrease")]
		public async Task<IActionResult> Decrease(
			[FromQuery] string id,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return CartError("Product ID is required.");
			}

			string productId = id.Trim();
			bool updated = await _cartSession.DecreaseProduct(
				productId,
				OwnerKey(),
				cancellationToken);

			if (!updated)
			{
				return CartError(
					"The product quantity could not be decreased.");
			}

			return await CartDeltaAsync(
				productId,
				removed: null,
				cancellationToken);
		}
	}
}
