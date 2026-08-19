using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.ItemsService
{
	public sealed class ItemAdminService : IItemAdminService
	{
		private readonly AppDbContext _db;

		public ItemAdminService(AppDbContext db)
		{
			_db = db;
		}

		public void Normalize(Item item)
		{
			item.Name = item.Name.Trim();
			item.Icon = NormalizeOptional(item.Icon);
			item.SecondIcon = NormalizeOptional(item.SecondIcon);
			item.Description = NormalizeOptional(item.Description);
			item.Url = NormalizeOptional(item.Url);
			item.Quality = NormalizeOptional(item.Quality);
		}

		public async Task<Item> CreateAsync(
			Item item,
			CancellationToken cancellationToken = default)
		{
			item.Id = 0;
			Normalize(item);
			_db.Items.Add(item);
			await _db.SaveChangesAsync(cancellationToken);
			return item;
		}

		public Task<Item?> GetAsync(
			int id,
			CancellationToken cancellationToken = default)
		{
			return _db.Items
				.AsNoTracking()
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);
		}

		public async Task<Item?> UpdateAsync(
			int id,
			Item item,
			CancellationToken cancellationToken = default)
		{
			Item? existing = await _db.Items
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);

			if (existing == null)
			{
				return null;
			}

			existing.Name = item.Name.Trim();
			existing.Icon = NormalizeOptional(item.Icon);
			existing.SecondIcon = NormalizeOptional(item.SecondIcon);
			existing.Description = NormalizeOptional(item.Description);
			existing.Url = NormalizeOptional(item.Url);
			existing.ItemLevel = item.ItemLevel;
			existing.RequiredLevel = item.RequiredLevel;
			existing.Quality = NormalizeOptional(item.Quality);

			await _db.SaveChangesAsync(cancellationToken);
			return existing;
		}

		public async Task<bool> DeleteAsync(
			int id,
			CancellationToken cancellationToken = default)
		{
			Item? item = await _db.Items
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);

			if (item == null)
			{
				return false;
			}

			_db.Items.Remove(item);
			await _db.SaveChangesAsync(cancellationToken);
			return true;
		}

		private static string? NormalizeOptional(string? value)
		{
			return string.IsNullOrWhiteSpace(value)
				? null
				: value.Trim();
		}
	}
}
