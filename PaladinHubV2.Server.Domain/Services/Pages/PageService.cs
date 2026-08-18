using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Pages
{
	public sealed class PageService : IPageService
	{
		private readonly AppDbContext _db;
		public PageService(AppDbContext db) { _db = db; }

		public Task<ContentPage?> GetByRouteAsync(string section, string slug)
		{
			section = (section ?? "").Trim().ToLower();
			slug = (slug ?? "").Trim().ToLower();
			return _db.ContentPages.FirstOrDefaultAsync(p => p.Section == section && p.Slug == slug);
		}

		public async Task<bool> UpdateLayoutAsync(int id, string jsonLayout, string updatedBy)
		{
			var page = await _db.ContentPages.FirstOrDefaultAsync(p => p.Id == id);
			if (page == null) return false;

			page.JsonLayout = string.IsNullOrWhiteSpace(jsonLayout) ? "[]" : jsonLayout;
			page.UpdatedBy = updatedBy;
			page.UpdatedAt = System.DateTime.UtcNow;

			await _db.SaveChangesAsync();
			return true;
		}
	}
}
