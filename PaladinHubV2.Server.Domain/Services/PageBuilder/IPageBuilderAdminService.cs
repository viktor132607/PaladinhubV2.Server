using PaladinHub.Areas.Admin.Models;
using PaladinHub.Models.PageBuilder;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder
{
	public interface IPageBuilderAdminService
	{
		CreatePageViewModel BuildCreateModel(string? section);
		Task<ContentPage?> GetByRouteAsync(string section, string slug, CancellationToken cancellationToken = default);
		DeletePageViewModel BuildDeleteModel(ContentPage page);
		Task<PageBuilderCreateResult> CreateAsync(CreatePageViewModel model, CancellationToken cancellationToken = default);
		Task<PageBuilderEditResult?> EditAsync(EditPageRequest request, CancellationToken cancellationToken = default);
		Task DeleteAsync(string section, string slug, CancellationToken cancellationToken = default);
		string DisplaySection(string section);
	}

	public sealed record PageBuilderCreateResult(
		bool Conflict,
		ContentPage? Page,
		string? RedirectUrl);

	public sealed record PageBuilderEditResult(
		ContentPage Page,
		string RedirectUrl);
}
