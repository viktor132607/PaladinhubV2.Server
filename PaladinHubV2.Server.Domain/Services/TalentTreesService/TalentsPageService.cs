using Microsoft.EntityFrameworkCore;
using PaladinHub.Models;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public sealed class TalentsPageService : ITalentsPageService
	{
		private readonly AppDbContext _db;
		private readonly ITalentTreeService _talentTrees;

		public TalentsPageService(
			AppDbContext db,
			ITalentTreeService talentTrees)
		{
			_db = db;
			_talentTrees = talentTrees;
		}

		public string? NormalizeSection(string? section)
		{
			string normalized = (section ?? string.Empty).Trim().ToLowerInvariant();
			return normalized switch
			{
				"holy" or "holly" => "holy",
				"prot" or "protection" => "protection",
				"ret" or "retri" or "retribution" => "retribution",
				_ => null
			};
		}

		public async Task<TalentsSectionResult> GetSectionAsync(
			string section,
			CancellationToken cancellationToken = default)
		{
			CombinedViewModel model = await BuildModel(section, cancellationToken);
			return new TalentsSectionResult(
				model,
				BuildKeysForSection(model.TalentTrees, section));
		}

		public async Task<TalentsTreeResult?> GetTreeAsync(
			string section,
			string key,
			CancellationToken cancellationToken = default)
		{
			CombinedViewModel model = await BuildModel(section, cancellationToken);
			string? resolvedKey = ResolveKey(key, model.TalentTrees.Keys, section);
			if (resolvedKey == null)
			{
				return null;
			}

			string[] keys = BuildKeysForTree(
				model.TalentTrees,
				section,
				resolvedKey);

			Dictionary<string, TalentTreeViewModel> selectedTrees = keys.ToDictionary(
				treeKey => treeKey,
				treeKey => model.TalentTrees[treeKey],
				StringComparer.OrdinalIgnoreCase);

			return new TalentsTreeResult(model, resolvedKey, keys, selectedTrees);
		}

		private async Task<CombinedViewModel> BuildModel(
			string section,
			CancellationToken cancellationToken)
		{
			var spells = await _db.Spells.AsNoTracking().ToListAsync(cancellationToken);
			var items = await _db.Items.AsNoTracking().ToListAsync(cancellationToken);
			var trees = await _talentTrees.GetTalentTrees(section, spells);

			return new CombinedViewModel
			{
				Section = TitleCase(section),
				PageTitle = $"{TitleCase(section)} Paladin – Talents",
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
				key.Equals("paladin", StringComparison.OrdinalIgnoreCase));

			string? FindHeroTree(string hero)
			{
				return trees.Keys.FirstOrDefault(key =>
						key.Contains(section, StringComparison.OrdinalIgnoreCase) &&
						key.EndsWith($"-{hero}", StringComparison.OrdinalIgnoreCase))
					?? trees.Keys.FirstOrDefault(key =>
						key.EndsWith($"-{hero}", StringComparison.OrdinalIgnoreCase));
			}

			string? heroKey = FindHeroTree("herald") ??
				FindHeroTree("lightsmith") ??
				FindHeroTree("templar");

			string? specializationKey = trees.Keys.FirstOrDefault(key =>
				key.Equals(section, StringComparison.OrdinalIgnoreCase))
				?? trees.Keys.FirstOrDefault(key =>
					key.Contains(section, StringComparison.OrdinalIgnoreCase) && !IsHeroKey(key));

			return NormalizeKeys(trees, classKey, heroKey, specializationKey);
		}

		private static string[] BuildKeysForTree(
			IReadOnlyDictionary<string, TalentTreeViewModel> trees,
			string section,
			string resolvedKey)
		{
			string? classKey = trees.Keys.FirstOrDefault(key =>
				key.Equals("paladin", StringComparison.OrdinalIgnoreCase));

			string? specializationKey = trees.Keys.FirstOrDefault(key =>
				key.Equals(section, StringComparison.OrdinalIgnoreCase))
				?? trees.Keys.FirstOrDefault(key =>
					key.Contains(section, StringComparison.OrdinalIgnoreCase) && !IsHeroKey(key));

			return NormalizeKeys(trees, classKey, resolvedKey, specializationKey);
		}

		private static string[] NormalizeKeys(
			IReadOnlyDictionary<string, TalentTreeViewModel> trees,
			params string?[] keys)
		{
			return keys
				.Where(key => !string.IsNullOrWhiteSpace(key) && trees.ContainsKey(key))
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

			string requestedKey = rawKey.Trim().ToLowerInvariant();
			List<string> keys = candidates.ToList();

			string? result = keys.FirstOrDefault(key =>
				key.Equals(requestedKey, StringComparison.OrdinalIgnoreCase));
			if (result != null) return result;

			result = keys.FirstOrDefault(key =>
				key.EndsWith(requestedKey, StringComparison.OrdinalIgnoreCase));
			if (result != null) return result;

			string[] requestedTokens = requestedKey.Split(
				'-',
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			if (requestedTokens.Length > 0)
			{
				result = keys.FirstOrDefault(key =>
				{
					string[] candidateTokens = key.Split(
						'-',
						StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
					return requestedTokens.All(token =>
						candidateTokens.Contains(token, StringComparer.OrdinalIgnoreCase));
				});
				if (result != null) return result;
			}

			if (requestedKey is "herald" or "lightsmith" or "templar")
			{
				result = keys.FirstOrDefault(key =>
					key.Contains(section, StringComparison.OrdinalIgnoreCase) &&
					key.EndsWith($"-{requestedKey}", StringComparison.OrdinalIgnoreCase));
				if (result != null) return result;

				return keys.FirstOrDefault(key =>
					key.EndsWith($"-{requestedKey}", StringComparison.OrdinalIgnoreCase));
			}

			return null;
		}

		private static string TitleCase(string value)
		{
			return string.IsNullOrWhiteSpace(value)
				? value
				: char.ToUpperInvariant(value[0]) + value[1..];
		}

		private static bool IsHeroKey(string key)
		{
			return key.EndsWith("-herald", StringComparison.OrdinalIgnoreCase) ||
				key.EndsWith("-lightsmith", StringComparison.OrdinalIgnoreCase) ||
				key.EndsWith("-templar", StringComparison.OrdinalIgnoreCase);
		}
	}
}
