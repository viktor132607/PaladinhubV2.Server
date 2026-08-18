using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.API.Controllers.GameData
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/items")]
	public sealed class ItemsController : ControllerBase
	{
		private readonly AppDbContext _db;

		public ItemsController(AppDbContext db)
		{
			_db = db;
		}

		[HttpGet("create")]
		public IActionResult Create()
		{
			return Ok(new Item());
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

			item.Id = 0;
			item.Name = item.Name.Trim();
			item.Icon = NormalizeOptional(item.Icon);
			item.SecondIcon = NormalizeOptional(item.SecondIcon);
			item.Description = NormalizeOptional(item.Description);
			item.Url = NormalizeOptional(item.Url);
			item.Quality = NormalizeOptional(item.Quality);

			_db.Items.Add(item);
			await _db.SaveChangesAsync(cancellationToken);

			return CreatedAtAction(
				nameof(Details),
				new { id = item.Id },
				item);
		}

		[HttpGet("{id:int}/edit")]
		public async Task<IActionResult> Edit(
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

			var item = await _db.Items
				.AsNoTracking()
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);

			if (item == null)
			{
				return NotFound(new
				{
					message = "Item not found."
				});
			}

			return Ok(item);
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

			var existing = await _db.Items
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);

			if (existing == null)
			{
				return NotFound(new
				{
					message = "Item not found."
				});
			}

			existing.Name = item.Name.Trim();
			existing.Icon = NormalizeOptional(item.Icon);
			existing.SecondIcon = NormalizeOptional(item.SecondIcon);
			existing.Description = NormalizeOptional(item.Description);
			existing.Url = NormalizeOptional(item.Url);
			existing.ItemLevel = item.ItemLevel;
			existing.RequiredLevel = item.RequiredLevel;
			existing.Quality = NormalizeOptional(item.Quality);

			await _db.SaveChangesAsync(cancellationToken);

			return Ok(existing);
		}

		[HttpGet("{id:int}")]
		public async Task<IActionResult> Details(
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

			var item = await _db.Items
				.AsNoTracking()
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);

			if (item == null)
			{
				return NotFound(new
				{
					message = "Item not found."
				});
			}

			return Ok(item);
		}

		[HttpGet("{id:int}/delete")]
		public async Task<IActionResult> Delete(
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

			var item = await _db.Items
				.AsNoTracking()
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);

			if (item == null)
			{
				return NotFound(new
				{
					message = "Item not found."
				});
			}

			return Ok(item);
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

			var item = await _db.Items
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);

			if (item == null)
			{
				return NotFound(new
				{
					message = "Item not found."
				});
			}

			_db.Items.Remove(item);
			await _db.SaveChangesAsync(cancellationToken);

			return NoContent();
		}

		private static string? NormalizeOptional(string? value)
		{
			return string.IsNullOrWhiteSpace(value)
				? null
				: value.Trim();
		}
	}
}
