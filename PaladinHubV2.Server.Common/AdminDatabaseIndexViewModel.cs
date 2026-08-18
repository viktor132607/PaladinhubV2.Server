using PaladinHubV2.Server.Data.Entities;

namespace PaladinHub.Areas.Admin.ViewModels
{
	public enum AdminEntity { Spells, Items }

	public class AdminDatabaseIndexViewModel
	{
		public AdminEntity Entity { get; set; }

		public IEnumerable<Spell>? Spells { get; set; }
		public IEnumerable<Item>? Items { get; set; }

		public string Search { get; set; } = "";
		public int Page { get; set; } = 1;
		public int PageSize { get; set; } = 20;
		public int Total { get; set; }

		public string ControllerName => Entity == AdminEntity.Spells ? "Spells" : "Items";
		public int Pages => (int)Math.Ceiling((double)Total / Math.Max(PageSize, 1));
	}
}
