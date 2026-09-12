using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.Navigation;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.Navigation;

public enum NavigationError { None, Validation, NotFound, Conflict }
public sealed record NavigationResult(NavigationError Error, NavigationLink? Link = null, string? Message = null);

public sealed class NavigationAdminService(AppDbContext db)
{
    public async Task<List<NavigationAdminRow>> List(CancellationToken ct) => await db.NavigationLinks.AsNoTracking()
        .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(c => new NavigationAdminRow(
            c.Id, c.Name, c.Description, c.Href, c.Location, c.OpenNewTab, c.ParentId, c.SortOrder, c.IsArchived, c.IsDeleted, c.Version,
            0, db.NavigationLinks.Count(child => child.ParentId == c.Id && !child.IsDeleted))).ToListAsync(ct);

    public async Task<List<PublicNavigationRow>> Public(CancellationToken ct) => await db.NavigationLinks.AsNoTracking()
        .Where(c => !c.IsDeleted && !c.IsArchived && (c.ParentId == null || db.NavigationLinks.Any(p => p.Id == c.ParentId && !p.IsDeleted && !p.IsArchived)))
        .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
        .Select(c => new PublicNavigationRow(c.Id, c.Name, c.Href, c.Location, c.OpenNewTab, c.ParentId, c.SortOrder)).ToListAsync(ct);

    public async Task<List<NavigationRevision>> History(int id, CancellationToken ct) => await db.NavigationRevisions
        .AsNoTracking().Where(r => r.NavigationId == id).OrderByDescending(r => r.Version).ToListAsync(ct);

    public static bool SafeHref(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) || value.Contains('\\')) return false;
        value = value.Trim();
        return (value.StartsWith('/') && !value.StartsWith("//")) ||
            (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https");
    }

    public async Task<NavigationResult> Create(NavigationRequest request, string actor, CancellationToken ct)
    {
        await using var transaction = await new GameDataAssignmentService(db).BeginAsync(ct);
        var category = new NavigationLink();
        var error = await Validate(category.Id, request, ct);
        if (error is not null) return new(NavigationError.Validation, Message: error);
        Apply(category, request);
        db.NavigationLinks.Add(category);
        await db.SaveChangesAsync(ct);
        Record(category, "created", actor);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(NavigationError.None, category);
    }

    public async Task<NavigationResult> Edit(int id, NavigationRequest request, string actor, CancellationToken ct)
    {
        await using var transaction = await new GameDataAssignmentService(db).BeginAsync(ct);
        var category = await db.NavigationLinks.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (category is null) return new(NavigationError.NotFound);
        if (request.Version != category.Version) return Stale();
        var error = await Validate(id, request, ct);
        if (error is not null) return new(NavigationError.Validation, Message: error);
        var action = category.IsArchived == request.IsArchived ? "updated" : request.IsArchived ? "archived" : "unarchived";
        Apply(category, request);
        category.Version++;
        Record(category, action, actor);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(NavigationError.None, category);
    }

    public async Task<NavigationResult> Delete(int id, int version, string actor, CancellationToken ct)
    {
        await using var transaction = await new GameDataAssignmentService(db).BeginAsync(ct);
        var category = await db.NavigationLinks.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (category is null) return new(NavigationError.NotFound);
        if (version != category.Version) return Stale();
        if (await db.NavigationLinks.AnyAsync(c => c.ParentId == id && !c.IsDeleted, ct))
            return new(NavigationError.Conflict, Message: "Move child links before deleting this entry, or archive it.");
        category.IsDeleted = true;
        category.Version++;
        Record(category, "deleted", actor);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(NavigationError.None);
    }

    public async Task<NavigationResult> Restore(int id, RestoreNavigationRequest request, string actor, CancellationToken ct)
    {
        await using var transaction = await new GameDataAssignmentService(db).BeginAsync(ct);
        var category = await db.NavigationLinks.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return new(NavigationError.NotFound);
        if (request.Version != category.Version) return Stale();
        var revision = await db.NavigationRevisions.SingleOrDefaultAsync(r => r.Id == request.RevisionId && r.NavigationId == id, ct);
        if (revision is null) return new(NavigationError.NotFound);
        var snapshot = JsonSerializer.Deserialize<NavigationLink>(revision.Snapshot)!;
        if (snapshot.IsDeleted) return new(NavigationError.Validation, Message: "Select a revision before deletion.");
        var restored = new NavigationRequest(snapshot.Name, snapshot.Description, snapshot.Href, snapshot.Location, snapshot.OpenNewTab, snapshot.ParentId, snapshot.SortOrder, snapshot.IsArchived, category.Version);
        var error = await Validate(id, restored, ct);
        if (error is not null) return new(NavigationError.Conflict, Message: error);
        Apply(category, restored);
        category.IsDeleted = false;
        category.Version++;
        Record(category, "restored", actor);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(NavigationError.None, category);
    }

    private static NavigationResult Stale() => new(NavigationError.Conflict, Message: "This navigation link changed in another session. Refresh before saving.");

    private async Task<string?> Validate(int id, NavigationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required.";
        if (!SafeHref(request.Href)) return "Use a site path beginning with / or an http/https URL.";
        if (request.Location is not ("primary" or "utility")) return "Choose primary or utility navigation.";
        var categories = await db.NavigationLinks.AsNoTracking().ToListAsync(ct);
        if (categories.Any(c => c.Id != id && !c.IsDeleted && c.ParentId == request.ParentId && c.Location == request.Location &&
            string.Equals(c.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return "A navigation link with this name already exists under this menu.";
        if (request.ParentId is not null)
        {
            var owner = categories.Find(c => c.Id == request.ParentId && !c.IsDeleted);
            if (owner is not null && owner.Location != request.Location) return "Use the same menu location as the parent.";
            if (owner is null || owner.ParentId is not null) return "A child link must belong to a top-level link.";
            if (categories.Any(c => c.ParentId == id && !c.IsDeleted)) return "A link with children cannot become a child link.";
        }
        var seen = new HashSet<int> { id };
        var parentId = request.ParentId;
        while (parentId is not null)
        {
            if (!seen.Add(parentId.Value)) return "A link cannot belong to itself or its child links.";
            var parent = categories.Find(c => c.Id == parentId && !c.IsDeleted);
            if (parent is null) return "The parent link does not exist. Restore it first.";
            if (parent.IsArchived && !request.IsArchived) return "An active child link cannot belong to an archived parent link.";
            parentId = parent.ParentId;
        }
        if (categories.Any(c => c.ParentId == id && !c.IsDeleted && c.Location != request.Location)) return "Move child links to another parent before changing menu location.";
        if (request.IsArchived && categories.Any(c => c.ParentId == id && !c.IsDeleted && !c.IsArchived))
            return "Archive or move active child links first.";
        return null;
    }

    private static void Apply(NavigationLink category, NavigationRequest request)
    {
        category.Name = request.Name.Trim();
        category.Description = request.Description?.Trim() ?? "";
        category.Href = request.Href.Trim(); category.Location = request.Location; category.OpenNewTab = request.OpenNewTab;
        category.ParentId = request.ParentId;
        category.SortOrder = request.SortOrder;
        category.IsArchived = request.IsArchived;
    }

    private void Record(NavigationLink category, string action, string actor) => db.NavigationRevisions.Add(new NavigationRevision {
        NavigationId = category.Id, Version = category.Version, Action = action,
        Actor = actor, Snapshot = JsonSerializer.Serialize(category)
    });
}
