using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.SpellbookService;

namespace PaladinHubV2.Server.API.Controllers.GameData
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/spells")]
	public sealed class SpellsController : ControllerBase
	{
		private readonly SpellAdminService _spells;

		public SpellsController(SpellAdminService spells)
		{
			_spells = spells;
		}

		[HttpGet("create")]
		public IActionResult Create()
		{
			return Ok(new Spell());
		}

		[HttpGet("{id:int}/edit")]
		public Task<IActionResult> Edit(
			[FromRoute] int id,
			CancellationToken cancellationToken)
		{
			return Details(id, cancellationToken);
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

			Spell? spell =
				await _spells.GetAsync(id, cancellationToken);

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
	}
}
