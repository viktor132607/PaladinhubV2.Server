using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder
{
	public sealed class PageService : IPageService
	{
		private readonly AppDbContext _db;
		private readonly IJsonLayoutValidator _validator;

		public PageService(AppDbContext db, IJsonLayoutValidator validator)
		{
			_db = db;
			_validator = validator;
		}

		public async Task<ContentPage?> GetByRouteAsync(string section, string slug)
		{
			section = (section ?? "").Trim().ToLowerInvariant();
			slug = (slug ?? "").Trim().ToLowerInvariant();

			return await _db.ContentPages
				.AsNoTracking()
				.FirstOrDefaultAsync(p => p.Section == section && p.Slug == slug);
		}

		public async Task<ContentPage?> GetByIdAsync(int id)
		{
			return await _db.ContentPages.FirstOrDefaultAsync(p => p.Id == id);
		}

		/// <summary>
		/// Небезопасен update (без optimistic concurrency). Ползвай само ако редът е “локален”.
		/// </summary>
		public async Task<bool> UpdateLayoutAsync(int id, string jsonLayout, string updatedBy)
		{
			var page = await _db.ContentPages.FirstOrDefaultAsync(p => p.Id == id);
			if (page == null) return false;

			// валидаторът може да хвърли при legacy JSON – не спираме запис в този метод
			try { _validator.ValidateOrThrow(jsonLayout); } catch { /* soft-fail */ }

			page.JsonLayout = string.IsNullOrWhiteSpace(jsonLayout) ? "[]" : jsonLayout;
			page.UpdatedBy = updatedBy;
			page.UpdatedAt = DateTime.UtcNow;

			await _db.SaveChangesAsync();
			return true;
		}

		/// <summary>
		/// Безопасен update с optimistic concurrency (RowVersion).
		/// </summary>
		public async Task<(bool ok, byte[]? newRowVersion)> UpdateLayoutSafeAsync(
			int id,
			string jsonLayout,
			byte[] rowVersion,
			string updatedBy)
		{
			_validator.ValidateOrThrow(jsonLayout);

			var page = await _db.ContentPages.FirstOrDefaultAsync(p => p.Id == id);
			if (page == null) return (false, null);

			// Кажи на EF коя версия очакваме да обновим
			_db.Entry(page).Property(p => p.RowVersion).OriginalValue = rowVersion ?? Array.Empty<byte>();

			page.JsonLayout = string.IsNullOrWhiteSpace(jsonLayout) ? "[]" : jsonLayout;
			page.UpdatedBy = updatedBy;
			page.UpdatedAt = DateTime.UtcNow;

			try
			{
				await _db.SaveChangesAsync();
				// При успешен запис EF ще е напълнил новата RowVersion
				return (true, page.RowVersion);
			}
			catch (DbUpdateConcurrencyException)
			{
				return (false, null);
			}
		}
	}
}
