using PaladinHubV2.Server.Data.Entities;

namespace PaladinHub.Models.PageBuilder
{

	public class ContentPageViewModel
	{
		public string Section { get; set; } = ""; 
		public string Title { get; set; } = "";
		public string Slug { get; set; } = "";

		public int Id { get; set; }
		public System.Guid Uid { get; set; }

		public string JsonLayout { get; set; } = "[]";

		public List<Item> Items { get; set; } = new();
		public List<Spell> Spells { get; set; } = new();

		public bool IsPublished { get; set; } = true;

		public System.DateTime UpdatedAt { get; set; }

		public string? UpdatedBy { get; set; }

		public string? RowVersionBase64 { get; set; }
	}
}
