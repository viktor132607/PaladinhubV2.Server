using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.Presets
{
	public sealed class BuildNodePresetDataResolver : IPresetDataResolver
	{
		private readonly AppDbContext _db;

		public BuildNodePresetDataResolver(AppDbContext db)
		{
			_db = db;
		}

		public string Entity => "buildnodes";

		public async Task<IReadOnlyList<Dictionary<string, object?>>> ResolveAsync(
			JsonObject q,
			int? take,
			CancellationToken ct)
		{
			int limit = take ?? 100;
			var nodes = await _db.TalentNodeStates
				.AsNoTracking()
				.OrderBy(n => n.TreeKey)
				.ThenBy(n => n.NodeId)
				.Take(limit)
				.ToListAsync(ct);

			return nodes.Select(n => new Dictionary<string, object?>
			{
				["treeKey"] = n.TreeKey,
				["nodeId"] = n.NodeId,
				["isActive"] = n.IsActive
			}).ToList();
		}
	}
}
