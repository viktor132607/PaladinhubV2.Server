using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;

namespace PaladinHubV2.Server.Domain.Services.Presets
{
	public sealed partial class DataPresetService
	{
		public async Task<IReadOnlyList<Dictionary<string, object?>>> ResolveAsync(
			int presetId,
			int? take = null,
			CancellationToken ct = default)
		{
			var preset = await GetAsync(presetId, ct) ??
				throw new KeyNotFoundException($"Preset {presetId} not found");

			var cacheKey = $"preset:{preset.Entity}:{preset.Id}:preview:{take}:{preset.UpdatedAt:O}";
			if (_cache.TryGetValue(
					cacheKey,
					out IReadOnlyList<Dictionary<string, object?>>? cached) &&
				cached is not null)
			{
				return cached;
			}

			JsonObject queryObj;
			try
			{
				queryObj = string.IsNullOrWhiteSpace(preset.JsonQuery)
					? new JsonObject()
					: JsonNode.Parse(preset.JsonQuery)!.AsObject();
			}
			catch
			{
				queryObj = new JsonObject();
			}

			string kind = (preset.Entity ?? string.Empty)
				.Trim()
				.ToLowerInvariant();

			IReadOnlyList<Dictionary<string, object?>> result =
				_resolvers.TryGetValue(kind, out IPresetDataResolver? resolver)
					? await resolver.ResolveAsync(queryObj, take, ct)
					: Array.Empty<Dictionary<string, object?>>();

			_cache.Set(
				cacheKey,
				result,
				new MemoryCacheEntryOptions
				{
					SlidingExpiration = TimeSpan.FromMinutes(10)
				});

			return result;
		}
	}
}
