using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.GameData;
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
			SpellAdminRequest request,
			CancellationToken cancellationToken = default)
		{
			var spell = new Spell
			{
				Name = request.Name,
				Icon = request.Icon,
				Description = request.Description,
				Url = request.Url,
				Quality = request.Quality
			};

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
			SpellAdminRequest request,
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

			existing.Name = request.Name;
			existing.Icon = request.Icon;
			existing.Description = request.Description;
			existing.Url = request.Url;
			existing.Quality = request.Quality;

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
