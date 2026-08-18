using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PaladinHub.Models.Checkout;

namespace PaladinHubV2.Server.Domain.Services.Checkout
{
	public sealed class CheckoutStateService : ICheckoutStateService
	{
		private const string SessionKey = "checkout_state";

		private readonly IHttpContextAccessor _httpContextAccessor;

		public CheckoutStateService(
			IHttpContextAccessor httpContextAccessor)
		{
			_httpContextAccessor = httpContextAccessor;
		}

		public CheckoutState Get()
		{
			byte[]? bytes = Session.Get(SessionKey);

			if (bytes == null || bytes.Length == 0)
			{
				var state = new CheckoutState();
				Save(state);
				return state;
			}

			try
			{
				return JsonSerializer.Deserialize<CheckoutState>(bytes) ??
					new CheckoutState();
			}
			catch (JsonException)
			{
				var state = new CheckoutState();
				Save(state);
				return state;
			}
		}

		public void Save(CheckoutState state)
		{
			ArgumentNullException.ThrowIfNull(state);

			Session.Set(
				SessionKey,
				JsonSerializer.SerializeToUtf8Bytes(state));
		}

		public void Clear()
		{
			Session.Remove(SessionKey);
		}

		public void NormalizeShipping(ShippingInfoVM shipping)
		{
			ArgumentNullException.ThrowIfNull(shipping);

			shipping.FullName = shipping.FullName?.Trim() ?? string.Empty;
			shipping.Address = shipping.Address?.Trim() ?? string.Empty;
			shipping.City = shipping.City?.Trim() ?? string.Empty;
			shipping.PostalCode = shipping.PostalCode?.Trim() ?? string.Empty;
			shipping.Country = shipping.Country?.Trim() ?? string.Empty;
			shipping.Phone = shipping.Phone?.Trim() ?? string.Empty;
			shipping.Email = string.IsNullOrWhiteSpace(shipping.Email)
				? null
				: shipping.Email.Trim();
		}

		private ISession Session =>
			_httpContextAccessor.HttpContext?.Session ??
			throw new InvalidOperationException(
				"Checkout session is not available for the current request.");
	}
}
