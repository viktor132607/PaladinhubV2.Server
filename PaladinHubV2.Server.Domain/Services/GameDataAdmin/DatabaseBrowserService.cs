using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.ViewModels;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum DatabaseBrowseError
{
	None = 0,
	CategoryNotFound,
	DisciplineNotFound
}

public sealed record DatabaseBrowseResult(
	DatabaseBrowseError Error,
	AdminDatabaseIndexViewModel? Model = null);

public sealed class DatabaseBrowserService
{
	private readonly AppDbContext _db;

	public DatabaseBrowserService(AppDbContext db)
	{
		_db = db;
	}

	public async Task<DatabaseBrowseResult> BrowseAsync(
		string? entity,
		string? search,
		int page,
		int pageSize,
		int? categoryId,
		int? disciplineId,
		int? tagId,
		int? patchId,
		int? rarityId,
		CancellationToken cancellationToken)
	{
		AdminEntity selectedEntity = ParseEntity(entity);
		string normalizedSearch = search?.Trim() ?? string.Empty;

		HashSet<int> categoryIds = await ResolveCategoryIdsAsync(
			categoryId,
			cancellationToken);

		if (categoryId > 0 && categoryIds.Count == 0)
		{
			return new DatabaseBrowseResult(
				DatabaseBrowseError.CategoryNotFound);
		}

		List<int>? disciplineIds = await ResolveDisciplineIdsAsync(
			disciplineId,
			cancellationToken);

		if (disciplineId > 0 && disciplineIds == null)
		{
			return new DatabaseBrowseResult(
				DatabaseBrowseError.DisciplineNotFound);
		}

		page = Math.Max(page, 1);
		pageSize = Math.Clamp(pageSize, 1, 100);

		var model = new AdminDatabaseIndexViewModel
		{
			Entity = selectedEntity,
			Search = normalizedSearch,
			Page = page,
			PageSize = pageSize
		};

		if (selectedEntity == AdminEntity.Spells)
		{
			await PopulateSpellsAsync(
				model,
				normalizedSearch,
				categoryId,
				categoryIds,
				disciplineId,
				disciplineIds ?? [],
				tagId,
				patchId,
				cancellationToken);
		}
		else
		{
			await PopulateItemsAsync(
				model,
				normalizedSearch,
				categoryId,
				categoryIds,
				disciplineId,
				disciplineIds ?? [],
				tagId,
				patchId,
				rarityId,
				cancellationToken);
		}

		return new DatabaseBrowseResult(
			DatabaseBrowseError.None,
			model);
	}

	private async Task<HashSet<int>> ResolveCategoryIdsAsync(
		int? categoryId,
		CancellationToken cancellationToken)
	{
		var result = new HashSet<int>();
		if (categoryId is not > 0)
		{
			return result;
		}

		var categories = await _db.Categories
			.AsNoTracking()
			.Where(category => !category.IsDeleted)
			.Select(category => new
			{
				category.Id,
				category.ParentId
			})
			.ToListAsync(cancellationToken);

		if (!categories.Any(category => category.Id == categoryId.Value))
		{
			return result;
		}

		result.Add(categoryId.Value);
		var queue = new Queue<int>();
		queue.Enqueue(categoryId.Value);

		while (queue.TryDequeue(out int parentId))
		{
			foreach (var child in categories.Where(
					category => category.ParentId == parentId))
			{
				if (result.Add(child.Id))
				{
					queue.Enqueue(child.Id);
				}
			}
		}

		return result;
	}

	private async Task<List<int>?> ResolveDisciplineIdsAsync(
		int? disciplineId,
		CancellationToken cancellationToken)
	{
		if (disciplineId is not > 0)
		{
			return [];
		}

		bool exists = await _db.GameDisciplines.AnyAsync(
			discipline =>
				discipline.Id == disciplineId.Value &&
				!discipline.IsDeleted,
			cancellationToken);

		if (!exists)
		{
			return null;
		}

		return await _db.GameDisciplines
			.Where(discipline =>
				!discipline.IsDeleted &&
				(discipline.Id == disciplineId.Value ||
				 discipline.ParentId == disciplineId.Value))
			.Select(discipline => discipline.Id)
			.ToListAsync(cancellationToken);
	}

	private async Task PopulateSpellsAsync(
		AdminDatabaseIndexViewModel model,
		string search,
		int? categoryId,
		HashSet<int> categoryIds,
		int? disciplineId,
		List<int> disciplineIds,
		int? tagId,
		int? patchId,
		CancellationToken cancellationToken)
	{
		var query = _db.Spells.AsNoTracking().AsQueryable();

		if (categoryId == 0)
		{
			query = query.Where(spell => spell.CategoryId == null);
		}
		else if (categoryId > 0)
		{
			query = query.Where(spell =>
				spell.CategoryId != null &&
				categoryIds.Contains(spell.CategoryId.Value));
		}

		if (disciplineId == 0)
		{
			query = query.Where(spell => spell.DisciplineId == null);
		}
		else if (disciplineId > 0)
		{
			query = query.Where(spell =>
				spell.DisciplineId != null &&
				disciplineIds.Contains(spell.DisciplineId.Value));
		}

		if (patchId == 0)
		{
			query = query.Where(spell => spell.PatchId == null);
		}
		else if (patchId > 0)
		{
			query = query.Where(spell => spell.PatchId == patchId);
		}

		if (tagId == 0)
		{
			query = query.Where(spell => spell.TagIds.Length == 0);
		}
		else if (tagId > 0)
		{
			query = query.Where(spell => spell.TagIds.Contains(tagId.Value));
		}

		if (!string.IsNullOrWhiteSpace(search))
		{
			query = query.Where(spell =>
				spell.Name.Contains(search) ||
				_db.GameTags.Any(tag =>
					!tag.IsDeleted &&
					spell.TagIds.Contains(tag.Id) &&
					tag.Name.Contains(search)) ||
				(spell.Description ?? string.Empty).Contains(search));
		}

		model.Total = await query.CountAsync(cancellationToken);
		ClampPage(model);

		model.Spells = await query
			.OrderBy(spell => spell.Name)
			.Skip((model.Page - 1) * model.PageSize)
			.Take(model.PageSize)
			.ToListAsync(cancellationToken);
	}

	private async Task PopulateItemsAsync(
		AdminDatabaseIndexViewModel model,
		string search,
		int? categoryId,
		HashSet<int> categoryIds,
		int? disciplineId,
		List<int> disciplineIds,
		int? tagId,
		int? patchId,
		int? rarityId,
		CancellationToken cancellationToken)
	{
		var query = _db.Items.AsNoTracking().AsQueryable();

		if (categoryId == 0)
		{
			query = query.Where(item => item.CategoryId == null);
		}
		else if (categoryId > 0)
		{
			query = query.Where(item =>
				item.CategoryId != null &&
				categoryIds.Contains(item.CategoryId.Value));
		}

		if (disciplineId == 0)
		{
			query = query.Where(item => item.DisciplineId == null);
		}
		else if (disciplineId > 0)
		{
			query = query.Where(item =>
				item.DisciplineId != null &&
				disciplineIds.Contains(item.DisciplineId.Value));
		}

		if (rarityId == 0)
		{
			query = query.Where(item => item.RarityId == null);
		}
		else if (rarityId > 0)
		{
			query = query.Where(item => item.RarityId == rarityId);
		}

		if (patchId == 0)
		{
			query = query.Where(item => item.PatchId == null);
		}
		else if (patchId > 0)
		{
			query = query.Where(item => item.PatchId == patchId);
		}

		if (tagId == 0)
		{
			query = query.Where(item => item.TagIds.Length == 0);
		}
		else if (tagId > 0)
		{
			query = query.Where(item => item.TagIds.Contains(tagId.Value));
		}

		if (!string.IsNullOrWhiteSpace(search))
		{
			query = query.Where(item =>
				item.Name.Contains(search) ||
				_db.GameTags.Any(tag =>
					!tag.IsDeleted &&
					item.TagIds.Contains(tag.Id) &&
					tag.Name.Contains(search)) ||
				(item.Description ?? string.Empty).Contains(search));
		}

		model.Total = await query.CountAsync(cancellationToken);
		ClampPage(model);

		model.Items = await query
			.OrderBy(item => item.Name)
			.Skip((model.Page - 1) * model.PageSize)
			.Take(model.PageSize)
			.ToListAsync(cancellationToken);
	}

	private static void ClampPage(AdminDatabaseIndexViewModel model)
	{
		int totalPages = Math.Max(
			1,
			(int)Math.Ceiling(model.Total / (double)model.PageSize));

		model.Page = Math.Min(model.Page, totalPages);
	}

	private static AdminEntity ParseEntity(string? entity)
	{
		return string.Equals(
			entity?.Trim(),
			nameof(AdminEntity.Items),
			StringComparison.OrdinalIgnoreCase)
				? AdminEntity.Items
				: AdminEntity.Spells;
	}
}
