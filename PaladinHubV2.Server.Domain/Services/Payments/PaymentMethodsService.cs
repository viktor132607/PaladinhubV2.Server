namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public partial class PaymentMethodsService : IPaymentMethodsService
	{
		private readonly IPaymentMethodsPageService _page;
		private readonly IPaymentMethodsStore _store;
		private readonly IStripePaymentMethodsGateway _stripe;

		public PaymentMethodsService(
			IPaymentMethodsPageService page,
			IPaymentMethodsStore store,
			IStripePaymentMethodsGateway stripe)
		{
			_page = page;
			_store = store;
			_stripe = stripe;
		}
	}
}
