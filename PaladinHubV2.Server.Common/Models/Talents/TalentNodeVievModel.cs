using System.Collections.Generic;

namespace PaladinHub.Models.Talents
{
	public class TalentNodeViewModel
	{
		// stable id inside a tree
		public string Id { get; set; } = string.Empty;

		// Link to a spell (priority: by Id; fallback: by Name)
		public int? SpellId { get; set; }
		public string? SpellName { get; set; }

		// position and visuals
		public int Col { get; set; }
		public int Row { get; set; }
		public string Shape { get; set; } = "circle";

		// gameplay
		public bool Active { get; set; } = false;
		public int Cost { get; set; } = 1;
		public List<string> Requires { get; set; } = new();
	}
}
