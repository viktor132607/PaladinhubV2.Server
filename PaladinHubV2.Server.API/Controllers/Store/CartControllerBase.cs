using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Carts;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	public abstract class CartControllerBase : ControllerBase
	{
		private readonly UserManager<User> _userManager;
		protected readonly CartFlowService CartFlow;

		protected CartControllerBase(
			UserManager<User> userManager,
			CartFlowService cartFlow)
		{
			_userManager = userManager;
			CartFlow = cartFlow;
		}

		protected Task<User?> CurrentUserAsync()
		{
			return _userManager.GetUserAsync(User);
		}

		protected string OwnerKey()
		{
			string? userId =
				User.FindFirstValue(ClaimTypes.NameIdentifier);

			return userId ?? $"anon:{HttpContext.Session.Id}";
		}

		protected IActionResult CartError(string message)
		{
			return BadRequest(new
			{
				ok = false,
				message
			});
		}

		protected async Task<IActionResult> CartDeltaAsync(
			string productId,
			bool? removed,
			CancellationToken cancellationToken)
		{
			User? user = await CurrentUserAsync();
			CartDeltaResult delta = await CartFlow.GetDeltaAsync(
				user,
				OwnerKey(),
				productId,
				removed,
				cancellationToken);

			if (delta.IsAnonymous)
			{
				return Ok(new
				{
					ok = true,
					productId = delta.ProductId,
					removed = delta.Removed,
					cartCount = delta.CartCount
				});
			}

			return Ok(new
			{
				ok = true,
				productId = delta.ProductId,
				removed = delta.Removed,
				quantity = delta.Quantity,
				unitPrice = delta.UnitPrice,
				lineTotal = delta.LineTotal,
				cartTotal = delta.CartTotal
			});
		}
	}
}
