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
	public sealed class ItemsController : ControllerBase
	{
		private readonly ItemAdminService _items;

		public ItemsController(AppDbContext db)
		{
			_items = new ItemAdminService(db);
		}

		[HttpGet("create")]
		public IActionResult Create()
		{
			return Ok(new Item());
		}

		[HttpGet("{id:int}/edit")]
		public Task<IActionResult> Edit(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			return GetItem(id, cancellationToken);
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

		private async Task<IActionResult> GetItem(
			int id,
			CancellationToken cancellationToken)
		{
			if (id <= 0)
			{
				return BadRequest(new
				{
					message = "Invalid item ID."
				});
			}

			Item? item =
				await _items.GetAsync(id, cancellationToken);

			if (item == null)
			{
				return NotFound(new
				{
					message = "Item not found."
				});
			}

			return Ok(item);
		}
	}
}
