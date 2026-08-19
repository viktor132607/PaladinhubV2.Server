using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.ViewModels;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameData
{
	public interface IAdminDatabaseService
	{
		Task<AdminDatabaseIndexViewModel> GetIndexAsync(
			string? entity,
			string? search,
			int page,
			int pageSize,
			CancellationToken cancellationToken = default);
	}

	public sealed class AdminDatabaseService : IAdminDatabaseService
	{
		private readonly AppDbContext _db;

		public AdminDatabaseService(AppDbContext db)
		{
			_db = db;
		}

		public async Task<AdminDatabaseIndexViewModel> GetIndexAsync(
			string? entity,
			string? search,
			int page,
			int pageSize,
			CancellationToken cancellationToken = default)
		{
			AdminEntity selectedEntity = ParseEntity(entity);
			string normalizedSearch = search?.Trim() ?? string.Empty;
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
				var query = _db.Spells.AsNoTracking().AsQueryable();
				if (!string.IsNullOrWhiteSpace(normalizedSearch))
				{
					query = query.Where(spell =>
						spell.Name.Contains(normalizedSearch) ||
						(spell.Description ?? string.Empty).Contains(normalizedSearch));
				}

				model.Total = await query.CountAsync(cancellationToken);
				int totalPages = Math.Max(
					1,
					(int)Math.Ceiling(model.Total / (double)pageSize));
				model.Page = Math.Min(page, totalPages);
				model.Spells = await query
					.OrderBy(spell => spell.Name)
					.Skip((model.Page - 1) * pageSize)
					.Take(pageSize)
					.ToListAsync(cancellationToken);
			}
			else
			{
				var query = _db.Items.AsNoTracking().AsQueryable();
				if (!string.IsNullOrWhiteSpace(normalizedSearch))
				{
					query = query.Where(item =>
						item.Name.Contains(normalizedSearch) ||
						(item.Description ?? string.Empty).Contains(normalizedSearch));
				}

				model.Total = await query.CountAsync(cancellationToken);
				int totalPages = Math.Max(
					1,
					(int)Math.Ceiling(model.Total / (double)pageSize));
				model.Page = Math.Min(page, totalPages);
				model.Items = await query
					.OrderBy(item => item.Name)
					.Skip((model.Page - 1) * pageSize)
					.Take(pageSize)
					.ToListAsync(cancellationToken);
			}

			return model;
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
