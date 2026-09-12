using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.TalentTrees;

namespace PaladinHubV2.Server.API.Controllers.Content.Talents
{
	[ApiController]
	[AllowAnonymous]
	[Route("talents")]
	public sealed class TalentsController : ControllerBase
	{
		private readonly TalentPageService _talentPages;

		public TalentsController(
			AppDbContext db,
			ITalentTreeService talentTrees)
		{
			_talentPages = new TalentPageService(
				db,
				talentTrees);
		}

		[HttpGet("{section:regex(^(holy|protection|retribution)$)}")]
		public async Task<IActionResult> SectionPage(
			[FromRoute] string section)
		{
			string? normalizedSection =
				_talentPages.NormalizeSection(section);

			if (normalizedSection == null)
			{
				return BadRequest(new
				{
					message = "Invalid paladin section."
				});
			}

			TalentSectionData data =
				await _talentPages.BuildSectionAsync(
					normalizedSection);

			return Ok(CreateSectionResponse(data));
		}

		[HttpGet("all/{section}")]
		public async Task<IActionResult> GetAll(
			[FromRoute] string section)
		{
			string? normalizedSection =
				_talentPages.NormalizeSection(section);

			if (normalizedSection == null)
			{
				return BadRequest(new
				{
					message =
						"Section must be holy, protection or retribution."
				});
			}

			TalentSectionData data =
				await _talentPages.BuildSectionAsync(
					normalizedSection);

			return Ok(CreateSectionResponse(data));
		}

		[HttpGet("tree/{key}")]
		public async Task<IActionResult> GetTree(
			[FromRoute] string key,
			[FromQuery] string? section = null)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				return BadRequest(new
				{
					message = "Talent tree key is required."
				});
			}

			string? normalizedSection =
				_talentPages.ResolveTreeSection(
					key,
					section);

			if (normalizedSection == null)
			{
				return BadRequest(new
				{
					message =
						"Section could not be resolved. Pass ?section=holy, protection or retribution."
				});
			}

			TalentTreeData? data =
				await _talentPages.BuildTreeAsync(
					normalizedSection,
					key);

			if (data == null)
			{
				return NotFound(new
				{
					message =
						$"No talent tree was found for key '{key}' in section '{normalizedSection}'."
				});
			}

			return Ok(new
			{
				section = data.Section,
				requestedKey = data.RequestedKey,
				resolvedKey = data.ResolvedKey,
				keys = data.Keys,
				pageTitle = data.Model.PageTitle,
				spells = data.Model.Spells,
				items = data.Model.Items,
				talentTrees = data.SelectedTrees
			});
		}

		private static object CreateSectionResponse(
			TalentSectionData data)
		{
			return new
			{
				section = data.Section,
				keys = data.Keys,
				pageTitle = data.Model.PageTitle,
				spells = data.Model.Spells,
				items = data.Model.Items,
				talentTrees = data.Model.TalentTrees
			};
		}
	}
}
