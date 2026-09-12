using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.ItemsService;

namespace PaladinHubV2.Server.API.Controllers.GameData
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/items")]
	public sealed class ItemMutationsController : ControllerBase
	{
		private readonly ItemAdminService _items;

		public ItemMutationsController(AppDbContext db)
		{
			_items = new ItemAdminService(db);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(
			[FromBody] Item item,
			CancellationToken cancellationToken)
		{
			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			ItemMutationResult result =
				await _items.CreateAsync(
					item,
					cancellationToken);

			IActionResult? error = MapMutationError(result.Error);

			if (error != null)
			{
				return error;
			}

			return CreatedAtAction(
				"Details",
				"Items",
				new { id = result.Item!.Id },
				result.Item);
		}

		[HttpPut("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(
			[FromRoute] int id,
			[FromBody] Item item,
			CancellationToken cancellationToken)
		{
			if (id <= 0 || id != item.Id)
			{
				return BadRequest(new
				{
					message =
						"The route ID does not match the item ID."
				});
			}

			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			ItemMutationResult result =
				await _items.UpdateAsync(
					id,
					item,
					cancellationToken);

			if (result.Error == ItemMutationError.NotFound)
			{
				return NotFound(new
				{
					message = "Item not found."
				});
			}

			IActionResult? error = MapMutationError(result.Error);

			if (error != null)
			{
				return error;
			}

			return Ok(result.Item);
		}

		[HttpDelete("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			if (id <= 0)
			{
				return BadRequest(new
				{
					message = "Invalid item ID."
				});
			}

			bool deleted =
				await _items.DeleteAsync(id, cancellationToken);

			if (!deleted)
			{
				return NotFound(new
				{
					message = "Item not found."
				});
			}

			return NoContent();
		}

		private IActionResult? MapMutationError(
			ItemMutationError error)
		{
			string? message = error switch
			{
				ItemMutationError.None => null,
				ItemMutationError.InvalidCategory =>
					"Choose an active category.",
				ItemMutationError.InvalidDiscipline =>
					"Choose an active class or specialization.",
				ItemMutationError.InvalidPatch =>
					"Select an active patch.",
				ItemMutationError.InvalidRarity =>
					"Choose an active rarity.",
				ItemMutationError.InvalidMedia =>
					"Choose an active image from the media library.",
				ItemMutationError.InvalidTags =>
					"Choose existing active tags (up to 100).",
				_ => null
			};

			return message == null
				? null
				: BadRequest(new { message });
		}
	}
}
