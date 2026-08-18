using System;
using System.Collections.Generic;
using System.Linq;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Talents;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public class HeroTalentTreesService : IHeroTalentTreesService
	{
		private static string Slug(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return "node";
			var chars = s.ToLowerInvariant().Select(ch =>
				(ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') ? ch : '-').ToArray();
			var lower = new string(chars);
			while (lower.Contains("--")) lower = lower.Replace("--", "-");
			return lower.Trim('-');
		}

		private static string IdAt(List<TalentNodeViewModel> nodes, int col, int row)
			=> nodes.First(n => n.Col == col && n.Row == row).Id;

		public Dictionary<string, TalentTreeViewModel> GetHeroTrees(string specialization, List<Spell> spells)
		{
			var dict = new Dictionary<string, TalentTreeViewModel>(StringComparer.OrdinalIgnoreCase);
			var spec = (specialization ?? "").Trim().ToLowerInvariant();

			if (spec == "holy")
			{
				dict["holy-herald"] = BuildHerald("holy-herald");
				dict["holy-lightsmith"] = BuildLightsmith("holy-lightsmith");
			}
			else if (spec == "protection")
			{
				dict["protection-lightsmith"] = BuildLightsmith("protection-lightsmith");
				dict["protection-templar"] = BuildTemplar("protection-templar");
			}
			else if (spec == "retribution")
			{
				dict["retribution-herald"] = BuildHerald("retribution-herald");
				dict["retribution-templar"] = BuildTemplar("retribution-templar");
			}

			return dict;
		}

		public TalentTreeViewModel? GetHeroTree(string specialization, string key, List<Spell> spells)
		{
			var all = GetHeroTrees(specialization, spells);
			return all.TryGetValue(key ?? "", out var vm) ? vm : null;
		}

		// ====== BUILDERS ======
		private TalentTreeViewModel BuildHerald(string key)
		{
			var nodes = new List<TalentNodeViewModel>();
			var idCount = new Dictionary<string, int>();

			TalentNodeViewModel Add(string name, int col, int row, string shape = "circle")
			{
				var baseId = Slug(name);
				if (!idCount.ContainsKey(baseId)) idCount[baseId] = 0;
				idCount[baseId]++;
				var id = idCount[baseId] == 1 ? baseId : $"{baseId}-{idCount[baseId]}";
				var n = new TalentNodeViewModel { Id = id, SpellName = name, Col = col, Row = row, Shape = shape, Active = true };
				nodes.Add(n); return n;
			}

			// Структура от статичния partial "Herald of the Sun"
			// Row 1
			Add("Dawnlight", 2, 1);
			// Row 2
			Add("Gleaming Rays", 1, 2);
			Add("Eternal Flame", 2, 2);
			Add("Luminosity", 3, 2);
			// Row 3
			Add("Will of the Dawn", 1, 3, "square");
			Add("Blessing of Anshe", 2, 3, "hexagon");
			Add("Sun Sear", 3, 3);
			// Row 4
			Add("Aurora", 1, 4);
			Add("Solar Grace", 2, 4);
			Add("Second Sunrise", 3, 4);
			// Row 5
			Add("Dawnlight", 2, 5);

			var edges = new List<TalentEdgeViewModel>
			{
				new() { FromId = IdAt(nodes,2,1), ToId = IdAt(nodes,1,2) },
				new() { FromId = IdAt(nodes,2,1), ToId = IdAt(nodes,2,2) },
				new() { FromId = IdAt(nodes,2,1), ToId = IdAt(nodes,3,2) },

				new() { FromId = IdAt(nodes,1,2), ToId = IdAt(nodes,1,3) },
				new() { FromId = IdAt(nodes,2,2), ToId = IdAt(nodes,2,3) },
				new() { FromId = IdAt(nodes,3,2), ToId = IdAt(nodes,3,3) },

				new() { FromId = IdAt(nodes,1,3), ToId = IdAt(nodes,1,4) },
				new() { FromId = IdAt(nodes,2,3), ToId = IdAt(nodes,2,4) },
				new() { FromId = IdAt(nodes,3,3), ToId = IdAt(nodes,3,4) },

				new() { FromId = IdAt(nodes,1,4), ToId = IdAt(nodes,2,5) },
				new() { FromId = IdAt(nodes,2,4), ToId = IdAt(nodes,2,5) },
				new() { FromId = IdAt(nodes,3,4), ToId = IdAt(nodes,2,5) },
			};

			return new TalentTreeViewModel
			{
				Key = key,
				Title = "Herald of the Sun",
				IsHero = true,
				MaxPoints = 10,
				Nodes = nodes,
				Edges = edges
			};
		}

		private TalentTreeViewModel BuildLightsmith(string key)
		{
			var nodes = new List<TalentNodeViewModel>();
			var idCount = new Dictionary<string, int>();

			TalentNodeViewModel Add(string name, int col, int row, string shape = "circle")
			{
				var baseId = Slug(name);
				if (!idCount.ContainsKey(baseId)) idCount[baseId] = 0;
				idCount[baseId]++;
				var id = idCount[baseId] == 1 ? baseId : $"{baseId}-{idCount[baseId]}";
				var n = new TalentNodeViewModel { Id = id, SpellName = name, Col = col, Row = row, Shape = shape, Active = true };
				nodes.Add(n); return n;
			}

			// Структура от статичния partial "Lightsmith"
			// Row 1
			Add("Holy Bulwark", 2, 1, "square");
			// Row 2
			Add("Rite of Sanctification", 1, 2, "hexagon");
			Add("Solidarity", 2, 2);
			Add("Divine Guidance", 3, 2, "hexagon");
			// Row 3
			Add("Laying Down Arms", 1, 3);
			Add("Divine Inspiration", 2, 3, "hexagon");
			Add("Authoritative Rebuke", 3, 3, "hexagon");
			// Row 4
			Add("Shared Resolve", 1, 4);
			Add("Valiance", 2, 4);
			Add("Hammer and Anvil", 3, 4);
			// Row 5
			Add("Blessing of the Forge", 2, 5);

			var edges = new List<TalentEdgeViewModel>
			{
				new() { FromId = IdAt(nodes,2,1), ToId = IdAt(nodes,1,2) },
				new() { FromId = IdAt(nodes,2,1), ToId = IdAt(nodes,2,2) },
				new() { FromId = IdAt(nodes,2,1), ToId = IdAt(nodes,3,2) },

				new() { FromId = IdAt(nodes,1,2), ToId = IdAt(nodes,1,3) },
				new() { FromId = IdAt(nodes,2,2), ToId = IdAt(nodes,2,3) },
				new() { FromId = IdAt(nodes,3,2), ToId = IdAt(nodes,3,3) },

				new() { FromId = IdAt(nodes,1,3), ToId = IdAt(nodes,1,4) },
				new() { FromId = IdAt(nodes,2,3), ToId = IdAt(nodes,2,4) },
				new() { FromId = IdAt(nodes,3,3), ToId = IdAt(nodes,3,4) },

				new() { FromId = IdAt(nodes,1,4), ToId = IdAt(nodes,2,5) },
				new() { FromId = IdAt(nodes,2,4), ToId = IdAt(nodes,2,5) },
				new() { FromId = IdAt(nodes,3,4), ToId = IdAt(nodes,2,5) },
			};

			return new TalentTreeViewModel
			{
				Key = key,
				Title = "Lightsmith",
				IsHero = true,
				MaxPoints = 10,
				Nodes = nodes,
				Edges = edges
			};
		}

		private TalentTreeViewModel BuildTemplar(string key)
		{
			var nodes = new List<TalentNodeViewModel>();
			var idCount = new Dictionary<string, int>();

			TalentNodeViewModel Add(string name, int col, int row, string shape = "circle")
			{
				var baseId = Slug(name);
				if (!idCount.ContainsKey(baseId)) idCount[baseId] = 0;
				idCount[baseId]++;
				var id = idCount[baseId] == 1 ? baseId : $"{baseId}-{idCount[baseId]}";
				var n = new TalentNodeViewModel { Id = id, SpellName = name, Col = col, Row = row, Shape = shape, Active = true };
				nodes.Add(n); return n;
			}

			Add("Holy Bulwark", 2, 1, "square");
			// Row 2
			Add("Rite of Sanctification", 1, 2, "hexagon");
			Add("Solidarity", 2, 2);
			Add("Divine Guidance", 3, 2, "hexagon");
			// Row 3
			Add("Laying Down Arms", 1, 3);
			Add("Divine Inspiration", 2, 3, "hexagon");
			Add("Authoritative Rebuke", 3, 3, "hexagon");
			// Row 4
			Add("Shared Resolve", 1, 4);
			Add("Valiance", 2, 4);
			Add("Hammer and Anvil", 3, 4);
			// Row 5
			Add("Blessing of the Forge", 2, 5);

			var edges = new List<TalentEdgeViewModel>
	{

				new() { FromId = IdAt(nodes,2,1), ToId = IdAt(nodes,1,2) },
				new() { FromId = IdAt(nodes,2,1), ToId = IdAt(nodes,2,2) },
				new() { FromId = IdAt(nodes,2,1), ToId = IdAt(nodes,3,2) },

				new() { FromId = IdAt(nodes,1,2), ToId = IdAt(nodes,1,3) },
				new() { FromId = IdAt(nodes,2,2), ToId = IdAt(nodes,2,3) },
				new() { FromId = IdAt(nodes,3,2), ToId = IdAt(nodes,3,3) },

				new() { FromId = IdAt(nodes,1,3), ToId = IdAt(nodes,1,4) },
				new() { FromId = IdAt(nodes,2,3), ToId = IdAt(nodes,2,4) },
				new() { FromId = IdAt(nodes,3,3), ToId = IdAt(nodes,3,4) },

				new() { FromId = IdAt(nodes,1,4), ToId = IdAt(nodes,2,5) },
				new() { FromId = IdAt(nodes,2,4), ToId = IdAt(nodes,2,5) },
				new() { FromId = IdAt(nodes,3,4), ToId = IdAt(nodes,2,5) },
	};

			return new TalentTreeViewModel
			{
				Key = key,
				Title = "Templar",
				IsHero = true,
				MaxPoints = 10,
				Nodes = nodes,
				Edges = edges
			};
		}

	}
}
