using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using Stripe;

using DbPaymentMethod = PaladinHubV2.Server.Data.Entities.PaymentMethod;
using StripePmService = Stripe.PaymentMethodService;

namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public interface IPaymentMethodsService
	{
		string? GetStripePublishableKey();
		Task<string> EnsureStripeCustomer(User me);
		Task<List<DbPaymentMethod>> GetMethods(User me);
		Task AddStripePaymentMethod(User me, string paymentMethodId);
		Task<bool> RemovePaymentMethod(User me, string id);
		Task<bool> SetDefaultPaymentMethod(User me, string id);
	}

	public class PaymentMethodsService : IPaymentMethodsService
	{
		private readonly AppDbContext _db;
		private readonly IConfiguration _cfg;

		public PaymentMethodsService(AppDbContext db, IConfiguration cfg)
		{
			_db = db;
			_cfg = cfg;
			var sk = _cfg["Stripe:SecretKey"];
			if (!string.IsNullOrWhiteSpace(sk)) StripeConfiguration.ApiKey = sk;
		}

		public string? GetStripePublishableKey() => _cfg["Stripe:PublishableKey"];

		public async Task<string> EnsureStripeCustomer(User me)
		{
			if (!string.IsNullOrWhiteSpace(me.StripeCustomerId)) return me.StripeCustomerId!;
			var cs = new CustomerService();
			var c = await cs.CreateAsync(new CustomerCreateOptions { Email = me.Email, Name = me.FullName });
			me.StripeCustomerId = c.Id;
			_db.Update(me);
			await _db.SaveChangesAsync();
			return c.Id;
		}

		public async Task<List<DbPaymentMethod>> GetMethods(User me)
		{
			return await _db.Set<DbPaymentMethod>()
				.Where(pm => pm.UserId == me.Id)
				.OrderByDescending(pm => pm.IsDefault)
				.ThenBy(pm => pm.Brand)
				.ToListAsync();
		}

		public async Task AddStripePaymentMethod(User me, string paymentMethodId)
		{
			var customerId = await EnsureStripeCustomer(me);
			var pms = new StripePmService();
			await pms.AttachAsync(paymentMethodId, new PaymentMethodAttachOptions { Customer = customerId });
			var pm = await pms.GetAsync(paymentMethodId);

			var cs = new CustomerService();
			await cs.UpdateAsync(customerId, new CustomerUpdateOptions
			{
				InvoiceSettings = new CustomerInvoiceSettingsOptions { DefaultPaymentMethod = paymentMethodId }
			});

			var entity = new DbPaymentMethod
			{
				Id = Guid.NewGuid().ToString("N"),
				UserId = me.Id,
				Brand = pm.Card.Brand,
				Last4 = pm.Card.Last4,
				IsDefault = true,
				Label = "Payment Method",
				ExternalId = pm.Id,
				Provider = "Stripe",
				CreatedAtUtc = DateTime.UtcNow
			};

			var all = await _db.Set<DbPaymentMethod>().Where(x => x.UserId == me.Id).ToListAsync();
			foreach (var m in all) m.IsDefault = false;

			_db.Add(entity);
			await _db.SaveChangesAsync();
		}

		public async Task<bool> RemovePaymentMethod(User me, string id)
		{
			if (string.IsNullOrWhiteSpace(id)) return false;

			var pm = await _db.Set<DbPaymentMethod>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == me.Id);
			if (pm == null) return false;

			var externalId = pm.ExternalId;

			_db.Remove(pm);
			await _db.SaveChangesAsync();

			if (!string.IsNullOrWhiteSpace(externalId))
			{
				try
				{
					var s = new StripePmService();
					await s.DetachAsync(externalId);
				}
				catch { }
			}
			return true;
		}

		public async Task<bool> SetDefaultPaymentMethod(User me, string id)
		{
			if (string.IsNullOrWhiteSpace(id)) return false;

			var methods = await _db.Set<DbPaymentMethod>().Where(x => x.UserId == me.Id).ToListAsync();
			var target = methods.FirstOrDefault(x => x.Id == id);
			if (target == null) return false;

			foreach (var m in methods) m.IsDefault = m.Id == id;
			await _db.SaveChangesAsync();

			if (!string.IsNullOrWhiteSpace(target.ExternalId))
			{
				try
				{
					var customerId = await EnsureStripeCustomer(me);
					var pms = new StripePmService();
					await pms.AttachAsync(target.ExternalId, new PaymentMethodAttachOptions { Customer = customerId });
					var cs = new CustomerService();
					await cs.UpdateAsync(customerId, new CustomerUpdateOptions
					{
						InvoiceSettings = new CustomerInvoiceSettingsOptions { DefaultPaymentMethod = target.ExternalId }
					});
				}
				catch { }
			}
			return true;
		}
	}
}
