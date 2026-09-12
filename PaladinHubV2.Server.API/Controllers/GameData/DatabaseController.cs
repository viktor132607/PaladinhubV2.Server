using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.ViewModels;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.API.Controllers.GameData
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/database")]
	public sealed class DatabaseController : ControllerBase
	{
		private readonly AppDbContext _db;

		public DatabaseController(AppDbContext db)
		{
			_db = db;
		}

		[HttpGet]
		public async Task<IActionResult> Index(
			[FromQuery] string? entity = "Spells",
			[FromQuery] string? search = null,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 20,
            [FromQuery] int? categoryId = null,
            [FromQuery] int? disciplineId = null,
            [FromQuery] int? tagId = null,
			CancellationToken cancellationToken = default)
		{
			var selectedEntity = ParseEntity(entity);
			var normalizedSearch = search?.Trim() ?? string.Empty;
            // A parent filter includes all descendants, not just direct assignments.
            var categoryIds = new HashSet<int>();
            if (categoryId > 0)
            {
                var categories = await _db.Categories.AsNoTracking().Where(c => !c.IsDeleted).ToListAsync(cancellationToken);
                if (!categories.Any(c => c.Id == categoryId)) return BadRequest(new { message = "Category does not exist." });
                categoryIds.Add(categoryId.Value);
                var queue = new Queue<int>(); queue.Enqueue(categoryId.Value);
                while (queue.TryDequeue(out var parent))
                    foreach (var child in categories.Where(c => c.ParentId == parent))
                        if (categoryIds.Add(child.Id)) queue.Enqueue(child.Id);
            }

			page = Math.Max(page, 1);
            var disciplineIds = new List<int>();
            if (disciplineId > 0)
            {
                if (!await _db.GameDisciplines.AnyAsync(c => c.Id == disciplineId && !c.IsDeleted, cancellationToken))
                    return BadRequest(new { message = "Class or specialization does not exist." });
                disciplineIds = await _db.GameDisciplines.Where(c => !c.IsDeleted && (c.Id == disciplineId || c.ParentId == disciplineId))
                    .Select(c => c.Id).ToListAsync(cancellationToken);
            }
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
				var query = _db.Spells
					.AsNoTracking()
					.AsQueryable();
                if (categoryId == 0) query = query.Where(s => s.CategoryId == null);
                if (disciplineId == 0) query = query.Where(s => s.DisciplineId == null);
                else if (disciplineId > 0) query = query.Where(s => s.DisciplineId != null && disciplineIds.Contains(s.DisciplineId.Value));
                if (categoryId > 0) query = query.Where(s => s.CategoryId != null && categoryIds.Contains(s.CategoryId.Value));
                if (tagId == 0) query = query.Where(s => s.TagIds.Length == 0);
                else if (tagId > 0) query = query.Where(s => s.TagIds.Contains(tagId.Value));

				if (!string.IsNullOrWhiteSpace(normalizedSearch))
				{
					query = query.Where(spell =>
						spell.Name.Contains(normalizedSearch) ||
                        _db.GameTags.Any(t => !t.IsDeleted && spell.TagIds.Contains(t.Id) && t.Name.Contains(normalizedSearch)) ||
						(spell.Description ?? string.Empty)
							.Contains(normalizedSearch));
				}

				model.Total = await query.CountAsync(
					cancellationToken);

				var totalPages = Math.Max(
					1,
					(int)Math.Ceiling(
						model.Total / (double)pageSize));

				model.Page = Math.Min(page, totalPages);

				model.Spells = await query
					.OrderBy(spell => spell.Name)
					.Skip((model.Page - 1) * pageSize)
					.Take(pageSize)
					.ToListAsync(cancellationToken);
			}
			else
			{
				var query = _db.Items
					.AsNoTracking()
					.AsQueryable();
                if (categoryId == 0) query = query.Where(i => i.CategoryId == null);
                if (disciplineId == 0) query = query.Where(i => i.DisciplineId == null);
                else if (disciplineId > 0) query = query.Where(i => i.DisciplineId != null && disciplineIds.Contains(i.DisciplineId.Value));
                if (categoryId > 0) query = query.Where(i => i.CategoryId != null && categoryIds.Contains(i.CategoryId.Value));
                if (tagId == 0) query = query.Where(i => i.TagIds.Length == 0);
                else if (tagId > 0) query = query.Where(i => i.TagIds.Contains(tagId.Value));

				if (!string.IsNullOrWhiteSpace(normalizedSearch))
				{
					query = query.Where(item =>
						item.Name.Contains(normalizedSearch) ||
                        _db.GameTags.Any(t => !t.IsDeleted && item.TagIds.Contains(t.Id) && t.Name.Contains(normalizedSearch)) ||
						(item.Description ?? string.Empty)
							.Contains(normalizedSearch));
				}

				model.Total = await query.CountAsync(
					cancellationToken);

				var totalPages = Math.Max(
					1,
					(int)Math.Ceiling(
						model.Total / (double)pageSize));

				model.Page = Math.Min(page, totalPages);

				model.Items = await query
					.OrderBy(item => item.Name)
					.Skip((model.Page - 1) * pageSize)
					.Take(pageSize)
					.ToListAsync(cancellationToken);
			}

			return Ok(model);
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
}
