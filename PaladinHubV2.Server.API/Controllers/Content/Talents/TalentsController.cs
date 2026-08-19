using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Domain.Services.TalentTrees;

namespace PaladinHubV2.Server.API.Controllers.Content.Talents
{
	[ApiController]
	[AllowAnonymous]
	[Route("talents")]
	public sealed class TalentsController : ControllerBase
	{
		private readonly ITalentsPageService _talents;

		public TalentsController(ITalentsPageService talents)
		{
			_talents = talents;
		}

		[HttpGet("{section:regex(^(holy|protection|retribution)$)}")]
		public async Task<IActionResult> SectionPage(
			[FromRoute] string section,
			CancellationToken cancellationToken = default)
		{
			string? normalizedSection = _talents.NormalizeSection(section);
			if (normalizedSection == null)
			{
				return BadRequest(new { message = "Invalid paladin section." });
			}

			TalentsSectionResult result = await _talents.GetSectionAsync(
				normalizedSection,
				cancellationToken);

			return Ok(new
			{
				section = normalizedSection,
				keys = result.Keys,
				pageTitle = result.Model.PageTitle,
				spells = result.Model.Spells,
				items = result.Model.Items,
				talentTrees = result.Model.TalentTrees
			});
		}

		[HttpGet("all/{section}")]
		public async Task<IActionResult> GetAll(
			[FromRoute] string section,
			CancellationToken cancellationToken = default)
		{
			string? normalizedSection = _talents.NormalizeSection(section);
			if (normalizedSection == null)
			{
				return BadRequest(new
				{
					message = "Section must be holy, protection or retribution."
				});
			}

			TalentsSectionResult result = await _talents.GetSectionAsync(
				normalizedSection,
				cancellationToken);

			return Ok(new
			{
				section = normalizedSection,
				keys = result.Keys,
				pageTitle = result.Model.PageTitle,
				spells = result.Model.Spells,
				items = result.Model.Items,
				talentTrees = result.Model.TalentTrees
			});
		}

		[HttpGet("tree/{key}")]
		public async Task<IActionResult> GetTree(
			[FromRoute] string key,
			[FromQuery] string? section = null,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				return BadRequest(new { message = "Talent tree key is required." });
			}

			string? sectionSource = !string.IsNullOrWhiteSpace(section)
				? section
				: key.Split(
					'-',
					StringSplitOptions.RemoveEmptyEntries)
					.FirstOrDefault();

			string? normalizedSection = _talents.NormalizeSection(sectionSource);
			if (normalizedSection == null)
			{
				return BadRequest(new
				{
					message = "Section could not be resolved. Pass ?section=holy, protection or retribution."
				});
			}

			TalentsTreeResult? result = await _talents.GetTreeAsync(
				normalizedSection,
				key,
				cancellationToken);

			if (result == null)
			{
				return NotFound(new
				{
					message = $"No talent tree was found for key '{key}' in section '{normalizedSection}'."
				});
			}

			return Ok(new
			{
				section = normalizedSection,
				requestedKey = key,
				resolvedKey = result.ResolvedKey,
				keys = result.Keys,
				pageTitle = result.Model.PageTitle,
				spells = result.Model.Spells,
				items = result.Model.Items,
				talentTrees = result.TalentTrees
			});
		}
	}
}
