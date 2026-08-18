using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Promos;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/promo-codes")]
	public sealed class PromoCodesController : ControllerBase
	{
		private readonly AppDbContext _db;
		private readonly IPromoCodeService _promoCodes;

		public PromoCodesController(
			AppDbContext db,
			IPromoCodeService promoCodes)
		{
			_db = db;
			_promoCodes = promoCodes;
		}

		[HttpGet]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Index(
			CancellationToken cancellationToken)
		{
			var promoCodes = await _db.PromoCodes
				.AsNoTracking()
				.OrderByDescending(promo => promo.CreatedAtUtc)
				.ToListAsync(cancellationToken);

			return Ok(promoCodes);
		}

		[HttpGet("create")]
		[HttpGet("~/Admin/PromoCodes/Create")]
		public IActionResult Create()
		{
			return Ok(new PromoCode
			{
				Type = PromoCodeType.Balance,
				Value = 5m,
				Currency = "EUR",
				IsActive = true
			});
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> CreateApi(
			[FromBody] PromoCode? model,
			CancellationToken cancellationToken)
		{
			return CreateCore(model, cancellationToken);
		}

		[HttpPost("~/Admin/PromoCodes/Create")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> CreateLegacy(
			[FromForm] PromoCode? model,
			CancellationToken cancellationToken)
		{
			return CreateCore(model, cancellationToken);
		}

		[HttpPost("{id}/deactivate")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeactivateApi(
			[FromRoute] string id)
		{
			return DeactivateCore(id);
		}

		[HttpPost("~/Admin/PromoCodes/Deactivate")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeactivateLegacy(
			[FromForm] string id)
		{
			return DeactivateCore(id);
		}

		private async Task<IActionResult> CreateCore(
			PromoCode? model,
			CancellationToken cancellationToken)
		{
			if (model == null)
			{
				return BadRequest(new
				{
					message = "Promo code data is required."
				});
			}

			model.Code = model.Code?.Trim().ToUpperInvariant()
				?? string.Empty;

			model.Currency = NormalizeOptional(model.Currency)?
				.ToUpperInvariant();

			model.Notes = NormalizeOptional(model.Notes);

			if (string.IsNullOrWhiteSpace(model.Code))
			{
				ModelState.AddModelError(
					nameof(model.Code),
					"Code is required.");
			}
			else if (model.Code.Length > 64)
			{
				ModelState.AddModelError(
					nameof(model.Code),
					"Code cannot exceed 64 characters.");
			}

			if (!Enum.IsDefined(typeof(PromoCodeType), model.Type))
			{
				ModelState.AddModelError(
					nameof(model.Type),
					"Invalid promo code type.");
			}

			if (model.Value <= 0m)
			{
				ModelState.AddModelError(
					nameof(model.Value),
					"Value must be greater than zero.");
			}

			if (model.Type == PromoCodeType.DiscountPercent &&
				model.Value > 100m)
			{
				ModelState.AddModelError(
					nameof(model.Value),
					"A percentage discount cannot exceed 100.");
			}

			if (model.Type == PromoCodeType.DiscountPercent)
			{
				model.Currency = null;
			}
			else if (model.Currency?.Length > 3)
			{
				ModelState.AddModelError(
					nameof(model.Currency),
					"Currency cannot exceed 3 characters.");
			}

			if (model.MaxUses.HasValue &&
				model.MaxUses.Value <= 0)
			{
				ModelState.AddModelError(
					nameof(model.MaxUses),
					"Max Uses must be greater than zero.");
			}

			if (model.Notes?.Length > 256)
			{
				ModelState.AddModelError(
					nameof(model.Notes),
					"Notes cannot exceed 256 characters.");
			}

			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			var codeExists = await _db.PromoCodes
				.AsNoTracking()
				.AnyAsync(
					promo => promo.Code == model.Code,
					cancellationToken);

			if (codeExists)
			{
				return Conflict(new
				{
					message = "Promo code already exists."
				});
			}

			model.Id = Guid.NewGuid().ToString("N");
			model.UsedCount = 0;
			model.IsActive = true;
			model.CreatedAtUtc = DateTime.UtcNow;

			try
			{
				var created =
					await _promoCodes.CreateAsync(model);

				return CreatedAtAction(
					nameof(Index),
					new { id = created.Id },
					created);
			}
			catch (DbUpdateException)
			{
				return Conflict(new
				{
					message = "Promo code already exists."
				});
			}
		}

		private async Task<IActionResult> DeactivateCore(
			string? id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return BadRequest(new
				{
					message = "Promo code ID is required."
				});
			}

			var normalizedId = id.Trim();

			var deactivated =
				await _promoCodes.DeactivateAsync(normalizedId);

			if (!deactivated)
			{
				return NotFound(new
				{
					message = "Promo code not found."
				});
			}

			return Ok(new
			{
				ok = true,
				id = normalizedId,
				isActive = false,
				message = "Promo deactivated."
			});
		}

		private static string? NormalizeOptional(
			string? value)
		{
			return string.IsNullOrWhiteSpace(value)
				? null
				: value.Trim();
		}
	}
}
