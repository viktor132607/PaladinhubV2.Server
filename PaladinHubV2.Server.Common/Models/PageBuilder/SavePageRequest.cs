namespace PaladinHub.Areas.Admin.Models
{
	public sealed class SavePageRequest
	{
		public string? Section { get; init; }
		public string? RowVersionBase64 { get; init; }
		public string? Title { get; init; }
		public string? Slug { get; init; }
		public bool IsPublished { get; init; } = true;
	}
}
