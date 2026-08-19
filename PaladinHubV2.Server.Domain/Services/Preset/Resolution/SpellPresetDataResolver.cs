using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Presets
{
	public sealed class SpellPresetDataResolver : IPresetDataResolver
	{
		private readonly AppDbContext _db;

		public SpellPresetDataResolver(AppDbContext db)
		{
			_db = db;
		}

		public string Entity => "spells";

		public async Task<IReadOnlyList<Dictionary<string, object?>>> ResolveAsync(
			JsonObject q,
			int? take,
			CancellationToken ct)
		{
			var name = q.TryGetPropertyValue("name", out var nameNode) ? nameNode?.ToString() : null;
			var limit = q.TryGetPropertyValue("limit", out var limNode) && int.TryParse(limNode?.ToString(), out var lim)
				? lim
				: (take ?? 50);

			IQueryable<Spell> query = _db.Spells.AsNoTracking();
			if (!string.IsNullOrWhiteSpace(name))
				query = query.Where(s => s.Name != null && s.Name.ToLower().Contains(name!.Trim().ToLower()));
			query = query.OrderBy(s => s.Name);

			if (limit > 0) query = query.Take(limit);

			var rows = await query.Select(s => new
			{
				id = s.Id,
				spell = s.Name,
				name = s.Name,
				icon = s.Icon,
				description = s.Description
			}).ToListAsync(ct);

			return rows.Select(PresetResolutionHelpers.AnonToDict).ToList();
		}
	}
}
