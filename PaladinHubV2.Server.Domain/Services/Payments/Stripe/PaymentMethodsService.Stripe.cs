using PaladinHubV2.Server.Data.Entities;

using DbPaymentMethod = PaladinHubV2.Server.Data.Entities.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public partial class PaymentMethodsService
	{
		public string? GetStripePublishableKey() =>
			_stripe.GetPublishableKey();

		public async Task<string> EnsureStripeCustomer(User me)
		{
			if (!string.IsNullOrWhiteSpace(me.StripeCustomerId))
			{
				return me.StripeCustomerId!;
			}

			string customerId = await _stripe.CreateCustomer(me);
			me.StripeCustomerId = customerId;
			_store.UpdateUser(me);
			await _store.SaveChanges();
			return customerId;
		}

		public async Task AddStripePaymentMethod(
			User me,
			string paymentMethodId)
		{
			string customerId = await EnsureStripeCustomer(me);
			Stripe.PaymentMethod paymentMethod =
				await _stripe.AttachAndGet(customerId, paymentMethodId);

			await _stripe.SetDefault(customerId, paymentMethodId);

			var entity = new DbPaymentMethod
			{
				Id = Guid.NewGuid().ToString("N"),
				UserId = me.Id,
				Brand = paymentMethod.Card.Brand,
				Last4 = paymentMethod.Card.Last4,
				IsDefault = true,
				Label = "Payment Method",
				ExternalId = paymentMethod.Id,
				Provider = "Stripe",
				CreatedAtUtc = DateTime.UtcNow
			};

			List<DbPaymentMethod> all = await _store.GetMethodsForUpdate(me);
			foreach (DbPaymentMethod method in all)
			{
				method.IsDefault = false;
			}

			_store.Add(entity);
			await _store.SaveChanges();
		}
	}
}
