using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder;

public sealed class PageManagementService
{
    private readonly IPageManagementQueryService _queries;
    private readonly IPageManagementRequestPolicy _policy;
    private readonly IPageManagementMutationService _mutations;

    public PageManagementService(
        AppDbContext db)
        : this(
            new PageManagementQueryService(db),
            new PageManagementRequestPolicy(),
            new PageManagementMutationService(
                db,
                new PageManagementRequestPolicy()))
    {
    }

    public PageManagementService(
        IPageManagementQueryService queries,
        IPageManagementRequestPolicy policy,
        IPageManagementMutationService mutations)
    {
        _queries = queries;
        _policy = policy;
        _mutations = mutations;
    }

    public Task<List<ContentPage>> ListAsync(
        CancellationToken cancellationToken) =>
        _queries.ListAsync(cancellationToken);

    public Task<ContentPage?> GetAsync(
        int id,
        CancellationToken cancellationToken) =>
        _queries.GetAsync(id, cancellationToken);

    public string? ValidateRequest(
        SavePageRequest? request) =>
        _policy.ValidateRequest(request);

    public Task<PageManagementResult> CreateAsync(
        SavePageRequest request,
        string updatedBy,
        CancellationToken cancellationToken) =>
        _mutations.CreateAsync(
            request,
            updatedBy,
            cancellationToken);

    public Task<PageManagementResult> UpdateAsync(
        int id,
        SavePageRequest request,
        string updatedBy,
        CancellationToken cancellationToken) =>
        _mutations.UpdateAsync(
            id,
            request,
            updatedBy,
            cancellationToken);

    public Task<bool> DeleteAsync(
        int id,
        string updatedBy,
        CancellationToken cancellationToken) =>
        _mutations.DeleteAsync(
            id,
            updatedBy,
            cancellationToken);

    public Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken) =>
        DeleteAsync(
            id,
            "admin",
            cancellationToken);

    public static string Capitalize(
        string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? value
            : char.ToUpperInvariant(value[0]) +
              value[1..];
    }
}
