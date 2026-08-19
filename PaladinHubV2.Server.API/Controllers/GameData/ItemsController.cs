using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.ItemsService;

namespace PaladinHubV2.Server.API.Controllers.GameData
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/items")]
	public sealed class ItemsController : ControllerBase
	{
		private readonly IItemAdminService _items;

		public ItemsController(IItemAdminService items)
		{
			_items = items;
		}

		[HttpGet("create")]
		public IActionResult Create() => Ok(new Item());

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

			Item created = await _items.CreateAsync(item, cancellationToken);

			return CreatedAtAction(
				nameof(Details),
				new { id = created.Id },
				created);
		}

		[HttpGet("{id:int}/edit")]
		public Task<IActionResult> Edit(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			return GetItem(id, cancellationToken);
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
					message = "The route ID does not match the item ID."
				});
			}

			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			Item? updated = await _items.UpdateAsync(
				id,
				item,
				cancellationToken);

			return updated == null
				? NotFound(new { message = "Item not found." })
				: Ok(updated);
		}

		[HttpGet("{id:int}")]
		public Task<IActionResult> Details(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			return GetItem(id, cancellationToken);
		}

		[HttpGet("{id:int}/delete")]
		public Task<IActionResult> Delete(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			return GetItem(id, cancellationToken);
		}

		[HttpDelete("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			if (id <= 0)
			{
				return BadRequest(new { message = "Invalid item ID." });
			}

			bool deleted = await _items.DeleteAsync(id, cancellationToken);

			return deleted
				? NoContent()
				: NotFound(new { message = "Item not found." });
		}

		private async Task<IActionResult> GetItem(
			int id,
			CancellationToken cancellationToken)
		{
			if (id <= 0)
			{
				return BadRequest(new { message = "Invalid item ID." });
			}

			Item? item = await _items.GetAsync(id, cancellationToken);

			return item == null
				? NotFound(new { message = "Item not found." })
				: Ok(item);
		}
	}
}
