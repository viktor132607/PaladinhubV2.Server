using PaladinHubV2.Server.Data.Entities;

using DbPaymentMethod = PaladinHubV2.Server.Data.Entities.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public partial class PaymentMethodsService
	{
		public Task<List<DbPaymentMethod>> GetMethods(User me) =>
			_store.GetMethods(me);

		public async Task<bool> RemovePaymentMethod(User me, string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return false;
			}

			DbPaymentMethod? paymentMethod = await _store.GetOwned(me, id);
			if (paymentMethod == null)
			{
				return false;
			}

			string? externalId = paymentMethod.ExternalId;

			_store.Remove(paymentMethod);
			await _store.SaveChanges();

			if (!string.IsNullOrWhiteSpace(externalId))
			{
				try
				{
					await _stripe.Detach(externalId);
				}
				catch
				{
				}
			}

			return true;
		}

		public async Task<bool> SetDefaultPaymentMethod(User me, string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return false;
			}

			List<DbPaymentMethod> methods = await _store.GetMethodsForUpdate(me);
			DbPaymentMethod? target = methods.FirstOrDefault(x => x.Id == id);

			if (target == null)
			{
				return false;
			}

			foreach (DbPaymentMethod method in methods)
			{
				method.IsDefault = method.Id == id;
			}

			await _store.SaveChanges();

			if (!string.IsNullOrWhiteSpace(target.ExternalId))
			{
				try
				{
					string customerId = await EnsureStripeCustomer(me);
					await _stripe.Attach(customerId, target.ExternalId);
					await _stripe.SetDefault(customerId, target.ExternalId);
				}
				catch
				{
				}
			}

			return true;
		}
	}
}
