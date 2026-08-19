using PaladinHubV2.Server.Data.Entities;

using StripePaymentMethod = Stripe.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public interface IStripePaymentMethodsGateway
	{
		string? GetPublishableKey();
		Task<string> CreateCustomer(User user);
		Task<StripePaymentMethod> AttachAndGet(string customerId, string paymentMethodId);
		Task Attach(string customerId, string paymentMethodId);
		Task SetDefault(string customerId, string paymentMethodId);
		Task Detach(string paymentMethodId);
	}
}
