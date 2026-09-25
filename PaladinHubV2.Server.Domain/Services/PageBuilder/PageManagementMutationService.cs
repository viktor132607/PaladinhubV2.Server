using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder;

public sealed class PageManagementMutationService :
    IPageManagementMutationService
{
    private readonly AppDbContext _db;
    private readonly IPageManagementRequestPolicy _policy;

    public PageManagementMutationService(
        AppDbContext db,
        IPageManagementRequestPolicy policy)
    {
        _db = db;
        _policy = policy;
    }

    public async Task<PageManagementResult> CreateAsync(
        SavePageRequest request,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        string section =
            _policy.NormalizeSection(request.Section!);

        string slug =
            _policy.Slugify(request.Slug!);

        if (_policy.IsReserved(section, slug))
        {
            return new PageManagementResult(
                PageManagementError.ReservedRoute);
        }

        bool exists = await _db.ContentPages
            .IgnoreQueryFilters()
            .AnyAsync(
                page =>
                    page.Section == section &&
                    page.Slug == slug,
                cancellationToken);

        if (exists)
        {
            return new PageManagementResult(
                PageManagementError.SlugConflict);
        }

        DateTime now = DateTime.UtcNow;

        var page = new ContentPage
        {
            Section = section,
            Title = request.Title!.Trim(),
            Slug = slug,
            IsPublished = request.IsPublished,
            JsonLayout = "[]",
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedBy = updatedBy,
            RowVersion = Array.Empty<byte>()
        };

        _db.AuditActor = updatedBy;
        _db.PageAuditAction = "created";
        _db.ContentPages.Add(page);
        await _db.SaveChangesAsync(cancellationToken);

        return new PageManagementResult(
            PageManagementError.None,
            page);
    }

    public async Task<PageManagementResult> UpdateAsync(
        int id,
        SavePageRequest request,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        ContentPage? page = await _db.ContentPages
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id,
                cancellationToken);

        if (page is null)
        {
            return new PageManagementResult(
                PageManagementError.NotFound);
        }

        string section =
            _policy.NormalizeSection(request.Section!);

        string slug =
            _policy.Slugify(request.Slug!);

        if (_policy.IsReserved(section, slug))
        {
            return new PageManagementResult(
                PageManagementError.ReservedRoute);
        }

        bool exists = await _db.ContentPages
            .IgnoreQueryFilters()
            .AnyAsync(
                candidate =>
                    candidate.Id != id &&
                    candidate.Section == section &&
                    candidate.Slug == slug,
                cancellationToken);

        if (exists)
        {
            return new PageManagementResult(
                PageManagementError.SlugConflict);
        }

        byte[] expected;

        try
        {
            expected = Convert.FromBase64String(
                request.RowVersionBase64 ?? "");
        }
        catch (FormatException)
        {
            return new PageManagementResult(
                PageManagementError.ConcurrencyConflict);
        }

        if (expected.Length == 0 ||
            !page.RowVersion.SequenceEqual(expected))
        {
            return new PageManagementResult(
                PageManagementError.ConcurrencyConflict);
        }

        _db.Entry(page)
            .Property(candidate => candidate.RowVersion)
            .OriginalValue = expected;

        page.Section = section;
        page.Title = request.Title!.Trim();
        page.Slug = slug;
        page.IsPublished = request.IsPublished;
        page.UpdatedAt = DateTime.UtcNow;
        page.UpdatedBy = updatedBy;

        _db.AuditActor = updatedBy;
        _db.PageAuditAction = "updated";

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PageManagementResult(
                PageManagementError.ConcurrencyConflict);
        }

        return new PageManagementResult(
            PageManagementError.None,
            page);
    }

    public async Task<bool> DeleteAsync(
        int id,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        ContentPage? page = await _db.ContentPages
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id,
                cancellationToken);

        if (page is null)
        {
            return false;
        }

        _db.AuditActor = updatedBy;
        _db.PageAuditAction = "deleted";
        _db.ContentPages.Remove(page);
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
