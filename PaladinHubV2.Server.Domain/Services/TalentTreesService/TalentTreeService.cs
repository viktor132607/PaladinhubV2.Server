using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Talents;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public class TalentTreeService : ITalentTreeService
	{
		private readonly IEnumerable<ISpecializationTreeBuilder> _specBuilders;
		private readonly IHeroTalentTreesService _heroTrees;
		private readonly IClassTreeBuilder _classTree;
		private readonly ITalentTreeAdminService _adminStates;

		public TalentTreeService(
			IEnumerable<ISpecializationTreeBuilder> specBuilders,
			IHeroTalentTreesService heroTrees,
			IClassTreeBuilder classTree,
			ITalentTreeAdminService adminStates)
		{
			_specBuilders = specBuilders ?? Array.Empty<ISpecializationTreeBuilder>();
			_heroTrees = heroTrees ?? throw new ArgumentNullException(nameof(heroTrees));
			_classTree = classTree ?? throw new ArgumentNullException(nameof(classTree));
			_adminStates = adminStates ?? throw new ArgumentNullException(nameof(adminStates));
		}

		public async Task<Dictionary<string, TalentTreeViewModel>> GetTalentTrees(string section, List<Spell> spells)
		{
			var dict = new Dictionary<string, TalentTreeViewModel>(StringComparer.OrdinalIgnoreCase);
			spells ??= new List<Spell>();
			string sec = Normalize(section);

			var classVm = _classTree.BuildTree(spells);
			if (classVm != null && !string.IsNullOrWhiteSpace(classVm.Key))
				dict[classVm.Key] = classVm;

			var specBuilder = _specBuilders.FirstOrDefault(b =>
				string.Equals(Normalize(b.BaseKey), sec, StringComparison.OrdinalIgnoreCase));

			if (specBuilder != null)
			{
				var specVm = specBuilder.BuildTree(spells);
				if (specVm != null && !string.IsNullOrWhiteSpace(specVm.Key))
					dict[specVm.Key] = specVm;
			}

			var heroTrees = _heroTrees.GetHeroTrees(sec, spells);
			if (heroTrees != null)
			{
				foreach (var kv in heroTrees)
				{
					if (!string.IsNullOrWhiteSpace(kv.Key) && kv.Value != null)
						dict[kv.Key] = kv.Value;
				}
			}

			foreach (var tree in dict.Values)
				await ApplyAdminStatesAsync(tree);

			return dict;
		}

		public async Task<TalentTreeViewModel?> GetTalentTree(string key, string section, List<Spell> spells)
		{
			if (string.IsNullOrWhiteSpace(key))
				return null;

			var all = await GetTalentTrees(section, spells);
			return all.TryGetValue(key, out var vm) ? vm : null;
		}

		public async Task SaveActiveStatesAsync(string key, List<NodeState> nodes)
		{
			if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException(nameof(key));
			var dict = (nodes ?? new List<NodeState>())
				.Where(n => !string.IsNullOrWhiteSpace(n.Id))
				.ToDictionary(n => n.Id!, n => n.Active, StringComparer.OrdinalIgnoreCase);
			await _adminStates.SaveStatesAsync(key, dict);
		}

		private static string Normalize(string s)
			=> (s ?? string.Empty).Trim().ToLowerInvariant();

		private async Task ApplyAdminStatesAsync(TalentTreeViewModel tree)
		{
			if (tree == null || string.IsNullOrWhiteSpace(tree.Key) || tree.Nodes == null || tree.Nodes.Count == 0)
				return;

			var states = await _adminStates.GetStatesAsync(tree.Key);
			if (states == null || states.Count == 0) return;

			foreach (var n in tree.Nodes)
			{
				if (n == null || string.IsNullOrWhiteSpace(n.Id)) continue;
				if (states.TryGetValue(n.Id, out var isActive))
					n.Active = isActive;
			}
		}
	}
}
