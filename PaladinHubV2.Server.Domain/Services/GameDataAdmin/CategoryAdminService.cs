using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum CategoryAdminError
{
	None,
	NotFound,
	Stale,
	Validation,
	InUse,
	RevisionNotFound,
	DeletedRevision
}

public sealed record CategoryAdminResult(
	CategoryAdminError Error,
	Category? Category = null,
	string? Message = null);

public sealed class CategoryAdminService
{
	private readonly AppDbContext _db;
	private readonly GameDataAssignmentService _assignments;

	public CategoryAdminService(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_db = db;
		_assignments = assignments;
	}

	public Task<List<CategoryListItem>> ListAsync(
		CancellationToken cancellationToken)
	{
		return _db.Categories
			.AsNoTracking()
			.OrderBy(item => item.SortOrder)
			.ThenBy(item => item.Name)
			.Select(item => new CategoryListItem(
				item.Id,
				item.Name,
				item.Description,
				item.ParentId,
				item.SortOrder,
				item.IsArchived,
				item.IsDeleted,
				item.Version,
				_db.Spells.Count(spell => spell.CategoryId == item.Id) +
				_db.Items.Count(product => product.CategoryId == item.Id),
				_db.Categories.Count(child =>
					child.ParentId == item.Id && !child.IsDeleted)))
			.ToListAsync(cancellationToken);
	}

	public Task<List<CategoryRevision>> HistoryAsync(
		int id,
		CancellationToken cancellationToken)
	{
		return _db.CategoryRevisions
			.AsNoTracking()
			.Where(revision => revision.CategoryId == id)
			.OrderByDescending(revision => revision.Version)
			.ToListAsync(cancellationToken);
	}

	public async Task<CategoryAdminResult> CreateAsync(
		CategoryRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		var category = new Category();
		string? error = await ValidateAsync(
			category.Id,
			request,
			cancellationToken);

		if (error != null)
		{
			return new CategoryAdminResult(
				CategoryAdminError.Validation,
				Message: error);
		}

		Apply(category, request);
		_db.Categories.Add(category);
		await _db.SaveChangesAsync(cancellationToken);
		Record(category, "created", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new CategoryAdminResult(
			CategoryAdminError.None,
			category);
	}

	public async Task<CategoryAdminResult> UpdateAsync(
		int id,
		CategoryRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		Category? category = await _db.Categories.SingleOrDefaultAsync(
			item => item.Id == id && !item.IsDeleted,
			cancellationToken);

		if (category == null)
		{
			return new CategoryAdminResult(CategoryAdminError.NotFound);
		}

		if (request.Version != category.Version)
		{
			return new CategoryAdminResult(CategoryAdminError.Stale);
		}

		string? error = await ValidateAsync(
			id,
			request,
			cancellationToken);

		if (error != null)
		{
			return new CategoryAdminResult(
				CategoryAdminError.Validation,
				Message: error);
		}

		string action = category.IsArchived == request.IsArchived
			? "updated"
			: request.IsArchived ? "archived" : "unarchived";

		Apply(category, request);
		category.Version++;
		Record(category, action, actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new CategoryAdminResult(
			CategoryAdminError.None,
			category);
	}

	public async Task<CategoryAdminResult> DeleteAsync(
		int id,
		int version,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		Category? category = await _db.Categories.SingleOrDefaultAsync(
			item => item.Id == id && !item.IsDeleted,
			cancellationToken);

		if (category == null)
		{
			return new CategoryAdminResult(CategoryAdminError.NotFound);
		}

		if (version != category.Version)
		{
			return new CategoryAdminResult(CategoryAdminError.Stale);
		}

		bool inUse =
			await _db.Categories.AnyAsync(
				item => item.ParentId == id && !item.IsDeleted,
				cancellationToken) ||
			await _db.Spells.AnyAsync(
				spell => spell.CategoryId == id,
				cancellationToken) ||
			await _db.Items.AnyAsync(
				item => item.CategoryId == id,
				cancellationToken);

		if (inUse)
		{
			return new CategoryAdminResult(
				CategoryAdminError.InUse,
				Message:
					"Move the subcategories and assigned records before deleting this category, or archive it.");
		}

		category.IsDeleted = true;
		category.Version++;
		Record(category, "deleted", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new CategoryAdminResult(CategoryAdminError.None);
	}

	public async Task<CategoryAdminResult> RestoreAsync(
		int id,
		RevisionRestoreRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		Category? category = await _db.Categories.SingleOrDefaultAsync(
			item => item.Id == id,
			cancellationToken);

		if (category == null)
		{
			return new CategoryAdminResult(CategoryAdminError.NotFound);
		}

		if (request.Version != category.Version)
		{
			return new CategoryAdminResult(CategoryAdminError.Stale);
		}

		CategoryRevision? revision = await _db.CategoryRevisions
			.SingleOrDefaultAsync(
				item =>
					item.Id == request.RevisionId &&
					item.CategoryId == id,
				cancellationToken);

		if (revision == null)
		{
			return new CategoryAdminResult(
				CategoryAdminError.RevisionNotFound);
		}

		Category snapshot =
			JsonSerializer.Deserialize<Category>(revision.Snapshot)!;

		if (snapshot.IsDeleted)
		{
			return new CategoryAdminResult(
				CategoryAdminError.DeletedRevision,
				Message: "Select a revision before deletion.");
		}

		var restored = new CategoryRequest(
			snapshot.Name,
			snapshot.Description,
			snapshot.ParentId,
			snapshot.SortOrder,
			snapshot.IsArchived,
			category.Version);

		string? error = await ValidateAsync(
			id,
			restored,
			cancellationToken);

		if (error != null)
		{
			return new CategoryAdminResult(
				CategoryAdminError.Validation,
				Message: error);
		}

		Apply(category, restored);
		category.IsDeleted = false;
		category.Version++;
		Record(category, "restored", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new CategoryAdminResult(
			CategoryAdminError.None,
			category);
	}

	private async Task<string?> ValidateAsync(
		int id,
		CategoryRequest request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
		{
			return "Name is required.";
		}

		List<Category> categories = await _db.Categories
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		if (categories.Any(item =>
				item.Id != id &&
				!item.IsDeleted &&
				item.ParentId == request.ParentId &&
				string.Equals(
					item.Name,
					request.Name.Trim(),
					StringComparison.OrdinalIgnoreCase)))
		{
			return "A category with this name already exists under this parent.";
		}

		var seen = new HashSet<int> { id };
		int? parentId = request.ParentId;

		while (parentId is not null)
		{
			if (!seen.Add(parentId.Value))
			{
				return "A category cannot be placed inside itself or its descendants.";
			}

			Category? parent = categories.Find(item =>
				item.Id == parentId && !item.IsDeleted);

			if (parent == null)
			{
				return "Parent category does not exist. Restore it first.";
			}

			if (parent.IsArchived && !request.IsArchived)
			{
				return "An active category cannot have an archived parent.";
			}

			parentId = parent.ParentId;
		}

		if (request.IsArchived && categories.Any(item =>
				item.ParentId == id &&
				!item.IsDeleted &&
				!item.IsArchived))
		{
			return "Archive or move the active subcategories first.";
		}

		return null;
	}

	private static void Apply(Category category, CategoryRequest request)
	{
		category.Name = request.Name.Trim();
		category.Description = request.Description?.Trim() ?? string.Empty;
		category.ParentId = request.ParentId;
		category.SortOrder = request.SortOrder;
		category.IsArchived = request.IsArchived;
	}

	private void Record(Category category, string action, string actor)
	{
		_db.CategoryRevisions.Add(new CategoryRevision
		{
			CategoryId = category.Id,
			Version = category.Version,
			Action = action,
			Actor = actor,
			Snapshot = JsonSerializer.Serialize(category)
		});
	}
}
