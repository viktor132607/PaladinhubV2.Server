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
	public sealed class CartLifecycleController : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		private readonly ICartSessionService _cartSession;

		public CartLifecycleController(
			UserManager<User> userManager,
			ICartSessionService cartSession)
		{
			_userManager = userManager;
			_cartSession = cartSession;
		}

		[HttpPost("Cancel")]
		public async Task<IActionResult> Cancel(
			CancellationToken cancellationToken)
		{
			User? user = await _userManager.GetUserAsync(User);

			if (user == null)
			{
				return Unauthorized(new
				{
					ok = false,
					message = "Authentication required."
				});
			}

			await _cartSession.CleanAndClear(
				user,
				cancellationToken);

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
