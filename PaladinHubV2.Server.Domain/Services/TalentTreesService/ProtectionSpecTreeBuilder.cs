using System.Collections.Generic;
using System.Linq;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Talents;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public class ProtectionSpecTreeBuilder : ISpecializationTreeBuilder
	{
		public string BaseKey => "protection";

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

		// БЕЗОПАСНО търсене на възел по координати
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

			// помощник за ръбове – добавя само ако двата възела съществуват
			void AddEdge(int fromCol, int fromRow, int toCol, int toRow, List<TalentEdgeViewModel> list)
			{
				if (TryIdAt(nodes, fromCol, fromRow, out var from) && TryIdAt(nodes, toCol, toRow, out var to))
				{
					list.Add(new TalentEdgeViewModel { FromId = from, ToId = to });
				}
				// else: липсващ възел – пропускаме реброто (без да гърми)
			}

			// ===== NODES =====
			Add("Avenger's Shield", 4, 1, "square");
			Add("Holy Shield", 3, 2);
			Add("Blessed Hammer", 5, 2, "hexagon");
			Add("Redoubt", 2, 3, "hexagon");
			Add("Grand Crusader", 4, 3);
			Add("Shining Light", 6, 3);
			Add("Improved Holy Shield", 2, 4);
			Add("Inspiring Vanguard", 3, 4);
			Add("Ardent Defender", 4, 4, "square");
			Add("Barricade of Faith", 5, 4);
			Add("Sanctuary", 6, 4);
			Add("Refining Fire", 1, 5);
			Add("Bulwark of Order", 3, 5);
			Add("Blessing of Spellwarding", 4, 5, "hexagon");
			Add("Tirion's Devotion", 5, 5, "hexagon");
			Add("Consecration in Flame", 7, 5);
			Add("Tyr's Enforcer", 2, 6);
			Add("Relentless Inquisitor", 3, 6, "hexagon");
			Add("Avenging Wrath", 4, 6);
			Add("Seal of Charity", 5, 6);
			Add("Faith in the Light", 6, 6);
			Add("Soaring Shield", 1, 7);
			Add("Seal of Reprisal", 3, 7);
			Add("Guardian of Ancient Kings", 4, 7, "square");
			Add("Hand of the Protector", 5, 7);
			Add("Crusader's Judgment", 7, 7);
			Add("Focused Enmity", 2, 8);
			Add("Gift of the Golden Val'kyr", 3, 8);
			Add("Sanctified Wrath", 5, 8);
			Add("Uther's Counsel", 6, 8);
			Add("Strength in Adversity", 1, 9, "hexagon");
			Add("Ferren Marcus's Fervor", 2, 9);
			Add("Eye of Tyr", 4, 9, "square");
			Add("Resolute Defender", 6, 9);
			Add("Bastion of Light", 7, 9, "square");
			Add("Moment of Glory", 1, 10, "square");
			Add("Bulwark of Righteous Fury", 3, 10);
			Add("Inmost Light", 4, 10);
			Add("Final Stand", 5, 10);
			Add("Righteous Protector", 7, 10);

			// ===== EDGES (безопасно добавяне) =====
			var edges = new List<TalentEdgeViewModel>();

			AddEdge(4, 1, 3, 2, edges);
			AddEdge(4, 1, 5, 2, edges);

			AddEdge(3, 2, 2, 3, edges);
			AddEdge(3, 2, 4, 3, edges);
			AddEdge(5, 2, 4, 3, edges);
			AddEdge(5, 2, 6, 3, edges);

			AddEdge(4, 3, 3, 4, edges);
			AddEdge(2, 3, 2, 4, edges);
			AddEdge(4, 3, 4, 4, edges);
			AddEdge(4, 3, 5, 4, edges);
			AddEdge(6, 3, 6, 4, edges);

			AddEdge(2, 4, 1, 5, edges);
			AddEdge(2, 4, 3, 5, edges);
			AddEdge(3, 4, 3, 5, edges);
			AddEdge(4, 4, 3, 5, edges);
			AddEdge(4, 4, 4, 5, edges);
			AddEdge(4, 4, 5, 5, edges);
			AddEdge(5, 4, 5, 5, edges);
			AddEdge(6, 4, 5, 5, edges);
			AddEdge(6, 4, 7, 5, edges);

			AddEdge(1, 5, 1, 7, edges);
			AddEdge(1, 5, 2, 6, edges);
			AddEdge(3, 5, 2, 6, edges);
			AddEdge(3, 5, 3, 6, edges);
			AddEdge(3, 5, 4, 6, edges);
			AddEdge(4, 5, 4, 6, edges);
			AddEdge(5, 5, 4, 6, edges);
			AddEdge(5, 5, 5, 6, edges);
			AddEdge(5, 5, 6, 6, edges);
			AddEdge(7, 5, 6, 6, edges);
			AddEdge(7, 5, 7, 7, edges);

			AddEdge(2, 6, 2, 8, edges);
			AddEdge(2, 6, 1, 7, edges);
			AddEdge(2, 6, 3, 7, edges);
			AddEdge(3, 6, 3, 7, edges);
			AddEdge(4, 6, 3, 7, edges);
			AddEdge(4, 6, 4, 7, edges);
			AddEdge(4, 6, 5, 7, edges);
			AddEdge(5, 6, 5, 7, edges);
			AddEdge(6, 6, 5, 7, edges);
			AddEdge(6, 6, 7, 7, edges);
			// В оригинала имаше ребро от (7,6), но нямаме такъв възел – пропускаме:

			AddEdge(1, 7, 1, 9, edges);
			AddEdge(1, 7, 2, 8, edges);
			AddEdge(3, 7, 2, 8, edges);
			AddEdge(4, 7, 4, 9, edges);
			AddEdge(4, 7, 3, 8, edges);
			AddEdge(4, 7, 5, 8, edges);
			AddEdge(5, 7, 6, 8, edges);
			AddEdge(7, 7, 6, 8, edges);
			AddEdge(7, 7, 7, 8, edges);

			AddEdge(2, 8, 1, 9, edges);
			AddEdge(2, 8, 2, 9, edges);
			AddEdge(3, 8, 2, 9, edges);
			AddEdge(3, 8, 3, 10, edges);
			AddEdge(3, 8, 4, 9, edges);
			AddEdge(5, 8, 4, 9, edges);
			AddEdge(5, 8, 6, 9, edges);
			AddEdge(6, 8, 6, 9, edges);
			AddEdge(7, 8, 7, 9, edges);

			AddEdge(1, 9, 1, 10, edges);
			AddEdge(2, 9, 1, 10, edges);
			AddEdge(2, 9, 3, 10, edges);
			AddEdge(4, 9, 3, 10, edges);
			AddEdge(4, 9, 4, 10, edges);
			AddEdge(4, 9, 5, 10, edges);
			AddEdge(6, 9, 5, 10, edges);
			AddEdge(6, 9, 7, 10, edges);
			AddEdge(7, 9, 7, 10, edges);

			return new TalentTreeViewModel
			{
				Key = "protection",
				Title = "Protection",
				IsHero = false,
				MaxPoints = 31,
				Nodes = nodes,
				Edges = edges
			};
		}
	}
}
