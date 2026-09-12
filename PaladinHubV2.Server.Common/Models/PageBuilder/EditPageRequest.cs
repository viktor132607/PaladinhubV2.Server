namespace PaladinHub.Areas.Admin.Models
{
	public sealed class EditPageRequest
	{
		public string Section { get; init; } = string.Empty;
		public string Slug { get; init; } = string.Empty;
		public string? Title { get; init; }
		public string? JsonLayout { get; init; }
	}
}
