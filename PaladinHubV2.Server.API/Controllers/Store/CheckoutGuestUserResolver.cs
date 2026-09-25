using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
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
		internal const string AnonymousCartSessionKey =
			"PaladinHub.AnonymousCartId";

		internal static string GetOwnerKey(
			HttpContext httpContext,
			ClaimsPrincipal principal)
		{
			string? authenticatedUserId =
				principal.FindFirstValue(ClaimTypes.NameIdentifier);

			if (!string.IsNullOrWhiteSpace(authenticatedUserId))
			{
				return authenticatedUserId;
			}

			ISession? session = GetSession(httpContext);
			string? guestUserId = session?.GetString(GuestUserSessionKey);

			if (!string.IsNullOrWhiteSpace(guestUserId))
			{
				return guestUserId;
			}

			string? anonymousId = session?.GetString(AnonymousCartSessionKey);
			if (session != null && string.IsNullOrWhiteSpace(anonymousId))
			{
				anonymousId = session.Id;
				// Reading Id alone does not persist the ASP.NET Core session cookie.
				session.SetString(AnonymousCartSessionKey, anonymousId);
			}

			anonymousId ??= httpContext.TraceIdentifier;

			return $"anon:{anonymousId}";
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

			ISession? session = GetSession(httpContext);
			string? guestUserId = session?.GetString(GuestUserSessionKey);

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
			User? existing = await ResolveExistingAsync(
				httpContext,
				principal,
				userManager);

			if (existing != null)
			{
				return existing;
			}

			ISession? session = GetSession(httpContext);
			if (session == null)
			{
				throw new InvalidOperationException(
					"Guest checkout session is unavailable.");
			}

			var state = checkoutSession.GetState();
			if (state?.Shipping == null)
			{
				throw new InvalidOperationException(
					"Shipping details are required before guest checkout can continue.");
			}

			string guestToken = Guid.NewGuid().ToString("N");
			var guestUser = new User
			{
				Id = Guid.NewGuid().ToString(),
				UserName = $"guest_{guestToken}",
				FullName = string.IsNullOrWhiteSpace(state.Shipping.FullName)
					? "Guest"
					: state.Shipping.FullName.Trim(),
				PhoneNumber = state.Shipping.Phone?.Trim()
			};

			IdentityResult createResult = await userManager.CreateAsync(guestUser);

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

			string anonymousOwnerKey = GetOwnerKey(httpContext, principal);
			var anonymousLines = await cartStore.GetAsync(
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

			session.SetString(GuestUserSessionKey, guestUser.Id);
			return guestUser;
		}

		private static ISession? GetSession(HttpContext? httpContext)
		{
			return httpContext?.Features.Get<ISessionFeature>()?.Session;
		}
	}
}
