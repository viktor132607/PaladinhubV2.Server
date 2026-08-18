using PaladinHub.Models.Checkout;

namespace PaladinHubV2.Server.Domain.Services.Checkout
{
	public interface ICheckoutStateService
	{
		CheckoutState Get();
		void Save(CheckoutState state);
		void Clear();
		void NormalizeShipping(ShippingInfoVM shipping);
	}
}
