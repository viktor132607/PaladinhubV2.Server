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
			CancellationToken cancellationToken = default)
		{
			var selectedEntity = ParseEntity(entity);
			var normalizedSearch = search?.Trim() ?? string.Empty;

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
				var query = _db.Spells
					.AsNoTracking()
					.AsQueryable();

				if (!string.IsNullOrWhiteSpace(normalizedSearch))
				{
					query = query.Where(spell =>
						spell.Name.Contains(normalizedSearch) ||
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

				if (!string.IsNullOrWhiteSpace(normalizedSearch))
				{
					query = query.Where(item =>
						item.Name.Contains(normalizedSearch) ||
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
