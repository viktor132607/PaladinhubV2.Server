using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Presets
{
	public sealed partial class DataPresetService
	{
		public async Task<DataPreset?> GetAsync(
			int id,
			CancellationToken ct = default) =>
			await _db.DataPresets
				.AsNoTracking()
				.FirstOrDefaultAsync(p => p.Id == id, ct);

		public async Task<IReadOnlyList<DataPreset>> ListAsync(
			string? entity = null,
			string? section = null,
			CancellationToken ct = default)
		{
			var q = _db.DataPresets.AsNoTracking().AsQueryable();
			if (!string.IsNullOrWhiteSpace(entity)) q = q.Where(p => p.Entity == entity);
			if (!string.IsNullOrWhiteSpace(section)) q = q.Where(p => p.Section == section);
			return await q.OrderBy(p => p.Entity).ThenBy(p => p.Name).ToListAsync(ct);
		}

		public async Task<DataPreset> CreateAsync(
			string name,
			string entity,
			string jsonQuery,
			string? section,
			CancellationToken ct = default)
		{
			var row = new DataPreset
			{
				Name = name.Trim(),
				Entity = entity.Trim(),
				Section = section?.Trim(),
				JsonQuery = jsonQuery?.Trim() ?? "{}"
			};
			_db.DataPresets.Add(row);
			await _db.SaveChangesAsync(ct);
			InvalidateCache(row);
			return row;
		}

		public async Task<DataPreset?> UpdateAsync(
			int id,
			string? name,
			string? jsonQuery,
			string? section,
			CancellationToken ct = default)
		{
			var row = await _db.DataPresets.FirstOrDefaultAsync(x => x.Id == id, ct);
			if (row == null) return null;

			if (!string.IsNullOrWhiteSpace(name)) row.Name = name.Trim();
			if (jsonQuery != null) row.JsonQuery = jsonQuery.Trim();
			if (section != null) row.Section = string.IsNullOrWhiteSpace(section) ? null : section.Trim();
			row.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync(ct);
			InvalidateCache(row);
			return row;
		}

		public async Task<bool> DeleteAsync(
			int id,
			CancellationToken ct = default)
		{
			var row = await _db.DataPresets.FirstOrDefaultAsync(x => x.Id == id, ct);
			if (row == null) return false;
			_db.DataPresets.Remove(row);
			await _db.SaveChangesAsync(ct);
			InvalidateCache(row);
			return true;
		}
	}
}
