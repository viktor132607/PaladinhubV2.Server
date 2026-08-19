using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.GameData;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.SpellbookService;

namespace PaladinHubV2.Server.API.Controllers.GameData
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/spells")]
	public sealed class SpellsController : ControllerBase
	{
		private readonly ISpellAdminService _spells;

		public SpellsController(ISpellAdminService spells)
		{
			_spells = spells;
		}

		[HttpGet("create")]
		public IActionResult Create() => Ok(new Spell());

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(
			[FromBody] SpellAdminRequest? spell,
			CancellationToken cancellationToken)
		{
			if (spell == null)
			{
				return BadRequest(
					new { message = "Spell data is required." });
			}

			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			Spell created =
				await _spells.CreateAsync(
					spell,
					cancellationToken);

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
			return Details(id, cancellationToken);
		}

		[HttpPut("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(
			[FromRoute] int id,
			[FromBody] SpellAdminRequest? spell,
			CancellationToken cancellationToken)
		{
			if (id <= 0)
			{
				return BadRequest(
					new { message = "Invalid spell ID." });
			}

			if (spell == null)
			{
				return BadRequest(
					new { message = "Spell data is required." });
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

			Spell? updated =
				await _spells.UpdateAsync(
					id,
					spell,
					cancellationToken);

			return updated == null
				? NotFound(new { message = "Spell not found." })
				: Ok(updated);
		}

		[HttpGet("{id:int}")]
		public async Task<IActionResult> Details(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			if (id <= 0)
			{
				return BadRequest(
					new { message = "Invalid spell ID." });
			}

			Spell? spell =
				await _spells.GetAsync(
					id,
					cancellationToken);

			return spell == null
				? NotFound(new { message = "Spell not found." })
				: Ok(spell);
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
				return BadRequest(
					new { message = "Invalid spell ID." });
			}

			bool deleted =
				await _spells.DeleteAsync(
					id,
					cancellationToken);

			return deleted
				? NoContent()
				: NotFound(new { message = "Spell not found." });
		}
	}
}
