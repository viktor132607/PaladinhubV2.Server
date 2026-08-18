namespace PaladinHub.Areas.Admin.Models
{
	public class DeletePageViewModel
	{
		public int Id { get; set; }
		public string Title { get; set; } = string.Empty;
		public string Section { get; set; } = string.Empty;
		public string Slug { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; }
	}
}