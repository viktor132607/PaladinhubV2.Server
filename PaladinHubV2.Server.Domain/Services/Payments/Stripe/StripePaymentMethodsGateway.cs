using Microsoft.Extensions.Configuration;
using PaladinHubV2.Server.Data.Entities;
using Stripe;

using StripePaymentMethod = Stripe.PaymentMethod;
using StripePmService = Stripe.PaymentMethodService;

namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public sealed class StripePaymentMethodsGateway : IStripePaymentMethodsGateway
	{
		private readonly IConfiguration _configuration;

		public StripePaymentMethodsGateway(IConfiguration configuration)
		{
			_configuration = configuration;

			string? secretKey = _configuration["Stripe:SecretKey"];
			if (!string.IsNullOrWhiteSpace(secretKey))
			{
				StripeConfiguration.ApiKey = secretKey;
			}
		}

		public string? GetPublishableKey()
		{
			return _configuration["Stripe:PublishableKey"];
		}

		public async Task<string> CreateCustomer(User user)
		{
			var customerService = new CustomerService();
			var customer = await customerService.CreateAsync(
				new CustomerCreateOptions
				{
					Email = user.Email,
					Name = user.FullName
				});

			return customer.Id;
		}

		public async Task<StripePaymentMethod> AttachAndGet(
			string customerId,
			string paymentMethodId)
		{
			var paymentMethods = new StripePmService();
			await paymentMethods.AttachAsync(
				paymentMethodId,
				new PaymentMethodAttachOptions
				{
					Customer = customerId
				});

			return await paymentMethods.GetAsync(paymentMethodId);
		}

		public Task Attach(string customerId, string paymentMethodId)
		{
			var paymentMethods = new StripePmService();
			return paymentMethods.AttachAsync(
				paymentMethodId,
				new PaymentMethodAttachOptions
				{
					Customer = customerId
				});
		}

		public Task SetDefault(string customerId, string paymentMethodId)
		{
			var customerService = new CustomerService();
			return customerService.UpdateAsync(
				customerId,
				new CustomerUpdateOptions
				{
					InvoiceSettings =
						new CustomerInvoiceSettingsOptions
						{
							DefaultPaymentMethod = paymentMethodId
						}
				});
		}

		public Task Detach(string paymentMethodId)
		{
			var paymentMethods = new StripePmService();
			return paymentMethods.DetachAsync(paymentMethodId);
		}
	}
}
