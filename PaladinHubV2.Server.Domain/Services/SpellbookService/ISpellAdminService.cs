using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.SpellbookService
{
	public interface ISpellAdminService
	{
		void Normalize(Spell spell);
		Task<Spell> CreateAsync(Spell spell, CancellationToken cancellationToken = default);
		Task<Spell?> GetAsync(int id, CancellationToken cancellationToken = default);
		Task<Spell?> UpdateAsync(int id, Spell spell, CancellationToken cancellationToken = default);
		Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
	}
}
