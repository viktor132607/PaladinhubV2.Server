using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/categories")]
public sealed class CategoriesController(AppDbContext db) : ControllerBase
{
    public sealed record CategoryRequest([property: Required, MaxLength(100)] string Name,
        [property: MaxLength(2000)] string? Description, int? ParentId, int SortOrder, bool IsArchived, int Version);
    public sealed record RestoreRequest(Guid RevisionId, int Version);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Categories.AsNoTracking()
        .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(c => new {
            c.Id, c.Name, c.Description, c.ParentId, c.SortOrder, c.IsArchived, c.IsDeleted, c.Version,
            usageCount = db.Spells.Count(s => s.CategoryId == c.Id) + db.Items.Count(i => i.CategoryId == c.Id),
            childCount = db.Categories.Count(child => child.ParentId == c.Id && !child.IsDeleted)
        }).ToListAsync(ct));

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id, CancellationToken ct) => Ok(await db.CategoryRevisions
        .AsNoTracking().Where(r => r.CategoryId == id).OrderByDescending(r => r.Version).ToListAsync(ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryRequest request, CancellationToken ct)
    {
        await using var transaction = await CategoryRules.BeginAsync(db, ct);
        var category = new Category();
        var error = await Validate(category.Id, request, ct);
        if (error is not null) return BadRequest(new { message = error });
        Apply(category, request);
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        Record(category, "created");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(category);
    }

    [HttpPut("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategoryRequest request, CancellationToken ct)
    {
        await using var transaction = await CategoryRules.BeginAsync(db, ct);
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
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
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (category is null) return NotFound();
        if (version != category.Version) return Stale();
        if (await db.Categories.AnyAsync(c => c.ParentId == id && !c.IsDeleted, ct) ||
            await db.Spells.AnyAsync(s => s.CategoryId == id, ct) || await db.Items.AnyAsync(i => i.CategoryId == id, ct))
            return Conflict(new { message = "Move the subcategories and assigned records before deleting this category, or archive it." });
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
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return NotFound();
        if (request.Version != category.Version) return Stale();
        var revision = await db.CategoryRevisions.SingleOrDefaultAsync(r => r.Id == request.RevisionId && r.CategoryId == id, ct);
        if (revision is null) return NotFound();
        var snapshot = JsonSerializer.Deserialize<Category>(revision.Snapshot)!;
        if (snapshot.IsDeleted) return BadRequest(new { message = "Select a revision before deletion." });
        var restored = new CategoryRequest(snapshot.Name, snapshot.Description, snapshot.ParentId, snapshot.SortOrder, snapshot.IsArchived, category.Version);
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

    private IActionResult Stale() => Conflict(new { message = "This category changed in another session. Refresh before saving." });

    private async Task<string?> Validate(int id, CategoryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required.";
        var categories = await db.Categories.AsNoTracking().ToListAsync(ct);
        if (categories.Any(c => c.Id != id && !c.IsDeleted && c.ParentId == request.ParentId &&
            string.Equals(c.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return "A category with this name already exists under this parent.";
        var seen = new HashSet<int> { id };
        var parentId = request.ParentId;
        while (parentId is not null)
        {
            if (!seen.Add(parentId.Value)) return "A category cannot be placed inside itself or its descendants.";
            var parent = categories.Find(c => c.Id == parentId && !c.IsDeleted);
            if (parent is null) return "Parent category does not exist. Restore it first.";
            if (parent.IsArchived && !request.IsArchived) return "An active category cannot have an archived parent.";
            parentId = parent.ParentId;
        }
        if (request.IsArchived && categories.Any(c => c.ParentId == id && !c.IsDeleted && !c.IsArchived))
            return "Archive or move the active subcategories first.";
        return null;
    }

    private static void Apply(Category category, CategoryRequest request)
    {
        category.Name = request.Name.Trim();
        category.Description = request.Description?.Trim() ?? "";
        category.ParentId = request.ParentId;
        category.SortOrder = request.SortOrder;
        category.IsArchived = request.IsArchived;
    }

    private void Record(Category category, string action) => db.CategoryRevisions.Add(new CategoryRevision {
        CategoryId = category.Id, Version = category.Version, Action = action,
        Actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin", Snapshot = JsonSerializer.Serialize(category)
    });
}
