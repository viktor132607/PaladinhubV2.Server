using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

using DbPaymentMethod = PaladinHubV2.Server.Data.Entities.PaymentMethod;

namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public sealed class PaymentMethodsStore : IPaymentMethodsStore
	{
		private readonly AppDbContext _db;

		public PaymentMethodsStore(AppDbContext db)
		{
			_db = db;
		}

		public async Task<List<DbPaymentMethod>> GetMethods(User me)
		{
			return await _db.Set<DbPaymentMethod>()
				.Where(pm => pm.UserId == me.Id)
				.OrderByDescending(pm => pm.IsDefault)
				.ThenBy(pm => pm.Brand)
				.ToListAsync();
		}

		public Task<List<DbPaymentMethod>> GetMethodsForUpdate(User me)
		{
			return _db.Set<DbPaymentMethod>()
				.Where(x => x.UserId == me.Id)
				.ToListAsync();
		}

		public Task<DbPaymentMethod?> GetOwned(User me, string id)
		{
			return _db.Set<DbPaymentMethod>()
				.FirstOrDefaultAsync(x => x.Id == id && x.UserId == me.Id);
		}

		public void Add(DbPaymentMethod paymentMethod)
		{
			_db.Add(paymentMethod);
		}

		public void Remove(DbPaymentMethod paymentMethod)
		{
			_db.Remove(paymentMethod);
		}

		public void UpdateUser(User user)
		{
			_db.Update(user);
		}

		public Task SaveChanges()
		{
			return _db.SaveChangesAsync();
		}
	}
}
