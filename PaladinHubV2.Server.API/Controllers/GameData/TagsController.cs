using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/tags")]
public sealed class TagsController(AppDbContext db) : ControllerBase
{
    public sealed record TagRequest([property: Required, MaxLength(100)] string Name,
        [property: MaxLength(2000)] string? Description, int SortOrder, bool IsArchived, int Version);
    public sealed record RestoreRequest(Guid RevisionId, int Version);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.GameTags.AsNoTracking()
        .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(c => new {
            c.Id, c.Name, c.Description, parentId = (int?)null, c.SortOrder, c.IsArchived, c.IsDeleted, c.Version,
            usageCount = db.Spells.Count(s => s.TagIds.Contains(c.Id)) + db.Items.Count(i => i.TagIds.Contains(c.Id)),
            childCount = 0
        }).ToListAsync(ct));

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id, CancellationToken ct) => Ok(await db.TagRevisions
        .AsNoTracking().Where(r => r.TagId == id).OrderByDescending(r => r.Version).ToListAsync(ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TagRequest request, CancellationToken ct)
    {
        await using var transaction = await CategoryRules.BeginAsync(db, ct);
        var category = new GameTag();
        var error = await Validate(category.Id, request, ct);
        if (error is not null) return BadRequest(new { message = error });
        Apply(category, request);
        db.GameTags.Add(category);
        await db.SaveChangesAsync(ct);
        Record(category, "created");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(category);
    }

    [HttpPut("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TagRequest request, CancellationToken ct)
    {
        await using var transaction = await CategoryRules.BeginAsync(db, ct);
        var category = await db.GameTags.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (category is null) return NotFound();
        if (request.Version != category.Version) return Stale();
        var error = await Validate(id, request, ct);
        if (error is not null) return BadRequest(new { message = error });
        var action = category.IsArchived == request.IsArchived ? "updated" : request.IsArchived ? "archived" : "unarchived";
        Apply(category, request);
        category.Version++;
        Record(category, action);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(category);
    }

    [HttpDelete("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, [FromQuery] int version, CancellationToken ct)
    {
        await using var transaction = await CategoryRules.BeginAsync(db, ct);
        var category = await db.GameTags.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (category is null) return NotFound();
        if (version != category.Version) return Stale();
        if (await db.Spells.AnyAsync(s => s.TagIds.Contains(id), ct) || await db.Items.AnyAsync(i => i.TagIds.Contains(id), ct))
            return Conflict(new { message = "Remove this tag from assigned records before deleting it, or archive it." });
        category.IsDeleted = true;
        category.Version++;
        Record(category, "deleted");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/restore"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, RestoreRequest request, CancellationToken ct)
    {
        await using var transaction = await CategoryRules.BeginAsync(db, ct);
        var category = await db.GameTags.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return NotFound();
        if (request.Version != category.Version) return Stale();
        var revision = await db.TagRevisions.SingleOrDefaultAsync(r => r.Id == request.RevisionId && r.TagId == id, ct);
        if (revision is null) return NotFound();
        var snapshot = JsonSerializer.Deserialize<GameTag>(revision.Snapshot)!;
        if (snapshot.IsDeleted) return BadRequest(new { message = "Select a revision before deletion." });
        var restored = new TagRequest(snapshot.Name, snapshot.Description, snapshot.SortOrder, snapshot.IsArchived, category.Version);
        var error = await Validate(id, restored, ct);
        if (error is not null) return Conflict(new { message = error });
        Apply(category, restored);
        category.IsDeleted = false;
        category.Version++;
        Record(category, "restored");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(category);
    }

    private IActionResult Stale() => Conflict(new { message = "This tag changed in another session. Refresh before saving." });

    private async Task<string?> Validate(int id, TagRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required.";
        var name = request.Name.Trim().ToLowerInvariant();
        return await db.GameTags.AnyAsync(t => t.Id != id && !t.IsDeleted && t.Name.ToLower() == name, ct)
            ? "A tag with this name already exists." : null;
    }

    private static void Apply(GameTag category, TagRequest request)
    {
        category.Name = request.Name.Trim();
        category.Description = request.Description?.Trim() ?? "";
        category.SortOrder = request.SortOrder;
        category.IsArchived = request.IsArchived;
    }

    private void Record(GameTag category, string action) => db.TagRevisions.Add(new TagRevision {
        TagId = category.Id, Version = category.Version, Action = action,
        Actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin", Snapshot = JsonSerializer.Serialize(category)
    });
}
