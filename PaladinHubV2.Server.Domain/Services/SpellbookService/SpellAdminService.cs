using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.SpellbookService
{
	public sealed class SpellAdminService : ISpellAdminService
	{
		private readonly AppDbContext _db;

		public SpellAdminService(AppDbContext db)
		{
			_db = db;
		}

		public void Normalize(Spell spell)
		{
			spell.Name = spell.Name.Trim();
			spell.Icon = NormalizeOptional(spell.Icon);
			spell.Description = NormalizeOptional(spell.Description);
			spell.Url = NormalizeOptional(spell.Url);
			spell.Quality = string.IsNullOrWhiteSpace(spell.Quality)
				? "spell"
				: spell.Quality.Trim().ToLowerInvariant();
		}

		public async Task<Spell> CreateAsync(
			Spell spell,
			CancellationToken cancellationToken = default)
		{
			spell.Id = 0;
			Normalize(spell);
			_db.Spells.Add(spell);
			await _db.SaveChangesAsync(cancellationToken);
			return spell;
		}

		public Task<Spell?> GetAsync(
			int id,
			CancellationToken cancellationToken = default)
		{
			return _db.Spells
				.AsNoTracking()
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);
		}

		public async Task<Spell?> UpdateAsync(
			int id,
			Spell spell,
			CancellationToken cancellationToken = default)
		{
			Spell? existing = await _db.Spells
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);

			if (existing == null)
			{
				return null;
			}

			existing.Name = spell.Name;
			existing.Icon = spell.Icon;
			existing.Description = spell.Description;
			existing.Url = spell.Url;
			existing.Quality = spell.Quality;
			Normalize(existing);

			await _db.SaveChangesAsync(cancellationToken);
			return existing;
		}

		public async Task<bool> DeleteAsync(
			int id,
			CancellationToken cancellationToken = default)
		{
			Spell? spell = await _db.Spells
				.FirstOrDefaultAsync(
					current => current.Id == id,
					cancellationToken);

			if (spell == null)
			{
				return false;
			}

			_db.Spells.Remove(spell);
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
