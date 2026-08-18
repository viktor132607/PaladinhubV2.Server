using System;
using System.Collections.Generic;
using System.Linq;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Talents;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public class PaladinClassTreeBuilder : IClassTreeBuilder
	{
		public string BaseKey => "paladin";

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

		private static string IdAt(List<TalentNodeViewModel> nodes, int col, int row)
			=> nodes.First(n => n.Col == col && n.Row == row).Id;

		public TalentTreeViewModel BuildTree(List<Spell> spells)
		{
			var nodes = new List<TalentNodeViewModel>();
			var idCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

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

			// ==== НОДОВЕ (1:1 със стария Paladin .cshtml) ====

			// Row 1
			Add("Lay on Hands", 2, 1, "square");
			Add("Auras of the Resolute", 4, 1, "square");
			Add("Hammer of Wrath", 6, 1, "square");

			// Row 2
			Add("Improved Cleanse", 1, 2);
			Add("Empyreal Ward", 2, 2);
			Add("Fist of Justice", 3, 2);
			Add("Blinding Light", 5, 2, "hexagon");
			Add("Turn Evil", 7, 2, "square");

			// Row 3
			Add("A Just Reward", 1, 3);
			Add("Afterimage", 2, 3);
			Add("Divine Steed", 4, 3, "square");
			Add("Light's Countenance", 5, 3);
			Add("Greater Judgment", 6, 3);
			Add("Wrench Evil", 7, 3, "hexagon");

			// Row 4
			Add("Holy Reprieve", 1, 4);
			Add("Cavalier", 3, 4);
			Add("Divine Spurs", 4, 4);
			Add("Blessing of Freedom", 5, 4, "hexagon");
			Add("Rebuke", 7, 4, "square");

			// Row 5
			Add("Obduracy", 2, 5);
			Add("Divine Toll", 4, 5, "square");
			Add("Echoing Blessings", 5, 5, "hexagon");
			Add("Sanctified Plates", 6, 5);
			Add("Punishment", 7, 5);

			// Row 6
			Add("Divine Reach", 1, 6);
			Add("Blessing of Sacrifice", 3, 6, "square");
			Add("Quickened Invocation", 4, 6, "hexagon");
			Add("Blessing of Protection", 5, 6, "square");
			Add("Consecrated Ground", 7, 6);

			// Row 7
			Add("Holy Aegis", 2, 7);
			Add("Sacrifice of the Just", 3, 7, "hexagon");
			Add("Divine Purpose", 4, 7);
			Add("Improved Blessing of Protection", 5, 7);
			Add("Unbreakable Spirit", 6, 7);

			// Row 8
			Add("Lightforged Blessing", 1, 8);
			Add("Lead the Charge", 2, 8);
			Add("Righteous Protection", 3, 8, "hexagon");
			Add("Holy Ritual", 4, 8);
			Add("Blessed Calling", 5, 8);
			Add("Inspired Guard", 6, 8);
			Add("Judgment of Light", 7, 8);

			// Row 9
			Add("Faith's Armor", 1, 9);
			Add("Stoicism", 2, 9);
			Add("Seal of Might", 3, 9);
			Add("Seal of the Crusader", 4, 9);
			Add("Vengeful Wrath", 5, 9);
			Add("Eye for an Eye", 6, 9);
			Add("Golden Path", 7, 9, "hexagon");

			// Row 10
			Add("Of Dusk and Dawn", 2, 10);
			Add("Lightbearer", 4, 10);
			Add("Light's Revocation", 6, 10);

			// ==== РЪБОВЕ (копирани от стария markup) ====
			var edges = new List<TalentEdgeViewModel>
			{
				new() { FromId = IdAt(nodes,2,1), ToId = IdAt(nodes,1,2) },
				new() { FromId = IdAt(nodes,2,1), ToId = IdAt(nodes,2,2) },
				new() { FromId = IdAt(nodes,2,1), ToId = IdAt(nodes,3,2) },
				new() { FromId = IdAt(nodes,4,1), ToId = IdAt(nodes,3,2) },
				new() { FromId = IdAt(nodes,4,1), ToId = IdAt(nodes,5,2) },
				new() { FromId = IdAt(nodes,4,1), ToId = IdAt(nodes,4,3) },
				new() { FromId = IdAt(nodes,6,1), ToId = IdAt(nodes,5,2) },
				new() { FromId = IdAt(nodes,6,1), ToId = IdAt(nodes,7,2) },
				new() { FromId = IdAt(nodes,6,1), ToId = IdAt(nodes,6,3) },

				new() { FromId = IdAt(nodes,1,2), ToId = IdAt(nodes,1,3) },
				new() { FromId = IdAt(nodes,1,2), ToId = IdAt(nodes,2,3) },
				new() { FromId = IdAt(nodes,2,2), ToId = IdAt(nodes,2,3) },
				new() { FromId = IdAt(nodes,3,2), ToId = IdAt(nodes,2,3) },
				new() { FromId = IdAt(nodes,3,2), ToId = IdAt(nodes,4,3) },
				new() { FromId = IdAt(nodes,5,2), ToId = IdAt(nodes,4,3) },
				new() { FromId = IdAt(nodes,5,2), ToId = IdAt(nodes,5,3) },
				new() { FromId = IdAt(nodes,5,2), ToId = IdAt(nodes,6,3) },
				new() { FromId = IdAt(nodes,7,2), ToId = IdAt(nodes,7,3) },

				new() { FromId = IdAt(nodes,1,3), ToId = IdAt(nodes,1,4) },
				new() { FromId = IdAt(nodes,2,3), ToId = IdAt(nodes,1,4) },
				new() { FromId = IdAt(nodes,2,3), ToId = IdAt(nodes,2,5) },
				new() { FromId = IdAt(nodes,4,3), ToId = IdAt(nodes,3,4) },
				new() { FromId = IdAt(nodes,4,3), ToId = IdAt(nodes,4,4) },
				new() { FromId = IdAt(nodes,4,3), ToId = IdAt(nodes,5,4) },
				new() { FromId = IdAt(nodes,6,3), ToId = IdAt(nodes,6,5) },
				new() { FromId = IdAt(nodes,6,3), ToId = IdAt(nodes,7,4) },
				new() { FromId = IdAt(nodes,7,3), ToId = IdAt(nodes,7,4) },

				new() { FromId = IdAt(nodes,1,4), ToId = IdAt(nodes,2,5) },
				new() { FromId = IdAt(nodes,1,4), ToId = IdAt(nodes,1,6) },
				new() { FromId = IdAt(nodes,3,4), ToId = IdAt(nodes,2,5) },
				new() { FromId = IdAt(nodes,3,4), ToId = IdAt(nodes,4,5) },
				new() { FromId = IdAt(nodes,3,4), ToId = IdAt(nodes,3,6) },
				new() { FromId = IdAt(nodes,5,4), ToId = IdAt(nodes,4,5) },
				new() { FromId = IdAt(nodes,5,4), ToId = IdAt(nodes,5,5) },
				new() { FromId = IdAt(nodes,5,4), ToId = IdAt(nodes,6,5) },
				new() { FromId = IdAt(nodes,7,4), ToId = IdAt(nodes,6,5) },
				new() { FromId = IdAt(nodes,7,4), ToId = IdAt(nodes,7,5) },

				new() { FromId = IdAt(nodes,2,5), ToId = IdAt(nodes,1,6) },
				new() { FromId = IdAt(nodes,2,5), ToId = IdAt(nodes,3,6) },
				new() { FromId = IdAt(nodes,2,5), ToId = IdAt(nodes,2,7) },
				new() { FromId = IdAt(nodes,4,5), ToId = IdAt(nodes,3,6) },
				new() { FromId = IdAt(nodes,4,5), ToId = IdAt(nodes,4,6) },
				new() { FromId = IdAt(nodes,4,5), ToId = IdAt(nodes,5,6) },
				new() { FromId = IdAt(nodes,5,5), ToId = IdAt(nodes,5,6) },
				new() { FromId = IdAt(nodes,6,5), ToId = IdAt(nodes,5,6) },
				new() { FromId = IdAt(nodes,6,5), ToId = IdAt(nodes,7,6) },
				new() { FromId = IdAt(nodes,6,5), ToId = IdAt(nodes,6,7) },
				new() { FromId = IdAt(nodes,7,5), ToId = IdAt(nodes,7,6) },

				new() { FromId = IdAt(nodes,1,6), ToId = IdAt(nodes,2,7) },
				new() { FromId = IdAt(nodes,1,6), ToId = IdAt(nodes,1,8) },
				new() { FromId = IdAt(nodes,3,6), ToId = IdAt(nodes,2,7) },
				new() { FromId = IdAt(nodes,3,6), ToId = IdAt(nodes,3,7) },
				new() { FromId = IdAt(nodes,3,6), ToId = IdAt(nodes,4,7) },
				new() { FromId = IdAt(nodes,5,6), ToId = IdAt(nodes,5,7) },
				new() { FromId = IdAt(nodes,5,6), ToId = IdAt(nodes,6,7) },
				new() { FromId = IdAt(nodes,5,6), ToId = IdAt(nodes,4,7) },
				new() { FromId = IdAt(nodes,7,6), ToId = IdAt(nodes,6,7) },
				new() { FromId = IdAt(nodes,7,6), ToId = IdAt(nodes,7,8) },

				new() { FromId = IdAt(nodes,2,7), ToId = IdAt(nodes,1,8) },
				new() { FromId = IdAt(nodes,2,7), ToId = IdAt(nodes,2,8) },
				new() { FromId = IdAt(nodes,3,7), ToId = IdAt(nodes,2,8) },
				new() { FromId = IdAt(nodes,3,7), ToId = IdAt(nodes,3,8) },
				new() { FromId = IdAt(nodes,3,7), ToId = IdAt(nodes,4,8) },
				new() { FromId = IdAt(nodes,4,7), ToId = IdAt(nodes,4,8) },
				new() { FromId = IdAt(nodes,4,7), ToId = IdAt(nodes,5,8) },
				new() { FromId = IdAt(nodes,5,7), ToId = IdAt(nodes,5,8) },
				new() { FromId = IdAt(nodes,5,7), ToId = IdAt(nodes,6,8) },
				new() { FromId = IdAt(nodes,6,7), ToId = IdAt(nodes,6,8) },
				new() { FromId = IdAt(nodes,6,7), ToId = IdAt(nodes,7,8) },

				new() { FromId = IdAt(nodes,1,8), ToId = IdAt(nodes,1,9) },
				new() { FromId = IdAt(nodes,2,8), ToId = IdAt(nodes,1,9) },
				new() { FromId = IdAt(nodes,2,8), ToId = IdAt(nodes,2,9) },
				new() { FromId = IdAt(nodes,2,8), ToId = IdAt(nodes,3,9) },
				new() { FromId = IdAt(nodes,3,8), ToId = IdAt(nodes,3,9) },
				new() { FromId = IdAt(nodes,4,8), ToId = IdAt(nodes,3,9) },
				new() { FromId = IdAt(nodes,4,8), ToId = IdAt(nodes,4,9) },
				new() { FromId = IdAt(nodes,4,8), ToId = IdAt(nodes,5,9) },
				new() { FromId = IdAt(nodes,5,8), ToId = IdAt(nodes,5,9) },
				new() { FromId = IdAt(nodes,6,8), ToId = IdAt(nodes,5,9) },
				new() { FromId = IdAt(nodes,6,8), ToId = IdAt(nodes,6,9) },
				new() { FromId = IdAt(nodes,6,8), ToId = IdAt(nodes,7,9) },
				new() { FromId = IdAt(nodes,7,8), ToId = IdAt(nodes,7,9) },

				new() { FromId = IdAt(nodes,1,9), ToId = IdAt(nodes,2,10) },
				new() { FromId = IdAt(nodes,2,9), ToId = IdAt(nodes,2,10) },
				new() { FromId = IdAt(nodes,3,9), ToId = IdAt(nodes,2,10) },
				new() { FromId = IdAt(nodes,3,9), ToId = IdAt(nodes,4,10) },
				new() { FromId = IdAt(nodes,4,9), ToId = IdAt(nodes,4,10) },
				new() { FromId = IdAt(nodes,5,9), ToId = IdAt(nodes,4,10) },
				new() { FromId = IdAt(nodes,5,9), ToId = IdAt(nodes,6,10) },
				new() { FromId = IdAt(nodes,6,9), ToId = IdAt(nodes,6,10) },
				new() { FromId = IdAt(nodes,7,9), ToId = IdAt(nodes,6,10) },
			};

			return new TalentTreeViewModel
			{
				Key = "paladin",
				Title = "Paladin",
				IsHero = false,
				MaxPoints = 31,
				Nodes = nodes,
				Edges = edges
			};
		}
	}
}
