using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.SpellbookService;

namespace PaladinHubV2.Server.API.Controllers.GameData
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/spells")]
	public sealed class SpellMutationsController : ControllerBase
	{
		private readonly SpellAdminService _spells;

		public SpellMutationsController(SpellAdminService spells)
		{
			_spells = spells;
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

			SpellMutationResult result =
				await _spells.CreateAsync(
					spell,
					cancellationToken);

			IActionResult? error = MapMutationError(result.Error);
			if (error != null)
			{
				return error;
			}

			return CreatedAtAction(
				"Details",
				"Spells",
				new { id = result.Spell!.Id },
				result.Spell);
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
				return InvalidId();
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

			SpellMutationResult result =
				await _spells.UpdateAsync(
					id,
					spell,
					cancellationToken);

			if (result.Error == SpellMutationError.NotFound)
			{
				return SpellNotFound();
			}

			IActionResult? error = MapMutationError(result.Error);
			if (error != null)
			{
				return error;
			}

			return Ok(result.Spell);
		}

		[HttpDelete("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteConfirmed(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			if (id <= 0)
			{
				return InvalidId();
			}

			bool deleted =
				await _spells.DeleteAsync(id, cancellationToken);

			if (!deleted)
			{
				return SpellNotFound();
			}

			return NoContent();
		}

		private IActionResult? MapMutationError(
			SpellMutationError error)
		{
			return error switch
			{
				SpellMutationError.None => null,
				SpellMutationError.InvalidCategory =>
					BadRequest(new { message = "Choose an active category." }),
				SpellMutationError.InvalidDiscipline =>
					BadRequest(new
					{
						message = "Choose an active class or specialization."
					}),
				SpellMutationError.InvalidPatch =>
					BadRequest(new { message = "Select an active patch." }),
				SpellMutationError.InvalidMedia =>
					BadRequest(new
					{
						message = "Choose an active image from the media library."
					}),
				SpellMutationError.InvalidTags =>
					BadRequest(new
					{
						message = "Choose existing active tags (up to 100)."
					}),
				SpellMutationError.InvalidRecordType =>
					BadRequest(new
					{
						message = "Choose an existing type. Refresh the type list if it was changed."
					}),
				SpellMutationError.RecordTypeChanged =>
					Conflict(new
					{
						message = "This type changed. Refresh the type list and try again."
					}),
				_ => null
			};
		}

		private IActionResult InvalidId()
		{
			return BadRequest(new
			{
				message = "Invalid spell ID."
			});
		}

		private IActionResult SpellNotFound()
		{
			return NotFound(new
			{
				message = "Spell not found."
			});
		}
	}
}
