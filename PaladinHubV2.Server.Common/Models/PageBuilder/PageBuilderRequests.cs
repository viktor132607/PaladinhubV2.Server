namespace PaladinHub.Models.PageBuilder
{
	public sealed record PutPageLayoutRequest(
		string? JsonLayout,
		string? RowVersionBase64);

	public sealed record CreateDataPresetRequest(
		string Name,
		string Entity,
		string? JsonQuery,
		string? Section);

	public sealed record UpdateDataPresetRequest(
		string? Name,
		string? JsonQuery,
		string? Section);

	public sealed class EditPageRequest
	{
		public string Section { get; init; } = string.Empty;
		public string Slug { get; init; } = string.Empty;
		public string? Title { get; init; }
		public string? JsonLayout { get; init; }
	}
}
