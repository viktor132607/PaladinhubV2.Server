using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder;

public sealed record PageLifecycleRow(int Id, string Title, string Section, string Slug, bool IsPublished, bool IsArchived, bool IsDeleted, int Version, DateTime UpdatedAt, string? UpdatedBy);
public sealed record PageLifecycleResult(int Status, string? Message = null);
public sealed class PageLifecycleService(AppDbContext db, IJsonLayoutValidator validator)
{
    public Task<List<PageLifecycleRow>> List(CancellationToken ct) => db.ContentPages.IgnoreQueryFilters().AsNoTracking().OrderBy(p => p.Section).ThenBy(p => p.Title)
        .Select(p => new PageLifecycleRow(p.Id,p.Title,p.Section,p.Slug,p.IsPublished,p.IsArchived,p.IsDeleted,p.Version,p.UpdatedAt,p.UpdatedBy)).ToListAsync(ct);
    public Task<List<PageRevision>> History(int id, CancellationToken ct) => db.PageRevisions.AsNoTracking().Where(r => r.PageId == id).OrderByDescending(r => r.Version).ToListAsync(ct);
    public async Task<PageLifecycleResult> Change(int id, int version, string action, Guid? revisionId, string actor, CancellationToken ct)
    {
        await using var transaction = await new GameDataAssignmentService(db).BeginAsync(ct);
        var page = await db.ContentPages.IgnoreQueryFilters().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (page is null) return new(404,"Page not found.");
        if (page.Version != version) return new(409,"This page changed. Refresh before continuing.");
        if (action == "restore")
        {
            var revision = await db.PageRevisions.SingleOrDefaultAsync(r => r.PageId == id && r.Id == revisionId, ct);
            if (revision is null) return new(404,"Revision not found.");
            var snapshot = JsonSerializer.Deserialize<PageSnapshot>(revision.Snapshot)!;
            if (snapshot.IsDeleted) return new(400,"Select a revision before deletion.");
            try { validator.ValidateOrThrow(snapshot.JsonLayout); }
            catch (JsonLayoutValidationException e) { return new(400,string.Join(" ",e.Errors)); }
            if (await db.ContentPages.IgnoreQueryFilters().AnyAsync(p => p.Id != id && p.Section == snapshot.Section && p.Slug == snapshot.Slug,ct)) return new(409,"The original page route is already in use.");
            page.Title=snapshot.Title;page.Section=snapshot.Section;page.Slug=snapshot.Slug;page.JsonLayout=snapshot.JsonLayout;
            page.IsPublished=snapshot.IsPublished;page.IsArchived=snapshot.IsArchived;page.IsDeleted=false;
        }
        else
        {
            if (page.IsDeleted) return new(409,"Restore the deleted page first.");
            if (action == "archive") { page.IsArchived=true;page.IsPublished=false; }
            else if (action == "unarchive") { page.IsArchived=false;page.IsPublished=false; }
            else if (action == "delete") db.ContentPages.Remove(page);
            else return new(400,"Unknown page action.");
        }
        db.AuditActor=actor;db.PageAuditAction=action;if (action != "delete") db.Entry(page).Property(p => p.UpdatedAt).IsModified=true;
        await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return new(204);
    }
}
