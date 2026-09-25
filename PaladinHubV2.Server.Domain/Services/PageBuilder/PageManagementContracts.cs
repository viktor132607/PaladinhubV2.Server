using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder;

public enum PageManagementError
{
    None = 0,
    NotFound,
    ReservedRoute,
    SlugConflict,
    ConcurrencyConflict
}

public sealed record PageManagementResult(
    PageManagementError Error,
    ContentPage? Page = null);

public interface IPageManagementQueryService
{
    Task<List<ContentPage>> ListAsync(
        CancellationToken cancellationToken);

    Task<ContentPage?> GetAsync(
        int id,
        CancellationToken cancellationToken);
}

public interface IPageManagementRequestPolicy
{
    string? ValidateRequest(
        SavePageRequest? request);

    string NormalizeSection(
        string value);

    string Slugify(
        string value);

    bool IsReserved(
        string section,
        string slug);
}

public interface IPageManagementMutationService
{
    Task<PageManagementResult> CreateAsync(
        SavePageRequest request,
        string updatedBy,
        CancellationToken cancellationToken);

    Task<PageManagementResult> UpdateAsync(
        int id,
        SavePageRequest request,
        string updatedBy,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(
        int id,
        string updatedBy,
        CancellationToken cancellationToken);
}
