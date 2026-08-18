using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Presets
{
	public interface IDataPresetService
	{
		// CRUD
		Task<DataPreset?> GetAsync(int id, CancellationToken ct = default);
		Task<IReadOnlyList<DataPreset>> ListAsync(string? entity = null, string? section = null, CancellationToken ct = default);
		Task<DataPreset> CreateAsync(string name, string entity, string jsonQuery, string? section, CancellationToken ct = default);
		Task<DataPreset?> UpdateAsync(int id, string? name, string? jsonQuery, string? section, CancellationToken ct = default);
		Task<bool> DeleteAsync(int id, CancellationToken ct = default);

		// Execute preset to a preview collection of rows (anonymous dictionaries)
		Task<IReadOnlyList<Dictionary<string, object?>>> ResolveAsync(int presetId, int? take = null, CancellationToken ct = default);
	}
}
