using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Promos
{
	public enum PromoCreateError
	{
		None = 0,
		Validation,
		Duplicate
	}

	public sealed record PromoValidationError(
		string Field,
		string Message);

	public sealed record PromoCreateResult(
		PromoCreateError Error,
		PromoCode? PromoCode = null,
		IReadOnlyList<PromoValidationError>? ValidationErrors = null);

	public sealed class PromoCodeAdminService
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

		public Task<List<PromoCode>> ListAsync(
			CancellationToken cancellationToken)
		{
			return _db.PromoCodes
				.AsNoTracking()
				.OrderByDescending(promo => promo.CreatedAtUtc)
				.ToListAsync(cancellationToken);
		}

		public static PromoCode BuildCreateModel()
		{
			return new PromoCode
			{
				Type = PromoCodeType.Balance,
				Value = 5m,
				Currency = "EUR",
				IsActive = true
			};
		}

		public async Task<PromoCreateResult> CreateAsync(
			PromoCode model,
			CancellationToken cancellationToken)
		{
			IReadOnlyList<PromoValidationError> validationErrors =
				NormalizeAndValidate(model);

			if (validationErrors.Count > 0)
			{
				return new PromoCreateResult(
					PromoCreateError.Validation,
					ValidationErrors: validationErrors);
			}

			bool codeExists = await _db.PromoCodes
				.AsNoTracking()
				.AnyAsync(
					promo => promo.Code == model.Code,
					cancellationToken);

			if (codeExists)
			{
				return new PromoCreateResult(
					PromoCreateError.Duplicate);
			}

			model.Id = Guid.NewGuid().ToString("N");
			model.UsedCount = 0;
			model.IsActive = true;
			model.CreatedAtUtc = DateTime.UtcNow;

			try
			{
				PromoCode created =
					await _promoCodes.CreateAsync(model);

				return new PromoCreateResult(
					PromoCreateError.None,
					created);
			}
			catch (DbUpdateException)
			{
				return new PromoCreateResult(
					PromoCreateError.Duplicate);
			}
		}

		public Task<bool> DeactivateAsync(string id)
		{
			return _promoCodes.DeactivateAsync(id.Trim());
		}

		private static IReadOnlyList<PromoValidationError>
			NormalizeAndValidate(PromoCode model)
		{
			var errors = new List<PromoValidationError>();

			model.Code = model.Code?.Trim().ToUpperInvariant()
				?? string.Empty;

			model.Currency = NormalizeOptional(model.Currency)?
				.ToUpperInvariant();

			model.Notes = NormalizeOptional(model.Notes);

			if (string.IsNullOrWhiteSpace(model.Code))
			{
				errors.Add(new(
					nameof(model.Code),
					"Code is required."));
			}
			else if (model.Code.Length > 64)
			{
				errors.Add(new(
					nameof(model.Code),
					"Code cannot exceed 64 characters."));
			}

			if (!Enum.IsDefined(
					typeof(PromoCodeType),
					model.Type))
			{
				errors.Add(new(
					nameof(model.Type),
					"Invalid promo code type."));
			}

			if (model.Value <= 0m)
			{
				errors.Add(new(
					nameof(model.Value),
					"Value must be greater than zero."));
			}

			if (model.Type == PromoCodeType.DiscountPercent &&
				model.Value > 100m)
			{
				errors.Add(new(
					nameof(model.Value),
					"A percentage discount cannot exceed 100."));
			}

			if (model.Type == PromoCodeType.DiscountPercent)
			{
				model.Currency = null;
			}
			else if (model.Currency?.Length > 3)
			{
				errors.Add(new(
					nameof(model.Currency),
					"Currency cannot exceed 3 characters."));
			}

			if (model.MaxUses.HasValue &&
				model.MaxUses.Value <= 0)
			{
				errors.Add(new(
					nameof(model.MaxUses),
					"Max Uses must be greater than zero."));
			}

			if (model.Notes?.Length > 256)
			{
				errors.Add(new(
					nameof(model.Notes),
					"Notes cannot exceed 256 characters."));
			}

			return errors;
		}

		private static string? NormalizeOptional(string? value)
		{
			return string.IsNullOrWhiteSpace(value)
				? null
				: value.Trim();
		}
	}
}
