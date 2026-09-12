using Microsoft.EntityFrameworkCore;
using PaladinHub.Models;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public sealed record TalentSectionData(
		string Section,
		string[] Keys,
		CombinedViewModel Model);

	public sealed record TalentTreeData(
		string Section,
		string RequestedKey,
		string ResolvedKey,
		string[] Keys,
		CombinedViewModel Model,
		IReadOnlyDictionary<string, TalentTreeViewModel> SelectedTrees);

	public sealed class TalentPageService
	{
		private readonly AppDbContext _db;
		private readonly ITalentTreeService _talentTrees;

		public TalentPageService(
			AppDbContext db,
			ITalentTreeService talentTrees)
		{
			_db = db;
			_talentTrees = talentTrees;
		}

		public string? NormalizeSection(string? section)
		{
			string normalized = (section ?? string.Empty)
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

		public string? ResolveTreeSection(
			string key,
			string? section)
		{
			string? sectionSource =
				!string.IsNullOrWhiteSpace(section)
					? section
					: key.Split(
						'-',
						StringSplitOptions.RemoveEmptyEntries)
						.FirstOrDefault();

			return NormalizeSection(sectionSource);
		}

		public async Task<TalentSectionData> BuildSectionAsync(
			string normalizedSection)
		{
			CombinedViewModel model =
				await BuildModelAsync(normalizedSection);

			string[] keys = BuildKeysForSection(
				model.TalentTrees,
				normalizedSection);

			return new TalentSectionData(
				normalizedSection,
				keys,
				model);
		}

		public async Task<TalentTreeData?> BuildTreeAsync(
			string normalizedSection,
			string requestedKey)
		{
			CombinedViewModel model =
				await BuildModelAsync(normalizedSection);

			string? resolvedKey = ResolveKey(
				requestedKey,
				model.TalentTrees.Keys,
				normalizedSection);

			if (resolvedKey == null)
			{
				return null;
			}

			string[] keys = BuildKeysForTree(
				model.TalentTrees,
				normalizedSection,
				resolvedKey);

			Dictionary<string, TalentTreeViewModel> selectedTrees =
				keys.ToDictionary(
					treeKey => treeKey,
					treeKey => model.TalentTrees[treeKey],
					StringComparer.OrdinalIgnoreCase);

			return new TalentTreeData(
				normalizedSection,
				requestedKey,
				resolvedKey,
				keys,
				model,
				selectedTrees);
		}

		private async Task<CombinedViewModel> BuildModelAsync(
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

		private static string[] BuildKeysForSection(
			IReadOnlyDictionary<string, TalentTreeViewModel> trees,
			string section)
		{
			string? classKey = trees.Keys.FirstOrDefault(key =>
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

			string? heroKey =
				FindHeroTree("herald") ??
				FindHeroTree("lightsmith") ??
				FindHeroTree("templar");

			string? specializationKey =
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
			string? classKey = trees.Keys.FirstOrDefault(key =>
				key.Equals(
					"paladin",
					StringComparison.OrdinalIgnoreCase));

			string? specializationKey =
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

			string requestedKey = rawKey
				.Trim()
				.ToLowerInvariant();

			List<string> keys = candidates.ToList();

			string? result = keys.FirstOrDefault(key =>
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

			string[] requestedTokens = requestedKey.Split(
				'-',
				StringSplitOptions.RemoveEmptyEntries |
				StringSplitOptions.TrimEntries);

			if (requestedTokens.Length > 0)
			{
				result = keys.FirstOrDefault(key =>
				{
					string[] candidateTokens = key.Split(
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
