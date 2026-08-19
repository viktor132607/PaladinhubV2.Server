using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.GameData;
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
			ItemAdminRequest request,
			CancellationToken cancellationToken = default)
		{
			var item = new Item
			{
				Name = request.Name,
				Icon = request.Icon,
				SecondIcon = request.SecondIcon,
				Description = request.Description,
				Url = request.Url,
				ItemLevel = request.ItemLevel,
				RequiredLevel = request.RequiredLevel,
				Quality = request.Quality
			};

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
			ItemAdminRequest request,
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

			existing.Name = request.Name;
			existing.Icon = request.Icon;
			existing.SecondIcon = request.SecondIcon;
			existing.Description = request.Description;
			existing.Url = request.Url;
			existing.ItemLevel = request.ItemLevel;
			existing.RequiredLevel = request.RequiredLevel;
			existing.Quality = request.Quality;

			Normalize(existing);

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
