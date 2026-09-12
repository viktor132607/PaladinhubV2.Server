using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PaladinHub.Models.Checkout;

namespace PaladinHubV2.Server.Domain.Services.Checkout
{
	public interface ICheckoutSessionService
	{
		CheckoutState GetState();
		void SaveState(CheckoutState state);
		void Clear();
		void NormalizeShipping(ShippingInfoVM shipping);
	}

	public sealed class CheckoutSessionService : ICheckoutSessionService
	{
		private const string SessionKey = "checkout_state";
		private readonly IHttpContextAccessor _httpContextAccessor;

		public CheckoutSessionService(
			IHttpContextAccessor httpContextAccessor)
		{
			_httpContextAccessor = httpContextAccessor;
		}

		public CheckoutState GetState()
		{
			ISession session = GetSession();
			byte[]? bytes = session.Get(SessionKey);

			if (bytes == null || bytes.Length == 0)
			{
				var state = new CheckoutState();
				SaveState(state);
				return state;
			}

			try
			{
				return JsonSerializer
					.Deserialize<CheckoutState>(bytes) ??
					new CheckoutState();
			}
			catch (JsonException)
			{
				var state = new CheckoutState();
				SaveState(state);
				return state;
			}
		}

		public void SaveState(CheckoutState state)
		{
			GetSession().Set(
				SessionKey,
				JsonSerializer.SerializeToUtf8Bytes(state));
		}

		public void Clear()
		{
			GetSession().Remove(SessionKey);
		}

		public void NormalizeShipping(ShippingInfoVM shipping)
		{
			shipping.FullName =
				shipping.FullName?.Trim() ??
				string.Empty;

			shipping.Address =
				shipping.Address?.Trim() ??
				string.Empty;

			shipping.City =
				shipping.City?.Trim() ??
				string.Empty;

			shipping.PostalCode =
				shipping.PostalCode?.Trim() ??
				string.Empty;

			shipping.Country =
				shipping.Country?.Trim() ??
				string.Empty;

			shipping.Phone =
				shipping.Phone?.Trim() ??
				string.Empty;

			shipping.Email =
				string.IsNullOrWhiteSpace(shipping.Email)
					? null
					: shipping.Email.Trim();
		}

		private ISession GetSession()
		{
			return _httpContextAccessor.HttpContext?.Session ??
				throw new InvalidOperationException(
					"Checkout session is unavailable outside an HTTP request.");
		}
	}
}
