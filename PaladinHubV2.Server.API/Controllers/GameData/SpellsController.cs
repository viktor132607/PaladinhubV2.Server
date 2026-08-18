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
	[Route("Admin/api/spells")]
	public sealed class SpellsController : ControllerBase
	{
		private readonly AppDbContext _db;

		public SpellsController(AppDbContext db)
		{
			_db = db;
		}

		[HttpGet("create")]
		public IActionResult Create()
		{
			return Ok(new Spell());
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(
			[FromBody] Spell? spell,
			CancellationToken cancellationToken)
		{
			if (spell == null)
			{
				return BadRequest(new
				{
					message = "Spell data is required."
				});
			}

			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			spell.Id = 0;
			NormalizeSpell(spell);

			_db.Spells.Add(spell);

			await _db.SaveChangesAsync(cancellationToken);

			return CreatedAtAction(
				nameof(Details),
				new { id = spell.Id },
				spell);
		}

		[HttpGet("{id:int}/edit")]
		public Task<IActionResult> Edit(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			return Details(id, cancellationToken);
		}

		[HttpPut("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(
			[FromRoute] int id,
			[FromBody] Spell? spell,
			CancellationToken cancellationToken)
		{
			if (id <= 0)
			{
				return BadRequest(new
				{
					message = "Invalid spell ID."
				});
			}

			if (spell == null)
			{
				return BadRequest(new
				{
					message = "Spell data is required."
				});
			}

			if (id != spell.Id)
			{
				return BadRequest(new
				{
					message =
							"The route ID does not match the spell ID."
				});
			}

			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			var existing = await _db.Spells
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);

			if (existing == null)
			{
				return NotFound(new
				{
					message = "Spell not found."
				});
			}

			existing.Name = spell.Name;
			existing.Icon = spell.Icon;
			existing.Description = spell.Description;
			existing.Url = spell.Url;
			existing.Quality = spell.Quality;

			NormalizeSpell(existing);

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
					message = "Invalid spell ID."
				});
			}

			var spell = await _db.Spells
				.AsNoTracking()
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);

			if (spell == null)
			{
				return NotFound(new
				{
					message = "Spell not found."
				});
			}

			return Ok(spell);
		}

		[HttpGet("{id:int}/delete")]
		public Task<IActionResult> Delete(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			return Details(id, cancellationToken);
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
					message = "Invalid spell ID."
				});
			}

			var spell = await _db.Spells
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);

			if (spell == null)
			{
				return NotFound(new
				{
					message = "Spell not found."
				});
			}

			_db.Spells.Remove(spell);

			await _db.SaveChangesAsync(cancellationToken);

			return NoContent();
		}

		private static void NormalizeSpell(Spell spell)
		{
			spell.Name = spell.Name.Trim();
			spell.Icon = NormalizeOptional(spell.Icon);
			spell.Description = NormalizeOptional(spell.Description);
			spell.Url = NormalizeOptional(spell.Url);
			spell.Quality = string.IsNullOrWhiteSpace(spell.Quality)
				? "spell"
				: spell.Quality.Trim().ToLowerInvariant();
		}

		private static string? NormalizeOptional(string? value)
		{
			return string.IsNullOrWhiteSpace(value)
				? null
				: value.Trim();
		}
	}
}
