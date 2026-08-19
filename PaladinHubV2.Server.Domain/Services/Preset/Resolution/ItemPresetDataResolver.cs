using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Presets
{
	public sealed class ItemPresetDataResolver : IPresetDataResolver
	{
		private readonly AppDbContext _db;

		public ItemPresetDataResolver(AppDbContext db)
		{
			_db = db;
		}

		public string Entity => "items";

		public async Task<IReadOnlyList<Dictionary<string, object?>>> ResolveAsync(
			JsonObject q,
			int? take,
			CancellationToken ct)
		{
			var name = q.TryGetPropertyValue("name", out var nameNode) ? nameNode?.ToString() : null;
			var quality = q.TryGetPropertyValue("quality", out var qualNode) ? qualNode?.ToString() : null;
			var slot = q.TryGetPropertyValue("slot", out var slotNode) ? slotNode?.ToString() : null;
			var source = q.TryGetPropertyValue("source", out var srcNode) ? srcNode?.ToString() : null;
			var limit = q.TryGetPropertyValue("limit", out var limNode) && int.TryParse(limNode?.ToString(), out var lim)
				? lim
				: (take ?? 50);

			IQueryable<Item> query = _db.Items.AsNoTracking();

			if (!string.IsNullOrWhiteSpace(name))
				query = query.Where(i => i.Name != null && i.Name.ToLower().Contains(name!.Trim().ToLower()));
			if (!string.IsNullOrWhiteSpace(quality))
				query = query.Where(i => i.Quality != null && i.Quality == quality);
			try { if (!string.IsNullOrWhiteSpace(slot)) query = query.Where(i => EF.Property<string>(i, "Slot") == slot); } catch { }
			try { if (!string.IsNullOrWhiteSpace(source)) query = query.Where(i => EF.Property<string>(i, "Source") == source); } catch { }

			var sort = q.TryGetPropertyValue("sort", out var sortNode) ? sortNode?.ToString() : null;
			if (!string.IsNullOrWhiteSpace(sort))
			{
				var s = sort!.Trim().ToLowerInvariant();
				if (s.Contains("score") && s.Contains("desc")) query = query.OrderByDescending(i => EF.Property<double?>(i, "Score"));
				else if (s.Contains("score")) query = query.OrderBy(i => EF.Property<double?>(i, "Score"));
				else if (s.Contains("name") && s.Contains("desc")) query = query.OrderByDescending(i => i.Name);
				else query = query.OrderBy(i => i.Name);
			}
			else
			{
				query = query.OrderBy(i => i.Name);
			}

			if (limit > 0) query = query.Take(limit);

			var rows = await query.Select(i => new
			{
				id = i.Id,
				item = i.Name,
				name = i.Name,
				icon = i.Icon,
				quality = i.Quality,
				source = EF.Property<string>(i, "Source"),
				slot = EF.Property<string>(i, "Slot")
			}).ToListAsync(ct);

			return rows.Select(PresetResolutionHelpers.AnonToDict).ToList();
		}
	}
}
