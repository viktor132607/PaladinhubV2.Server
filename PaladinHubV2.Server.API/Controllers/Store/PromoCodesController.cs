using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
		private readonly PromoCodeAdminService _promoCodes;

		public PromoCodesController(
			AppDbContext db,
			IPromoCodeService promoCodes)
		{
			_promoCodes = new PromoCodeAdminService(
				db,
				promoCodes);
		}

		[HttpGet]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Index(
			CancellationToken cancellationToken)
		{
			List<PromoCode> promoCodes =
				await _promoCodes.ListAsync(cancellationToken);

			return Ok(promoCodes);
		}

		[HttpGet("create")]
		[HttpGet("~/Admin/PromoCodes/Create")]
		public IActionResult Create()
		{
			return Ok(PromoCodeAdminService.BuildCreateModel());
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

			PromoCreateResult result =
				await _promoCodes.CreateAsync(
					model,
					cancellationToken);

			if (result.Error == PromoCreateError.Validation)
			{
				foreach (
					PromoValidationError error in
					result.ValidationErrors ?? [])
				{
					ModelState.AddModelError(
						error.Field,
						error.Message);
				}

				return ValidationProblem(ModelState);
			}

			if (result.Error == PromoCreateError.Duplicate)
			{
				return Conflict(new
				{
					message = "Promo code already exists."
				});
			}

			PromoCode created = result.PromoCode!;

			return CreatedAtAction(
				nameof(Index),
				new { id = created.Id },
				created);
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

			string normalizedId = id.Trim();

			bool deactivated =
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
	}
}
