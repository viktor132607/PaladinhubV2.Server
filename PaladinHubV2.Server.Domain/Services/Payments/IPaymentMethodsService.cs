using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Accounts;
using Stripe;

using DbPaymentMethod = PaladinHubV2.Server.Data.Entities.PaymentMethod;
using StripePmService = Stripe.PaymentMethodService;

namespace PaladinHubV2.Server.Domain.Services.Payments
{
	public sealed record PaymentMethodsPageData(
		string Region,
		string RegionCode,
		string Currency,
		decimal Balance,
		IReadOnlyList<DbPaymentMethod> Methods);

	public interface IPaymentMethodsService
	{
		Task<PaymentMethodsPageData?> GetPageAsync(
			ClaimsPrincipal principal);

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
		private readonly IAccountUiService _ui;

		public PaymentMethodsService(
			AppDbContext db,
			IConfiguration cfg,
			IAccountUiService ui)
		{
			_db = db;
			_cfg = cfg;
			_ui = ui;

			var sk = _cfg["Stripe:SecretKey"];
			if (!string.IsNullOrWhiteSpace(sk))
			{
				StripeConfiguration.ApiKey = sk;
			}
		}

		public async Task<PaymentMethodsPageData?> GetPageAsync(
			ClaimsPrincipal principal)
		{
			User? user = await _ui.GetMe(principal);

			if (user == null)
			{
				return null;
			}

			string regionCode =
				_ui.ReadRegionCookie() ?? "EU";

			string currency =
				_ui.GetCurrencyForRegion(regionCode);

			decimal balance =
				await _ui.GetBalance(user.Id);

			List<DbPaymentMethod> methods =
				await GetMethods(user);

			return new PaymentMethodsPageData(
				_ui.RegionDisplay(regionCode),
				regionCode,
				currency,
				balance,
				methods);
		}

		public string? GetStripePublishableKey()
		{
			return _cfg["Stripe:PublishableKey"];
		}

		public async Task<string> EnsureStripeCustomer(User me)
		{
			if (!string.IsNullOrWhiteSpace(me.StripeCustomerId))
			{
				return me.StripeCustomerId!;
			}

			var customerService = new CustomerService();

			var customer = await customerService.CreateAsync(
				new CustomerCreateOptions
				{
					Email = me.Email,
					Name = me.FullName
				});

			me.StripeCustomerId = customer.Id;
			_db.Update(me);
			await _db.SaveChangesAsync();
			return customer.Id;
		}

		public async Task<List<DbPaymentMethod>> GetMethods(User me)
		{
			return await _db.Set<DbPaymentMethod>()
				.Where(pm => pm.UserId == me.Id)
				.OrderByDescending(pm => pm.IsDefault)
				.ThenBy(pm => pm.Brand)
				.ToListAsync();
		}

		public async Task AddStripePaymentMethod(
			User me,
			string paymentMethodId)
		{
			string customerId =
				await EnsureStripeCustomer(me);

			var paymentMethods = new StripePmService();

			await paymentMethods.AttachAsync(
				paymentMethodId,
				new PaymentMethodAttachOptions
				{
					Customer = customerId
				});

			var paymentMethod =
				await paymentMethods.GetAsync(paymentMethodId);

			var customerService = new CustomerService();

			await customerService.UpdateAsync(
				customerId,
				new CustomerUpdateOptions
				{
					InvoiceSettings =
						new CustomerInvoiceSettingsOptions
						{
							DefaultPaymentMethod =
								paymentMethodId
						}
				});

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

			var all = await _db.Set<DbPaymentMethod>()
				.Where(x => x.UserId == me.Id)
				.ToListAsync();

			foreach (var method in all)
			{
				method.IsDefault = false;
			}

			_db.Add(entity);
			await _db.SaveChangesAsync();
		}

		public async Task<bool> RemovePaymentMethod(
			User me,
			string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return false;
			}

			var paymentMethod =
				await _db.Set<DbPaymentMethod>()
					.FirstOrDefaultAsync(
						x =>
							x.Id == id &&
							x.UserId == me.Id);

			if (paymentMethod == null)
			{
				return false;
			}

			string? externalId =
				paymentMethod.ExternalId;

			_db.Remove(paymentMethod);
			await _db.SaveChangesAsync();

			if (!string.IsNullOrWhiteSpace(externalId))
			{
				try
				{
					var stripeMethods = new StripePmService();
					await stripeMethods.DetachAsync(externalId);
				}
				catch
				{
				}
			}

			return true;
		}

		public async Task<bool> SetDefaultPaymentMethod(
			User me,
			string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return false;
			}

			var methods = await _db.Set<DbPaymentMethod>()
				.Where(x => x.UserId == me.Id)
				.ToListAsync();

			var target =
				methods.FirstOrDefault(x => x.Id == id);

			if (target == null)
			{
				return false;
			}

			foreach (var method in methods)
			{
				method.IsDefault = method.Id == id;
			}

			await _db.SaveChangesAsync();

			if (!string.IsNullOrWhiteSpace(target.ExternalId))
			{
				try
				{
					string customerId =
						await EnsureStripeCustomer(me);

					var paymentMethods = new StripePmService();

					await paymentMethods.AttachAsync(
						target.ExternalId,
						new PaymentMethodAttachOptions
						{
							Customer = customerId
						});

					var customerService =
						new CustomerService();

					await customerService.UpdateAsync(
						customerId,
						new CustomerUpdateOptions
						{
							InvoiceSettings =
								new CustomerInvoiceSettingsOptions
								{
									DefaultPaymentMethod =
										target.ExternalId
								}
						});
				}
				catch
				{
				}
			}

			return true;
		}
	}
}
