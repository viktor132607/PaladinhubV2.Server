using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Presets
{
	public sealed class DataPresetService : IDataPresetService
	{
		private readonly AppDbContext _db;
		private readonly IMemoryCache _cache;

		public DataPresetService(AppDbContext db, IMemoryCache cache)
		{
			_db = db;
			_cache = cache;
		}

		// ---------- CRUD ----------
		public async Task<DataPreset?> GetAsync(int id, CancellationToken ct = default)
			=> await _db.DataPresets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

		public async Task<IReadOnlyList<DataPreset>> ListAsync(string? entity = null, string? section = null, CancellationToken ct = default)
		{
			var q = _db.DataPresets.AsNoTracking().AsQueryable();
			if (!string.IsNullOrWhiteSpace(entity)) q = q.Where(p => p.Entity == entity);
			if (!string.IsNullOrWhiteSpace(section)) q = q.Where(p => p.Section == section);
			return await q.OrderBy(p => p.Entity).ThenBy(p => p.Name).ToListAsync(ct);
		}

		public async Task<DataPreset> CreateAsync(string name, string entity, string jsonQuery, string? section, CancellationToken ct = default)
		{
			var row = new DataPreset { Name = name.Trim(), Entity = entity.Trim(), Section = section?.Trim(), JsonQuery = jsonQuery?.Trim() ?? "{}" };
			_db.DataPresets.Add(row);
			await _db.SaveChangesAsync(ct);
			InvalidateCache(row);
			return row;
		}

		public async Task<DataPreset?> UpdateAsync(int id, string? name, string? jsonQuery, string? section, CancellationToken ct = default)
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

		public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
		{
			var row = await _db.DataPresets.FirstOrDefaultAsync(x => x.Id == id, ct);
			if (row == null) return false;
			_db.DataPresets.Remove(row);
			await _db.SaveChangesAsync(ct);
			InvalidateCache(row);
			return true;
		}

		// ---------- Resolve ----------
		public async Task<IReadOnlyList<Dictionary<string, object?>>> ResolveAsync(int presetId, int? take = null, CancellationToken ct = default)
		{
			var preset = await GetAsync(presetId, ct) ?? throw new KeyNotFoundException($"Preset {presetId} not found");

			var cacheKey = $"preset:{preset.Entity}:{preset.Id}:preview:{take}:{preset.UpdatedAt:O}";
			if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Dictionary<string, object?>>? cached) && cached is not null)
				return cached;

			// Parse JsonQuery (lenient)
			JsonObject queryObj;
			try
			{
				queryObj = string.IsNullOrWhiteSpace(preset.JsonQuery)
					? new JsonObject()
					: JsonNode.Parse(preset.JsonQuery)!.AsObject();
			}
			catch
			{
				queryObj = new JsonObject(); // не чупим UI-то
			}

			var kind = (preset.Entity ?? "").Trim().ToLowerInvariant();
			IReadOnlyList<Dictionary<string, object?>> result = kind switch
			{
				"items" => await ResolveItemsAsync(queryObj, take, ct),
				"spells" => await ResolveSpellsAsync(queryObj, take, ct),
				"buildnodes" => await ResolveBuildNodesAsync(queryObj, take, ct),
				_ => Array.Empty<Dictionary<string, object?>>()
			};

			_cache.Set(cacheKey, result, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(10) });
			return result;
		}

		// ---------- Helpers ----------

		private async Task<IReadOnlyList<Dictionary<string, object?>>> ResolveItemsAsync(JsonObject q, int? take, CancellationToken ct)
		{
			// Поддържани филтри (безопасно): name (contains), quality (eq), slot (eq), source (eq)
			// Допълнителни полета: season (ако съществува в ентитито ти) се игнорира, ако липсва.
			var name = q.TryGetPropertyValue("name", out var nameNode) ? nameNode?.ToString() : null;
			var quality = q.TryGetPropertyValue("quality", out var qualNode) ? qualNode?.ToString() : null;
			var slot = q.TryGetPropertyValue("slot", out var slotNode) ? slotNode?.ToString() : null;
			var source = q.TryGetPropertyValue("source", out var srcNode) ? srcNode?.ToString() : null;
			var limit = q.TryGetPropertyValue("limit", out var limNode) && int.TryParse(limNode?.ToString(), out var lim) ? lim : (take ?? 50);

			IQueryable<Item> query = _db.Items.AsNoTracking();

			if (!string.IsNullOrWhiteSpace(name)) query = query.Where(i => i.Name != null && i.Name.ToLower().Contains(name!.Trim().ToLower()));
			if (!string.IsNullOrWhiteSpace(quality)) query = query.Where(i => i.Quality != null && i.Quality == quality);
			// опит за slot/source ако има такива колони:
			try { if (!string.IsNullOrWhiteSpace(slot)) query = query.Where(i => EF.Property<string>(i, "Slot") == slot); } catch { }
			try { if (!string.IsNullOrWhiteSpace(source)) query = query.Where(i => EF.Property<string>(i, "Source") == source); } catch { }

			// сорт: score desc|name asc (ако има Score)
			var sort = q.TryGetPropertyValue("sort", out var sortNode) ? sortNode?.ToString() : null;
			if (!string.IsNullOrWhiteSpace(sort))
			{
				var s = sort!.Trim().ToLowerInvariant();
				if (s.Contains("score") && s.Contains("desc")) query = query.OrderByDescending(i => EF.Property<double?>(i, "Score"));
				else if (s.Contains("score")) query = query.OrderBy(i => EF.Property<double?>(i, "Score"));
				else if (s.Contains("name") && s.Contains("desc")) query = query.OrderByDescending(i => i.Name);
				else query = query.OrderBy(i => i.Name);
			}
			else query = query.OrderBy(i => i.Name);

			if (limit > 0) query = query.Take(limit);

			var rows = await query.Select(i => new
			{
				id = i.Id,
				item = i.Name,          // за колони с Kind=item → @Item(Name)
				name = i.Name,          // удобен alias
				icon = i.Icon,          // ако имаш колона Icon
				quality = i.Quality,
				source = EF.Property<string>(i, "Source"),
				slot = EF.Property<string>(i, "Slot")
			}).ToListAsync(ct);

			return rows.Select(AnonToDict).ToList();
		}

		private async Task<IReadOnlyList<Dictionary<string, object?>>> ResolveSpellsAsync(JsonObject q, int? take, CancellationToken ct)
		{
			var name = q.TryGetPropertyValue("name", out var nameNode) ? nameNode?.ToString() : null;
			var limit = q.TryGetPropertyValue("limit", out var limNode) && int.TryParse(limNode?.ToString(), out var lim) ? lim : (take ?? 50);

			IQueryable<Spell> query = _db.Spells.AsNoTracking();
			if (!string.IsNullOrWhiteSpace(name)) query = query.Where(s => s.Name != null && s.Name.ToLower().Contains(name!.Trim().ToLower()));
			query = query.OrderBy(s => s.Name);

			if (limit > 0) query = query.Take(limit);

			var rows = await query.Select(s => new
			{
				id = s.Id,
				spell = s.Name,     // за Kind=spell → @Spell(Name)
				name = s.Name,
				icon = s.Icon,
				description = s.Description
			}).ToListAsync(ct);

			return rows.Select(AnonToDict).ToList();
		}

		private async Task<IReadOnlyList<Dictionary<string, object?>>> ResolveBuildNodesAsync(JsonObject q, int? take, CancellationToken ct)
		{
			// placeholder – ако ти трябва: върни nodeId + state по подразбиране
			int limit = take ?? 100;
			var nodes = await _db.TalentNodeStates.AsNoTracking().OrderBy(n => n.TreeKey).ThenBy(n => n.NodeId).Take(limit).ToListAsync(ct);
			return nodes.Select(n => new Dictionary<string, object?>
			{
				["treeKey"] = n.TreeKey,
				["nodeId"] = n.NodeId,
				["isActive"] = n.IsActive
			}).ToList();
		}

		private static Dictionary<string, object?> AnonToDict(object o)
			=> o.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(o));

		private void InvalidateCache(DataPreset p)
		{
			// прост – разчитаме на UpdatedAt в cache key; достатъчно е
		}
	}
}
