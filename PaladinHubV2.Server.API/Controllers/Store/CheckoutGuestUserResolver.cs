using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services;
using PaladinHubV2.Server.Domain.Services.Checkout;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	internal static class CheckoutGuestUserResolver
	{
		internal const string GuestUserSessionKey =
			"PaladinHub.GuestCheckoutUserId";

		internal static string GetOwnerKey(
			HttpContext httpContext,
			ClaimsPrincipal principal)
		{
			string? authenticatedUserId =
				principal.FindFirstValue(
					ClaimTypes.NameIdentifier);

			if (!string.IsNullOrWhiteSpace(
					authenticatedUserId))
			{
				return authenticatedUserId;
			}

			string? guestUserId =
				httpContext.Session.GetString(
					GuestUserSessionKey);

			if (!string.IsNullOrWhiteSpace(guestUserId))
			{
				return guestUserId;
			}

			return $"anon:{httpContext.Session.Id}";
		}

		internal static async Task<User?> ResolveExistingAsync(
			HttpContext httpContext,
			ClaimsPrincipal principal,
			UserManager<User> userManager)
		{
			User? authenticatedUser =
				await userManager.GetUserAsync(principal);

			if (authenticatedUser != null)
			{
				return authenticatedUser;
			}

			string? guestUserId =
				httpContext.Session.GetString(
					GuestUserSessionKey);

			if (string.IsNullOrWhiteSpace(guestUserId))
			{
				return null;
			}

			return await userManager.FindByIdAsync(guestUserId);
		}

		internal static async Task<User> ResolveOrCreateAsync(
			HttpContext httpContext,
			ClaimsPrincipal principal,
			UserManager<User> userManager,
			ICartStore cartStore,
			ICheckoutSessionService checkoutSession,
			CancellationToken cancellationToken)
		{
			User? existing =
				await ResolveExistingAsync(
					httpContext,
					principal,
					userManager);

			if (existing != null)
			{
				return existing;
			}

			var state = checkoutSession.GetState();
			if (state.Shipping == null)
			{
				throw new InvalidOperationException(
					"Shipping details are required before guest checkout can continue.");
			}

			string guestToken = Guid.NewGuid().ToString("N");
			var guestUser = new User
			{
				Id = Guid.NewGuid().ToString(),
				UserName = $"guest_{guestToken}",
				FullName = string.IsNullOrWhiteSpace(
						state.Shipping.FullName)
					? "Guest"
					: state.Shipping.FullName.Trim(),
				PhoneNumber = state.Shipping.Phone?.Trim()
			};

			IdentityResult createResult =
				await userManager.CreateAsync(guestUser);

			if (!createResult.Succeeded)
			{
				string error = string.Join(
					" ",
					createResult.Errors.Select(item => item.Description));

				throw new InvalidOperationException(
					string.IsNullOrWhiteSpace(error)
						? "Guest checkout profile could not be created."
						: error);
			}

			string anonymousOwnerKey =
				$"anon:{httpContext.Session.Id}";

			var anonymousLines =
				await cartStore.GetAsync(
					anonymousOwnerKey,
					cancellationToken);

			foreach (var line in anonymousLines)
			{
				if (line.Quantity <= 0)
				{
					continue;
				}

				await cartStore.AddOrUpdateAsync(
					guestUser.Id,
					line.ProductId,
					line.Quantity,
					cancellationToken);
			}

			await cartStore.ClearAsync(
				anonymousOwnerKey,
				cancellationToken);

			httpContext.Session.SetString(
				GuestUserSessionKey,
				guestUser.Id);

			return guestUser;
		}
	}
}
