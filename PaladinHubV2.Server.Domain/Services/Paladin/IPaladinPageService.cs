using PaladinHub.Models;
using PaladinHub.Models.PageBuilder;

namespace PaladinHubV2.Server.Domain.Services.Paladin
{
	public interface IPaladinPageService
	{
		string NormalizeSection(string? section);
		Task<CombinedViewModel> GetSectionPageAsync(string normalizedSection, string actionName);
		Task<CombinedViewModel> GetTalentsAsync(string normalizedSection);
		Task<PaladinContentPageResult?> GetContentPageAsync(string normalizedSection, string slug);
	}

	public sealed record PaladinContentPageResult(
		ContentPageViewModel Page,
		string Html,
		string? RenderError);
}
