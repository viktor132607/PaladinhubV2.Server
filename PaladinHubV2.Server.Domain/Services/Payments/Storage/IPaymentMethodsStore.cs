using PaladinHubV2.Server.Data.Entities;

using DbPaymentMethod = PaladinHubV2.Server.Data.Entities.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public interface IPaymentMethodsStore
	{
		Task<List<DbPaymentMethod>> GetMethods(User me);
		Task<List<DbPaymentMethod>> GetMethodsForUpdate(User me);
		Task<DbPaymentMethod?> GetOwned(User me, string id);
		void Add(DbPaymentMethod paymentMethod);
		void Remove(DbPaymentMethod paymentMethod);
		void UpdateUser(User user);
		Task SaveChanges();
	}
}
