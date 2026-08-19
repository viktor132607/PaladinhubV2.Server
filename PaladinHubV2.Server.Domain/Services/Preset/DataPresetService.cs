using Microsoft.Extensions.Caching.Memory;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.Presets
{
	public sealed partial class DataPresetService : IDataPresetService
	{
		private readonly AppDbContext _db;
		private readonly IMemoryCache _cache;
		private readonly IReadOnlyDictionary<string, IPresetDataResolver> _resolvers;

		public DataPresetService(
			AppDbContext db,
			IMemoryCache cache,
			IEnumerable<IPresetDataResolver> resolvers)
		{
			_db = db;
			_cache = cache;
			_resolvers = resolvers.ToDictionary(
				resolver => resolver.Entity.Trim().ToLowerInvariant(),
				resolver => resolver,
				StringComparer.OrdinalIgnoreCase);
		}
	}
}
