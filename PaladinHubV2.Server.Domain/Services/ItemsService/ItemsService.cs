using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.ItemsService;

public class ItemsService : IItemsService
{
	private readonly AppDbContext _db;
	public ItemsService(AppDbContext db) => _db = db;

	public Task<List<Item>> GetAllAsync() =>
		_db.Items.AsNoTracking().OrderBy(i => i.Name).ToListAsync();

	public Task<Item?> GetByIdAsync(int id) =>
		_db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);

	public Task<List<Item>> SearchAsync(string? term)
	{
		var q = _db.Items.AsNoTracking().AsQueryable();
		if (!string.IsNullOrWhiteSpace(term))
			q = q.Where(i => i.Name!.Contains(term) || (i.Description ?? "").Contains(term));
		return q.OrderBy(i => i.Name).ToListAsync();
	}

	public async Task<(IReadOnlyList<Item> Items, int Total)> GetPagedAsync(int page, int pageSize, string? term = null)
	{
		var q = _db.Items.AsNoTracking().AsQueryable();
		if (!string.IsNullOrWhiteSpace(term))
			q = q.Where(i => i.Name!.Contains(term) || (i.Description ?? "").Contains(term));

		var total = await q.CountAsync();
		var items = await q.OrderBy(i => i.Name)
						   .Skip((page - 1) * pageSize)
						   .Take(pageSize)
						   .ToListAsync();
		return (items, total);
	}
}
