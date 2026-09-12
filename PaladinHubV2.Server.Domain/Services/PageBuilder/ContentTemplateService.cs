using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;
namespace PaladinHubV2.Server.Domain.Services.PageBuilder;

public sealed record TemplateRequest(string Name, string Description, string JsonLayout, int Version);
public sealed record TemplateResult(int Status, string? Message = null, ContentTemplate? Template = null);
public sealed class ContentTemplateService(AppDbContext db, IJsonLayoutValidator validator)
{
    public Task<List<ContentTemplate>> List(string kind, CancellationToken ct) => db.ContentTemplates.AsNoTracking()
        .Where(t => t.Kind == kind).OrderBy(t => t.Name).ToListAsync(ct);
    public Task<List<ContentTemplateRevision>> History(Guid id, CancellationToken ct) => db.ContentTemplateRevisions.AsNoTracking()
        .Where(r => r.TemplateId == id).OrderByDescending(r => r.Version).ToListAsync(ct);

    public string? Validate(string kind, TemplateRequest request)
    {
        if (kind != "block") return "Unknown template kind.";
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100) return "Enter a name of 1–100 characters.";
        if ((request.Description?.Length ?? 0) > 1000) return "Description must be at most 1000 characters.";
        if (string.IsNullOrWhiteSpace(request.JsonLayout) || request.JsonLayout.Length > 500000) return "Content is required and must be under 500 KB.";
        try
        {
            validator.ValidateOrThrow(request.JsonLayout);
            using var json = JsonDocument.Parse(request.JsonLayout);
            if (json.RootElement.GetArrayLength() == 0 || json.RootElement.GetArrayLength() > 100) return "Choose between 1 and 100 blocks.";
        }
        catch (JsonLayoutValidationException e) { return string.Join(" ", e.Errors); }
        catch (JsonException) { return "Invalid template JSON."; }
        return null;
    }

    public async Task<TemplateResult> Save(Guid? id, string kind, TemplateRequest request, string actor, CancellationToken ct)
    {
        var error = Validate(kind, request);
        if (error is not null) return new(400, error);
        await using var tx = await new GameDataAssignmentService(db).BeginAsync(ct);
        var template = id.HasValue ? await db.ContentTemplates.SingleOrDefaultAsync(t => t.Id == id, ct) : new ContentTemplate { Kind = kind };
        if (template is null || template.Kind != kind) return new(404, "Template not found.");
        if (id.HasValue && template.Version != request.Version) return new(409, "Template changed. Reload the library first.");
        if (template.IsDeleted || template.IsArchived) return new(409, "Restore or unarchive the template before editing.");
        var name = request.Name.Trim();
        if (await db.ContentTemplates.AnyAsync(t => t.Id != template.Id && t.Kind == kind && !t.IsDeleted && t.Name.ToLower() == name.ToLower(), ct)) return new(409, "A template with this name already exists.");
        template.Name = name; template.Description = request.Description?.Trim() ?? ""; template.JsonLayout = request.JsonLayout;
        if (id.HasValue) template.Version++; else db.ContentTemplates.Add(template);
        Record(template, id.HasValue ? "updated" : "created", actor);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(200, Template: template);
    }

    public async Task<TemplateResult> Change(Guid id, int version, string action, Guid? revisionId, string actor, CancellationToken ct)
    {
        await using var tx = await new GameDataAssignmentService(db).BeginAsync(ct);
        var template = await db.ContentTemplates.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return new(404, "Template not found.");
        if (template.Version != version) return new(409, "Template changed. Reload the library first.");
        if (action == "restore")
        {
            var revision = await db.ContentTemplateRevisions.SingleOrDefaultAsync(r => r.TemplateId == id && r.Id == revisionId, ct);
            if (revision is null) return new(404, "Revision not found.");
            var previous = JsonSerializer.Deserialize<ContentTemplate>(revision.Snapshot)!;
            if (previous.IsDeleted) return new(400, "Select a revision before deletion.");
            var error = Validate(template.Kind, new(previous.Name, previous.Description, previous.JsonLayout, version));
            if (error is not null) return new(400, error);
            if (await db.ContentTemplates.AnyAsync(t => t.Id != id && t.Kind == template.Kind && !t.IsDeleted && t.Name.ToLower() == previous.Name.ToLower(), ct)) return new(409, "This template name is now in use. Rename the other template first.");
            template.Name = previous.Name; template.Description = previous.Description; template.JsonLayout = previous.JsonLayout;
            template.IsArchived = previous.IsArchived; template.IsDeleted = false;
        }
        else
        {
            if (template.IsDeleted) return new(409, "Restore the deleted template first.");
            if (action == "archive") template.IsArchived = true;
            else if (action == "unarchive") template.IsArchived = false;
            else if (action == "delete") { template.IsDeleted = true; template.IsArchived = true; }
            else return new(400, "Unknown action.");
        }
        template.Version++; Record(template, action, actor);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(200, Template: template);
    }
    private void Record(ContentTemplate template, string action, string actor) => db.ContentTemplateRevisions.Add(new()
    { TemplateId = template.Id, Version = template.Version, Action = action, Actor = actor[..Math.Min(actor.Length, 100)], Snapshot = JsonSerializer.Serialize(template) });
}
