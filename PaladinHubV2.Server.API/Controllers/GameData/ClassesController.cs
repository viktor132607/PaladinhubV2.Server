using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/classes")]
public sealed class ClassesController(AppDbContext db) : ControllerBase
{
    public sealed record DisciplineRequest([property: Required, MaxLength(100)] string Name,
        [property: MaxLength(2000)] string? Description, int? ParentId, int SortOrder, bool IsArchived, int Version);
    public sealed record RestoreRequest(Guid RevisionId, int Version);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.GameDisciplines.AsNoTracking()
        .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(c => new {
            c.Id, c.Name, c.Description, c.ParentId, c.SortOrder, c.IsArchived, c.IsDeleted, c.Version,
            usageCount = db.Spells.Count(s => s.DisciplineId == c.Id) + db.Items.Count(i => i.DisciplineId == c.Id),
            childCount = db.GameDisciplines.Count(child => child.ParentId == c.Id && !child.IsDeleted)
        }).ToListAsync(ct));

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id, CancellationToken ct) => Ok(await db.DisciplineRevisions
        .AsNoTracking().Where(r => r.DisciplineId == id).OrderByDescending(r => r.Version).ToListAsync(ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DisciplineRequest request, CancellationToken ct)
    {
        await using var transaction = await CategoryRules.BeginAsync(db, ct);
        var category = new GameDiscipline();
        var error = await Validate(category.Id, request, ct);
        if (error is not null) return BadRequest(new { message = error });
        Apply(category, request);
        db.GameDisciplines.Add(category);
        await db.SaveChangesAsync(ct);
        Record(category, "created");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(category);
    }

    [HttpPut("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DisciplineRequest request, CancellationToken ct)
    {
        await using var transaction = await CategoryRules.BeginAsync(db, ct);
        var category = await db.GameDisciplines.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
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
        var category = await db.GameDisciplines.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (category is null) return NotFound();
        if (version != category.Version) return Stale();
        if (await db.GameDisciplines.AnyAsync(c => c.ParentId == id && !c.IsDeleted, ct) ||
            await db.Spells.AnyAsync(s => s.DisciplineId == id, ct) || await db.Items.AnyAsync(i => i.DisciplineId == id, ct))
            return Conflict(new { message = "Move specializations and assigned records before deleting this entry, or archive it." });
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
        var category = await db.GameDisciplines.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return NotFound();
        if (request.Version != category.Version) return Stale();
        var revision = await db.DisciplineRevisions.SingleOrDefaultAsync(r => r.Id == request.RevisionId && r.DisciplineId == id, ct);
        if (revision is null) return NotFound();
        var snapshot = JsonSerializer.Deserialize<GameDiscipline>(revision.Snapshot)!;
        if (snapshot.IsDeleted) return BadRequest(new { message = "Select a revision before deletion." });
        var restored = new DisciplineRequest(snapshot.Name, snapshot.Description, snapshot.ParentId, snapshot.SortOrder, snapshot.IsArchived, category.Version);
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

    private IActionResult Stale() => Conflict(new { message = "This class or specialization changed in another session. Refresh before saving." });

    private async Task<string?> Validate(int id, DisciplineRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required.";
        var categories = await db.GameDisciplines.AsNoTracking().ToListAsync(ct);
        if (categories.Any(c => c.Id != id && !c.IsDeleted && c.ParentId == request.ParentId &&
            string.Equals(c.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return "A class or specialization with this name already exists under this class.";
        if (request.ParentId is not null)
        {
            var owner = categories.Find(c => c.Id == request.ParentId && !c.IsDeleted);
            if (owner is null || owner.ParentId is not null) return "A specialization must belong to a top-level class.";
            if (categories.Any(c => c.ParentId == id && !c.IsDeleted)) return "A class with specializations cannot become a specialization.";
        }
        var seen = new HashSet<int> { id };
        var parentId = request.ParentId;
        while (parentId is not null)
        {
            if (!seen.Add(parentId.Value)) return "A class cannot belong to itself or its specializations.";
            var parent = categories.Find(c => c.Id == parentId && !c.IsDeleted);
            if (parent is null) return "The class does not exist. Restore it first.";
            if (parent.IsArchived && !request.IsArchived) return "An active specialization cannot belong to an archived class.";
            parentId = parent.ParentId;
        }
        if (request.IsArchived && categories.Any(c => c.ParentId == id && !c.IsDeleted && !c.IsArchived))
            return "Archive or move active specializations first.";
        return null;
    }

    private static void Apply(GameDiscipline category, DisciplineRequest request)
    {
        category.Name = request.Name.Trim();
        category.Description = request.Description?.Trim() ?? "";
        category.ParentId = request.ParentId;
        category.SortOrder = request.SortOrder;
        category.IsArchived = request.IsArchived;
    }

    private void Record(GameDiscipline category, string action) => db.DisciplineRevisions.Add(new DisciplineRevision {
        DisciplineId = category.Id, Version = category.Version, Action = action,
        Actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin", Snapshot = JsonSerializer.Serialize(category)
    });
}
