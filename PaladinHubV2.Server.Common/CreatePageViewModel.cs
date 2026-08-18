using System.ComponentModel.DataAnnotations;

namespace PaladinHub.Areas.Admin.Models
{
	public class CreatePageViewModel
	{
		[Required, StringLength(50)]
		public string Section { get; set; } = string.Empty;

		[Required, StringLength(200)]
		public string Title { get; set; } = string.Empty;

		[StringLength(100)]
		public string? Slug { get; set; }

		[StringLength(500)]
		public string? Description { get; set; }

		public string? InitialContent { get; set; }

		public bool IsPublished { get; set; } = false;

		[Required]
		public string JsonLayout { get; set; } = "[]";

		public string SectionName =>
			string.IsNullOrWhiteSpace(Section) ? "" :
			char.ToUpperInvariant(Section[0]) + Section[1..];
	}
}