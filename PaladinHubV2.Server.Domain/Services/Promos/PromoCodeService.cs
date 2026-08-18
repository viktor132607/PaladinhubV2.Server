using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Promos
{
	public class PromoCodeService : IPromoCodeService
	{
		private readonly AppDbContext _db;

		public PromoCodeService(AppDbContext db) { _db = db; }

		public async Task<(bool ok, string msg, decimal? amount, string? currency, int? percent)> RedeemAsync(User user, string rawCode, string fallbackCurrency)
		{
			if (string.IsNullOrWhiteSpace(rawCode))
				return (false, "Invalid code.", null, null, null);

			var code = rawCode.Trim().ToUpperInvariant();

			var promo = await _db.PromoCodes.FirstOrDefaultAsync(p => p.Code == code);
			if (promo == null || !promo.IsActive)
				return (false, "Code not found or inactive.", null, null, null);

			if (promo.ExpiresAtUtc.HasValue && promo.ExpiresAtUtc.Value < DateTime.UtcNow)
				return (false, "This code has expired.", null, null, null);

			if (promo.MaxUses.HasValue && promo.UsedCount >= promo.MaxUses.Value)
				return (false, "This code has reached its maximum redemptions.", null, null, null);

			var already = await _db.PromoRedemptions.AnyAsync(r => r.PromoCodeId == promo.Id && r.UserId == user.Id);
			if (already)
				return (false, "You have already redeemed this code.", null, null, null);

			if (promo.Type == PromoCodeType.Balance)
			{
				var currency = string.IsNullOrWhiteSpace(promo.Currency) ? fallbackCurrency : promo.Currency!;
				var amount = promo.Value;

				_db.Transactions.Add(new Transaction
				{
					Id = Guid.NewGuid(),
					UserId = user.Id,
					PurchaseTitle = $"Promo {promo.Code}",
					Amount = amount,
					Currency = currency,
					Status = TransactionStatus.Complete,
					Region = "Promo",
					CreatedAtUtc = DateTime.UtcNow
				});

				_db.PromoRedemptions.Add(new PromoRedemption
				{
					PromoCodeId = promo.Id,
					UserId = user.Id,
					AmountCredited = amount,
					Currency = currency
				});

				promo.UsedCount += 1;
				await _db.SaveChangesAsync();

				return (true, $"Balance credited: {amount:0.##} {currency}.", amount, currency, null);
			}
			else
			{
				var percent = (int)Math.Round(promo.Value);

				_db.PromoRedemptions.Add(new PromoRedemption
				{
					PromoCodeId = promo.Id,
					UserId = user.Id
				});

				promo.UsedCount += 1;
				await _db.SaveChangesAsync();

				return (true, $"Discount code applied: {percent}% off will be used at checkout.", null, null, percent);
			}
		}

		public async Task<PromoCode> CreateAsync(PromoCode code)
		{
			code.Code = code.Code.Trim().ToUpperInvariant();
			_db.PromoCodes.Add(code);
			await _db.SaveChangesAsync();
			return code;
		}

		public async Task<bool> DeactivateAsync(string id)
		{
			var p = await _db.PromoCodes.FirstOrDefaultAsync(x => x.Id == id);
			if (p == null) return false;
			p.IsActive = false;
			await _db.SaveChangesAsync();
			return true;
		}
	}
}
