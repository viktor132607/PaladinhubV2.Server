namespace PaladinHub.Models.PageBuilder
{
	public sealed record ContentPageResponse(
		ContentPageViewModel Page,
		string Html,
		bool CanEdit,
		string? RenderError);
}
