using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Promos;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/promo-codes")]
	public sealed class PromoCodesController : ControllerBase
	{
		private readonly IPromoCodeAdminService _promoCodes;

		public PromoCodesController(IPromoCodeAdminService promoCodes)
		{
			_promoCodes = promoCodes;
		}

		[HttpGet]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Index(CancellationToken cancellationToken)
		{
			return Ok(await _promoCodes.GetAllAsync(cancellationToken));
		}

		[HttpGet("create")]
		[HttpGet("~/Admin/PromoCodes/Create")]
		public IActionResult Create() => Ok(_promoCodes.BuildCreateModel());

		[HttpPost]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> CreateApi(
			[FromBody] PromoCode? model,
			CancellationToken cancellationToken)
			=> CreateCore(model, cancellationToken);

		[HttpPost("~/Admin/PromoCodes/Create")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> CreateLegacy(
			[FromForm] PromoCode? model,
			CancellationToken cancellationToken)
			=> CreateCore(model, cancellationToken);

		[HttpPost("{id}/deactivate")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeactivateApi([FromRoute] string id)
			=> DeactivateCore(id);

		[HttpPost("~/Admin/PromoCodes/Deactivate")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeactivateLegacy([FromForm] string id)
			=> DeactivateCore(id);

		private async Task<IActionResult> CreateCore(
			PromoCode? model,
			CancellationToken cancellationToken)
		{
			if (model == null)
				return BadRequest(new { message = "Promo code data is required." });

			IReadOnlyDictionary<string, string[]> errors =
				_promoCodes.NormalizeAndValidate(model);

			foreach ((string key, string[] messages) in errors)
			{
				foreach (string message in messages)
					ModelState.AddModelError(key, message);
			}

			if (!ModelState.IsValid)
				return ValidationProblem(ModelState);

			PromoCode? created = await _promoCodes.CreateAsync(
				model,
				cancellationToken);

			return created == null
				? Conflict(new { message = "Promo code already exists." })
				: CreatedAtAction(nameof(Index), new { id = created.Id }, created);
		}

		private async Task<IActionResult> DeactivateCore(string? id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return BadRequest(new { message = "Promo code ID is required." });

			string normalizedId = id.Trim();
			bool deactivated = await _promoCodes.DeactivateAsync(normalizedId);

			return deactivated
				? Ok(new
				{
					ok = true,
					id = normalizedId,
					isActive = false,
					message = "Promo deactivated."
				})
				: NotFound(new { message = "Promo code not found." });
		}
	}
}
