using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.SpellbookService;

public class SpellbookService : ISpellbookService
{
	private readonly AppDbContext _db;
	public SpellbookService(AppDbContext db) => _db = db;

	public Task<List<Spell>> GetAllAsync() =>
		_db.Spells.AsNoTracking().OrderBy(s => s.Name).ToListAsync();

	public Task<Spell?> GetByIdAsync(int id) =>
		_db.Spells.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);

	public Task<List<Spell>> SearchAsync(string? term)
	{
		var q = _db.Spells.AsNoTracking().AsQueryable();
		if (!string.IsNullOrWhiteSpace(term))
			q = q.Where(s => s.Name!.Contains(term) || (s.Description ?? "").Contains(term));
		return q.OrderBy(s => s.Name).ToListAsync();
	}

	public async Task<(IReadOnlyList<Spell> Items, int Total)> GetPagedAsync(int page, int pageSize, string? term = null)
	{
		var q = _db.Spells.AsNoTracking().AsQueryable();
		if (!string.IsNullOrWhiteSpace(term))
			q = q.Where(s => s.Name!.Contains(term) || (s.Description ?? "").Contains(term));

		var total = await q.CountAsync();
		var items = await q.OrderBy(s => s.Name)
						   .Skip((page - 1) * pageSize)
						   .Take(pageSize)
						   .ToListAsync();
		return (items, total);
	}
}
