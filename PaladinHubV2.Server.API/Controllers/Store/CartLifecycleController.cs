using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services;
using PaladinHubV2.Server.Domain.Services.Carts;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/cart")]
	[Route("Cart")]
	[AutoValidateAntiforgeryToken]
	public sealed class CartLifecycleController : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		private readonly ICartSessionService _cartSession;
		private readonly ICartStore _cartStore;

		public CartLifecycleController(
			UserManager<User> userManager,
			ICartSessionService cartSession,
			ICartStore cartStore)
		{
			_userManager = userManager;
			_cartSession = cartSession;
			_cartStore = cartStore;
		}

		[AllowAnonymous]
		[HttpPost("Cancel")]
		public async Task<IActionResult> Cancel(
			CancellationToken cancellationToken)
		{
			User? user =
				await CheckoutGuestUserResolver.ResolveExistingAsync(
					HttpContext,
					User,
					_userManager);

			if (user != null)
			{
				await _cartSession.CleanAndClear(
					user,
					cancellationToken);
			}
			else
			{
				await _cartStore.ClearAsync(
					CheckoutGuestUserResolver.GetOwnerKey(
						HttpContext,
						User),
					cancellationToken);
			}

			return Ok(new
			{
				ok = true,
				cleared = true,
				cartTotal = 0m,
				message = "Cart was cleared."
			});
		}
	}
}
