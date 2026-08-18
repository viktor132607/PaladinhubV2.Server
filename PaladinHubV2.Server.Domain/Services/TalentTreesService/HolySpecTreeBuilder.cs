using System.Collections.Generic;
using System.Linq;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Talents;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public class HolySpecTreeBuilder : ISpecializationTreeBuilder
	{
		public string BaseKey => "holy";

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
				nodes.Add(n); return n;
			}

			// NODES
			Add("Holy Shock", 5, 1, "square");
			Add("Extrication", 4, 2);
			Add("Light of Dawn", 6, 2, "square");
			Add("Light's Conviction", 3, 3);
			Add("Aura Mastery", 5, 3, "square");
			Add("Beacon of the Lightbringer", 7, 3);
			Add("Tower of Radiance", 2, 4);
			Add("Tirion's Devotion", 4, 4);
			Add("Unending Light", 6, 4);
			Add("Awestruck", 8, 4);
			Add("Moment of Compassion", 1, 5, "hexagon");
			Add("Holy Prism", 3, 5, "hexagon");
			Add("Protection of Tyr", 5, 5, "hexagon");
			Add("Imbued Infusions", 7, 5);
			Add("Light of the Martyr", 9, 5);
			Add("Righteous Judgment", 2, 6);
			Add("Divine Favor", 3, 6, "hexagon");
			Add("Saved by the Light", 4, 6);
			Add("Light's Protection", 6, 6);
			Add("Overflowing Light", 7, 6);
			Add("Shining Righteousness", 8, 6);
			Add("Liberation", 1, 7);
			Add("Commanding Light", 3, 7);
			Add("Glistening Radiance", 4, 7);
			Add("Breaking Dawn", 5, 7);
			Add("Divine Revelations", 6, 7);
			Add("Divine Glimpse", 7, 7);
			Add("Bestow Light", 9, 7);
			Add("Beacon of Faith", 2, 8, "hexagon");
			Add("Empyrean Legacy", 3, 8);
			Add("Veneration", 4, 8);
			Add("Avenging Wrath", 6, 8, "hexagon");
			Add("Power of the Silver Hand", 7, 8);
			Add("Tyr's Deliverance", 8, 8, "square");
			Add("Truth Prevails", 1, 9);
			Add("Crusader's Might", 3, 9);
			Add("Awakening", 5, 9, "hexagon");
			Add("Reclamation", 7, 9);
			Add("Relentless Inquisitor", 9, 9);
			Add("Rising Sunlight", 2, 10);
			Add("Glorious Dawn", 4, 10);
			Add("Blessing of Summer", 5, 10, "hexagon");
			Add("Inflorescence of the Sunwell", 6, 10);
			Add("Boundless Salvation", 8, 10);

			// EDGES
			var edges = new List<TalentEdgeViewModel>
			{
				new(){ FromId = IdAt(nodes,5,1), ToId = IdAt(nodes,4,2)},
				new(){ FromId = IdAt(nodes,5,1), ToId = IdAt(nodes,6,2)},

				new(){ FromId = IdAt(nodes,4,2), ToId = IdAt(nodes,3,3)},
				new(){ FromId = IdAt(nodes,4,2), ToId = IdAt(nodes,5,3)},
				new(){ FromId = IdAt(nodes,6,2), ToId = IdAt(nodes,5,3)},
				new(){ FromId = IdAt(nodes,6,2), ToId = IdAt(nodes,7,3)},

				new(){ FromId = IdAt(nodes,3,3), ToId = IdAt(nodes,2,4)},
				new(){ FromId = IdAt(nodes,3,3), ToId = IdAt(nodes,4,4)},
				new(){ FromId = IdAt(nodes,3,3), ToId = IdAt(nodes,3,5)},
				new(){ FromId = IdAt(nodes,5,3), ToId = IdAt(nodes,5,5)},
				new(){ FromId = IdAt(nodes,7,3), ToId = IdAt(nodes,6,4)},
				new(){ FromId = IdAt(nodes,7,3), ToId = IdAt(nodes,8,4)},
				new(){ FromId = IdAt(nodes,7,3), ToId = IdAt(nodes,7,5)},

				new(){ FromId = IdAt(nodes,2,4), ToId = IdAt(nodes,1,5)},
				new(){ FromId = IdAt(nodes,2,4), ToId = IdAt(nodes,2,6)},
				new(){ FromId = IdAt(nodes,4,4), ToId = IdAt(nodes,4,6)},
				new(){ FromId = IdAt(nodes,6,4), ToId = IdAt(nodes,6,6)},
				new(){ FromId = IdAt(nodes,8,4), ToId = IdAt(nodes,9,5)},
				new(){ FromId = IdAt(nodes,8,4), ToId = IdAt(nodes,8,6)},

				new(){ FromId = IdAt(nodes,1,5), ToId = IdAt(nodes,1,7)},
				new(){ FromId = IdAt(nodes,3,5), ToId = IdAt(nodes,2,6)},
				new(){ FromId = IdAt(nodes,3,5), ToId = IdAt(nodes,3,6)},
				new(){ FromId = IdAt(nodes,3,5), ToId = IdAt(nodes,4,6)},
				new(){ FromId = IdAt(nodes,5,5), ToId = IdAt(nodes,4,6)},
				new(){ FromId = IdAt(nodes,5,5), ToId = IdAt(nodes,6,6)},
				new(){ FromId = IdAt(nodes,7,5), ToId = IdAt(nodes,6,6)},
				new(){ FromId = IdAt(nodes,7,5), ToId = IdAt(nodes,7,6)},
				new(){ FromId = IdAt(nodes,7,5), ToId = IdAt(nodes,8,6)},
				new(){ FromId = IdAt(nodes,9,5), ToId = IdAt(nodes,9,7)},

				new(){ FromId = IdAt(nodes,2,6), ToId = IdAt(nodes,1,7)},
				new(){ FromId = IdAt(nodes,2,6), ToId = IdAt(nodes,3,7)},
				new(){ FromId = IdAt(nodes,3,6), ToId = IdAt(nodes,3,7)},
				new(){ FromId = IdAt(nodes,4,6), ToId = IdAt(nodes,3,7)},
				new(){ FromId = IdAt(nodes,4,6), ToId = IdAt(nodes,4,7)},
				new(){ FromId = IdAt(nodes,4,6), ToId = IdAt(nodes,5,7)},
				new(){ FromId = IdAt(nodes,6,6), ToId = IdAt(nodes,5,7)},
				new(){ FromId = IdAt(nodes,6,6), ToId = IdAt(nodes,6,7)},
				new(){ FromId = IdAt(nodes,6,6), ToId = IdAt(nodes,7,7)},
				new(){ FromId = IdAt(nodes,7,6), ToId = IdAt(nodes,7,7)},
				new(){ FromId = IdAt(nodes,8,6), ToId = IdAt(nodes,7,7)},

				new(){ FromId = IdAt(nodes,1,7), ToId = IdAt(nodes,2,8)},
				new(){ FromId = IdAt(nodes,3,7), ToId = IdAt(nodes,2,8)},
				new(){ FromId = IdAt(nodes,3,7), ToId = IdAt(nodes,3,8)},
				new(){ FromId = IdAt(nodes,3,7), ToId = IdAt(nodes,4,8)},
				new(){ FromId = IdAt(nodes,4,7), ToId = IdAt(nodes,4,8)},
				new(){ FromId = IdAt(nodes,5,7), ToId = IdAt(nodes,4,8)},
				new(){ FromId = IdAt(nodes,5,7), ToId = IdAt(nodes,6,8)},
				new(){ FromId = IdAt(nodes,6,7), ToId = IdAt(nodes,6,8)},
				new(){ FromId = IdAt(nodes,7,7), ToId = IdAt(nodes,6,8)},
				new(){ FromId = IdAt(nodes,7,7), ToId = IdAt(nodes,7,8)},
				new(){ FromId = IdAt(nodes,7,7), ToId = IdAt(nodes,8,8)},
				new(){ FromId = IdAt(nodes,9,7), ToId = IdAt(nodes,8,8)},

				new(){ FromId = IdAt(nodes,2,8), ToId = IdAt(nodes,1,9)},
				new(){ FromId = IdAt(nodes,2,8), ToId = IdAt(nodes,3,9)},
				new(){ FromId = IdAt(nodes,2,8), ToId = IdAt(nodes,2,10)},
				new(){ FromId = IdAt(nodes,3,8), ToId = IdAt(nodes,3,9)},
				new(){ FromId = IdAt(nodes,4,8), ToId = IdAt(nodes,3,9)},
				new(){ FromId = IdAt(nodes,6,8), ToId = IdAt(nodes,5,9)},
				new(){ FromId = IdAt(nodes,6,8), ToId = IdAt(nodes,7,9)},
				new(){ FromId = IdAt(nodes,7,8), ToId = IdAt(nodes,7,9)},

				new(){ FromId = IdAt(nodes,8,8), ToId = IdAt(nodes,7,9)},
				new(){ FromId = IdAt(nodes,8,8), ToId = IdAt(nodes,8,10)},
				new(){ FromId = IdAt(nodes,8,8), ToId = IdAt(nodes,9,9)},

				new(){ FromId = IdAt(nodes,3,9), ToId = IdAt(nodes,4,10)},
				new(){ FromId = IdAt(nodes,5,9), ToId = IdAt(nodes,4,10)},
				new(){ FromId = IdAt(nodes,5,9), ToId = IdAt(nodes,5,10)},
				new(){ FromId = IdAt(nodes,5,9), ToId = IdAt(nodes,6,10)},
				new(){ FromId = IdAt(nodes,7,9), ToId = IdAt(nodes,6,10)},
			};

			return new TalentTreeViewModel
			{
				Key = "holy",
				Title = "Holy",
				IsHero = false,
				MaxPoints = 31,
				Nodes = nodes,
				Edges = edges
			};
		}
	}
}
