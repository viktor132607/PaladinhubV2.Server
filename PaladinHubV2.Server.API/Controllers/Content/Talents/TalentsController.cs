using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Models;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.TalentTrees;

namespace PaladinHubV2.Server.API.Controllers.Content.Talents
{
	[ApiController]
	[AllowAnonymous]
	[Route("talents")]
	public sealed class TalentsController : ControllerBase
	{
		private readonly AppDbContext _db;
		private readonly ITalentTreeService _talentTrees;

		public TalentsController(
			AppDbContext db,
			ITalentTreeService talentTrees)
		{
			_db = db;
			_talentTrees = talentTrees;
		}

		[HttpGet("{section:regex(^(holy|protection|retribution)$)}")]
		public async Task<IActionResult> SectionPage(
			[FromRoute] string section)
		{
			var normalizedSection = NormalizeSection(section);

			if (normalizedSection == null)
			{
				return BadRequest(new
				{
					message = "Invalid paladin section."
				});
			}

			var model = await BuildModel(normalizedSection);

			var keys = BuildKeysForSection(
				model.TalentTrees,
				normalizedSection);

			return Ok(CreateSectionResponse(
				normalizedSection,
				keys,
				model));
		}

		[HttpGet("all/{section}")]
		public async Task<IActionResult> GetAll(
			[FromRoute] string section)
		{
			var normalizedSection = NormalizeSection(section);

			if (normalizedSection == null)
			{
				return BadRequest(new
				{
					message =
						"Section must be holy, protection or retribution."
				});
			}

			var model = await BuildModel(normalizedSection);

			var keys = BuildKeysForSection(
				model.TalentTrees,
				normalizedSection);

			return Ok(CreateSectionResponse(
				normalizedSection,
				keys,
				model));
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

			var sectionSource = !string.IsNullOrWhiteSpace(section)
				? section
				: key.Split(
					'-',
					StringSplitOptions.RemoveEmptyEntries)
					.FirstOrDefault();

			var normalizedSection =
				NormalizeSection(sectionSource);

			if (normalizedSection == null)
			{
				return BadRequest(new
				{
					message =
							"Section could not be resolved. Pass ?section=holy, protection or retribution."
				});
			}

			var model = await BuildModel(normalizedSection);

			var resolvedKey = ResolveKey(
				key,
				model.TalentTrees.Keys,
				normalizedSection);

			if (resolvedKey == null)
			{
				return NotFound(new
				{
					message =
							$"No talent tree was found for key '{key}' in section '{normalizedSection}'."
				});
			}

			var keys = BuildKeysForTree(
				model.TalentTrees,
				normalizedSection,
				resolvedKey);

			var selectedTrees = keys.ToDictionary(
				treeKey => treeKey,
				treeKey => model.TalentTrees[treeKey],
				StringComparer.OrdinalIgnoreCase);

			return Ok(new
			{
				section = normalizedSection,
				requestedKey = key,
				resolvedKey,
				keys,
				pageTitle = model.PageTitle,
				spells = model.Spells,
				items = model.Items,
				talentTrees = selectedTrees
			});
		}

		private async Task<CombinedViewModel> BuildModel(
			string section)
		{
			var spells = await _db.Spells
				.AsNoTracking()
				.ToListAsync();

			var items = await _db.Items
				.AsNoTracking()
				.ToListAsync();

			var trees = await _talentTrees.GetTalentTrees(
				section,
				spells);

			return new CombinedViewModel
			{
				Section = TitleCase(section),
				PageTitle =
					$"{TitleCase(section)} Paladin – Talents",
				Spells = spells,
				Items = items,
				TalentTrees = trees
			};
		}

		private static object CreateSectionResponse(
			string section,
			string[] keys,
			CombinedViewModel model)
		{
			return new
			{
				section,
				keys,
				pageTitle = model.PageTitle,
				spells = model.Spells,
				items = model.Items,
				talentTrees = model.TalentTrees
			};
		}

		private static string[] BuildKeysForSection(
			IReadOnlyDictionary<string, TalentTreeViewModel> trees,
			string section)
		{
			var classKey = trees.Keys.FirstOrDefault(key =>
				key.Equals(
					"paladin",
					StringComparison.OrdinalIgnoreCase));

			string? FindHeroTree(string hero)
			{
				return trees.Keys.FirstOrDefault(key =>
						   key.Contains(
							   section,
							   StringComparison.OrdinalIgnoreCase) &&
						   key.EndsWith(
							   $"-{hero}",
							   StringComparison.OrdinalIgnoreCase))
					   ?? trees.Keys.FirstOrDefault(key =>
						   key.EndsWith(
							   $"-{hero}",
							   StringComparison.OrdinalIgnoreCase));
			}

			var heroKey =
				FindHeroTree("herald") ??
				FindHeroTree("lightsmith") ??
				FindHeroTree("templar");

			var specializationKey =
				trees.Keys.FirstOrDefault(key =>
					key.Equals(
						section,
						StringComparison.OrdinalIgnoreCase))
				?? trees.Keys.FirstOrDefault(key =>
					key.Contains(
						section,
						StringComparison.OrdinalIgnoreCase) &&
					!IsHeroKey(key));

			return NormalizeKeys(
				trees,
				classKey,
				heroKey,
				specializationKey);
		}

		private static string[] BuildKeysForTree(
			IReadOnlyDictionary<string, TalentTreeViewModel> trees,
			string section,
			string resolvedKey)
		{
			var classKey = trees.Keys.FirstOrDefault(key =>
				key.Equals(
					"paladin",
					StringComparison.OrdinalIgnoreCase));

			var specializationKey =
				trees.Keys.FirstOrDefault(key =>
					key.Equals(
						section,
						StringComparison.OrdinalIgnoreCase))
				?? trees.Keys.FirstOrDefault(key =>
					key.Contains(
						section,
						StringComparison.OrdinalIgnoreCase) &&
					!IsHeroKey(key));

			return NormalizeKeys(
				trees,
				classKey,
				resolvedKey,
				specializationKey);
		}

		private static string[] NormalizeKeys(
			IReadOnlyDictionary<string, TalentTreeViewModel> trees,
			params string?[] keys)
		{
			return keys
				.Where(key =>
					!string.IsNullOrWhiteSpace(key) &&
					trees.ContainsKey(key))
				.Select(key => key!)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		private static string? ResolveKey(
			string rawKey,
			IEnumerable<string> candidates,
			string section)
		{
			if (string.IsNullOrWhiteSpace(rawKey))
			{
				return null;
			}

			var requestedKey = rawKey
				.Trim()
				.ToLowerInvariant();

			var keys = candidates.ToList();

			var result = keys.FirstOrDefault(key =>
				key.Equals(
					requestedKey,
					StringComparison.OrdinalIgnoreCase));

			if (result != null)
			{
				return result;
			}

			result = keys.FirstOrDefault(key =>
				key.EndsWith(
					requestedKey,
					StringComparison.OrdinalIgnoreCase));

			if (result != null)
			{
				return result;
			}

			var requestedTokens = requestedKey.Split(
				'-',
				StringSplitOptions.RemoveEmptyEntries |
				StringSplitOptions.TrimEntries);

			if (requestedTokens.Length > 0)
			{
				result = keys.FirstOrDefault(key =>
				{
					var candidateTokens = key.Split(
						'-',
						StringSplitOptions.RemoveEmptyEntries |
							StringSplitOptions.TrimEntries);

					return requestedTokens.All(token =>
						candidateTokens.Contains(
							token,
							StringComparer.OrdinalIgnoreCase));
				});

				if (result != null)
				{
					return result;
				}
			}

			if (requestedKey is
				"herald" or
				"lightsmith" or
				"templar")
			{
				result = keys.FirstOrDefault(key =>
					key.Contains(
						section,
						StringComparison.OrdinalIgnoreCase) &&
					key.EndsWith(
						$"-{requestedKey}",
						StringComparison.OrdinalIgnoreCase));

				if (result != null)
				{
					return result;
				}

				return keys.FirstOrDefault(key =>
					key.EndsWith(
						$"-{requestedKey}",
						StringComparison.OrdinalIgnoreCase));
			}

			return null;
		}

		private static string? NormalizeSection(
			string? section)
		{
			var normalized = (section ?? string.Empty)
				.Trim()
				.ToLowerInvariant();

			return normalized switch
			{
				"holy" or "holly" => "holy",
				"prot" or "protection" => "protection",
				"ret" or "retri" or "retribution" =>
					"retribution",
				_ => null
			};
		}

		private static string TitleCase(string value)
		{
			return string.IsNullOrWhiteSpace(value)
				? value
				: char.ToUpperInvariant(value[0]) +
				  value[1..];
		}

		private static bool IsHeroKey(string key)
		{
			return key.EndsWith(
					   "-herald",
					   StringComparison.OrdinalIgnoreCase) ||
				   key.EndsWith(
					   "-lightsmith",
					   StringComparison.OrdinalIgnoreCase) ||
				   key.EndsWith(
					   "-templar",
					   StringComparison.OrdinalIgnoreCase);
		}
	}
}
