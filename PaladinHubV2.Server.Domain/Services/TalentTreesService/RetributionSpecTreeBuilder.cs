using System.Collections.Generic;
using System.Linq;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Talents;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public class RetributionSpecTreeBuilder : ISpecializationTreeBuilder
	{
		public string BaseKey => "retribution";

		private static string Slug(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return "node";
			var chars = s.ToLowerInvariant()
				.Select(ch => (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') ? ch : '-')
				.ToArray();
			var lower = new string(chars);
			while (lower.Contains("--")) lower = lower.Replace("--", "-");
			return lower.Trim('-');
		}

		// безопасно намиране на възел по координати
		private static bool TryIdAt(List<TalentNodeViewModel> nodes, int col, int row, out string id)
		{
			var node = nodes.FirstOrDefault(n => n.Col == col && n.Row == row);
			id = node?.Id ?? "";
			return node != null;
		}

		public TalentTreeViewModel BuildTree(List<Spell> spells)
		{
			var nodes = new List<TalentNodeViewModel>();
			var idCount = new Dictionary<string, int>();

			TalentNodeViewModel Add(string name, int col, int row, string shape = "circle")
			{
				var baseId = Slug(name);
				if (!idCount.ContainsKey(baseId)) idCount[baseId] = 0;
				idCount[baseId]++;
				var id = idCount[baseId] == 1 ? baseId : $"{baseId}-{idCount[baseId]}";
				var n = new TalentNodeViewModel { Id = id, SpellName = name, Col = col, Row = row, Shape = shape };
				nodes.Add(n);
				return n;
			}

			// помощник за ребра – добавя само ако и двата възела съществуват
			void AddEdge(int fromCol, int fromRow, int toCol, int toRow, List<TalentEdgeViewModel> list)
			{
				if (TryIdAt(nodes, fromCol, fromRow, out var from) && TryIdAt(nodes, toCol, toRow, out var to))
					list.Add(new TalentEdgeViewModel { FromId = from, ToId = to });
				// иначе прескачаме (няма да хвърля)
			}

			// ===== NODES =====
			// Row 1
			Add("Blade of Justice", 5, 1, "square");
			// Row 2
			Add("Divine Storm", 5, 2, "square");
			// Row 3
			Add("Swift Justice", 2, 3, "hexagon");
			Add("Expurgation", 4, 3);
			Add("Judgement of Justice", 6, 3);
			Add("Holy Blade", 8, 3, "hexagon");
			// Row 4
			Add("Final Verdict", 2, 4, "hexagon");
			Add("Guided Prayer", 5, 4, "hexagon");
			Add("Art of War", 8, 4, "hexagon");
			// Row 5
			Add("Jurisdiction", 1, 5);
			Add("Tempest of the Lightbringer", 3, 5, "hexagon");
			Add("Crusade", 5, 5, "hexagon");
			Add("Vanguard's Momentum", 7, 5, "hexagon");
			Add("Zealot's Fervor", 8, 5);
			Add("Rush of Light", 9, 5);
			// Row 6
			Add("Boundless Judgment", 2, 6, "hexagon");
			Add("Crusading Strikes", 4, 6, "hexagon");
			Add("Divine Wrath", 5, 6);
			Add("Divine Hammer", 6, 6, "square");
			Add("Holy Flames", 8, 6, "hexagon");
			// Row 7
			Add("Empyrean Legacy", 1, 7);
			Add("Heart of the Crusader", 2, 7);
			Add("Highlord's Wrath", 3, 7);
			Add("Wake of Ashes", 5, 7, "square");
			Add("Blessed Champion", 7, 7);
			Add("Judge, Jury and Executioner", 9, 7, "hexagon");
			// Row 8
			Add("Adjudication", 2, 8);
			Add("Shield of Vengeance", 5, 8, "hexagon");
			Add("Penitence", 8, 8);
			// Row 9
			Add("Blades of Light", 2, 9);
			Add("Execution Sentence", 4, 9, "hexagon");
			Add("Seething Flames", 6, 9);
			Add("Burning Crusade", 8, 9);
			// Row 10
			Add("Divine Arbiter", 2, 10);
			Add("Executioner's Will", 3, 10);
			Add("Divine Auxiliary", 4, 10);
			Add("Radiant Glory", 6, 10);
			Add("Burn to Ash", 7, 10);
			Add("Searing Light", 8, 10);

			// ===== EDGES (безопасно) =====
			var edges = new List<TalentEdgeViewModel>();

			AddEdge(5, 1, 5, 2, edges);

			AddEdge(5, 2, 4, 3, edges);
			AddEdge(5, 2, 6, 3, edges);
			AddEdge(5, 2, 2, 3, edges);
			AddEdge(5, 2, 8, 3, edges);

			AddEdge(2, 3, 2, 4, edges);
			AddEdge(2, 3, 5, 4, edges);
			AddEdge(4, 3, 5, 4, edges);
			AddEdge(6, 3, 5, 4, edges);
			AddEdge(8, 3, 5, 4, edges);
			AddEdge(8, 3, 8, 4, edges);

			AddEdge(2, 4, 1, 5, edges);
			AddEdge(2, 4, 2, 6, edges);
			AddEdge(2, 4, 3, 5, edges);
			AddEdge(5, 4, 3, 5, edges);
			AddEdge(5, 4, 5, 5, edges);
			AddEdge(5, 4, 7, 5, edges);
			AddEdge(8, 4, 7, 5, edges);
			AddEdge(8, 4, 8, 5, edges);
			AddEdge(8, 4, 9, 5, edges);

			AddEdge(1, 5, 1, 7, edges);
			// в оригинала имаше (2,5)->(2,6), но възел при (2,5) няма → прескачаме
			AddEdge(3, 5, 2, 6, edges);
			AddEdge(3, 5, 3, 7, edges);
			AddEdge(5, 5, 4, 6, edges);
			AddEdge(5, 5, 5, 6, edges);
			AddEdge(5, 5, 6, 6, edges);
			AddEdge(7, 5, 7, 7, edges);
			AddEdge(7, 5, 8, 6, edges);
			AddEdge(8, 5, 8, 6, edges);
			AddEdge(9, 5, 8, 6, edges);
			AddEdge(9, 5, 9, 7, edges);

			AddEdge(2, 6, 1, 7, edges);
			AddEdge(2, 6, 2, 7, edges);
			AddEdge(2, 6, 3, 7, edges);
			AddEdge(4, 6, 3, 7, edges);
			AddEdge(4, 6, 3, 7, edges); // дублирано в оригинала – не пречи
			AddEdge(4, 6, 5, 7, edges);
			AddEdge(5, 6, 5, 7, edges);
			AddEdge(6, 6, 5, 7, edges);
			AddEdge(6, 6, 7, 7, edges);
			AddEdge(8, 6, 7, 7, edges);
			AddEdge(8, 6, 8, 8, edges);
			AddEdge(8, 6, 9, 7, edges);

			AddEdge(1, 7, 2, 8, edges);
			AddEdge(2, 7, 2, 8, edges);
			AddEdge(3, 7, 2, 8, edges);
			AddEdge(5, 7, 2, 8, edges);
			AddEdge(5, 7, 5, 8, edges);
			AddEdge(5, 7, 8, 8, edges);
			AddEdge(7, 7, 8, 8, edges);
			AddEdge(9, 7, 8, 8, edges);

			AddEdge(2, 8, 2, 9, edges);
			AddEdge(5, 8, 4, 9, edges);
			AddEdge(5, 8, 6, 9, edges);
			AddEdge(8, 8, 8, 9, edges);

			AddEdge(2, 9, 2, 10, edges);
			AddEdge(4, 9, 3, 10, edges);
			AddEdge(4, 9, 4, 10, edges);
			AddEdge(6, 9, 6, 10, edges);
			AddEdge(6, 9, 7, 10, edges);
			AddEdge(8, 9, 8, 10, edges);

			return new TalentTreeViewModel
			{
				Key = "retribution",
				Title = "Retribution",
				IsHero = false,
				MaxPoints = 31,
				Nodes = nodes,
				Edges = edges
			};
		}
	}
}
