using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Promos
{
	public sealed class PromoCodeAdminService : IPromoCodeAdminService
	{
		private readonly AppDbContext _db;
		private readonly IPromoCodeService _promoCodes;

		public PromoCodeAdminService(
			AppDbContext db,
			IPromoCodeService promoCodes)
		{
			_db = db;
			_promoCodes = promoCodes;
		}

		public Task<List<PromoCode>> GetAllAsync(
			CancellationToken cancellationToken = default)
		{
			return _db.PromoCodes
				.AsNoTracking()
				.OrderByDescending(promo => promo.CreatedAtUtc)
				.ToListAsync(cancellationToken);
		}

		public PromoCode BuildCreateModel()
		{
			return new PromoCode
			{
				Type = PromoCodeType.Balance,
				Value = 5m,
				Currency = "EUR",
				IsActive = true
			};
		}

		public IReadOnlyDictionary<string, string[]> NormalizeAndValidate(
			PromoCode model)
		{
			model.Code = model.Code?.Trim().ToUpperInvariant() ?? string.Empty;
			model.Currency = NormalizeOptional(model.Currency)?.ToUpperInvariant();
			model.Notes = NormalizeOptional(model.Notes);

			var errors = new Dictionary<string, List<string>>();
			void Add(string key, string message)
			{
				if (!errors.TryGetValue(key, out List<string>? messages))
				{
					messages = new List<string>();
					errors[key] = messages;
				}
				messages.Add(message);
			}

			if (string.IsNullOrWhiteSpace(model.Code))
				Add(nameof(model.Code), "Code is required.");
			else if (model.Code.Length > 64)
				Add(nameof(model.Code), "Code cannot exceed 64 characters.");

			if (!Enum.IsDefined(typeof(PromoCodeType), model.Type))
				Add(nameof(model.Type), "Invalid promo code type.");

			if (model.Value <= 0m)
				Add(nameof(model.Value), "Value must be greater than zero.");

			if (model.Type == PromoCodeType.DiscountPercent && model.Value > 100m)
				Add(nameof(model.Value), "A percentage discount cannot exceed 100.");

			if (model.Type == PromoCodeType.DiscountPercent)
				model.Currency = null;
			else if (model.Currency?.Length > 3)
				Add(nameof(model.Currency), "Currency cannot exceed 3 characters.");

			if (model.MaxUses.HasValue && model.MaxUses.Value <= 0)
				Add(nameof(model.MaxUses), "Max Uses must be greater than zero.");

			if (model.Notes?.Length > 256)
				Add(nameof(model.Notes), "Notes cannot exceed 256 characters.");

			return errors.ToDictionary(
				pair => pair.Key,
				pair => pair.Value.ToArray());
		}

		public async Task<PromoCode?> CreateAsync(
			PromoCode model,
			CancellationToken cancellationToken = default)
		{
			bool codeExists = await _db.PromoCodes
				.AsNoTracking()
				.AnyAsync(promo => promo.Code == model.Code, cancellationToken);

			if (codeExists)
				return null;

			model.Id = Guid.NewGuid().ToString("N");
			model.UsedCount = 0;
			model.IsActive = true;
			model.CreatedAtUtc = DateTime.UtcNow;

			try
			{
				return await _promoCodes.CreateAsync(model);
			}
			catch (DbUpdateException)
			{
				return null;
			}
		}

		public Task<bool> DeactivateAsync(string id)
		{
			return _promoCodes.DeactivateAsync(id.Trim());
		}

		private static string? NormalizeOptional(string? value)
		{
			return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}
	}
}
