using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public class TalentTreeAdminService : ITalentTreeAdminService
	{
		private readonly AppDbContext _db;
		public TalentTreeAdminService(AppDbContext db) => _db = db;

		public async Task<Dictionary<string, bool>> GetStatesAsync(string treeKey)
		{
			var dict = await _db.TalentNodeStates
				.Where(x => x.TreeKey == treeKey)
				.ToDictionaryAsync(x => x.NodeId, x => x.IsActive);

			return new Dictionary<string, bool>(dict, System.StringComparer.OrdinalIgnoreCase);
		}

		public async Task SaveStatesAsync(string treeKey, IDictionary<string, bool> states)
		{
			var existing = await _db.TalentNodeStates
				.Where(x => x.TreeKey == treeKey)
				.ToListAsync();

			foreach (var kv in states)
			{
				var row = existing.FirstOrDefault(x => x.NodeId == kv.Key);
				if (row == null)
				{
					_db.TalentNodeStates.Add(new TalentNodeState
					{
						TreeKey = treeKey,
						NodeId = kv.Key,
						IsActive = kv.Value
					});
				}
				else
				{
					row.IsActive = kv.Value;
				}
			}

			foreach (var row in existing)
			{
				if (!states.ContainsKey(row.NodeId))
					_db.TalentNodeStates.Remove(row);
			}

			await _db.SaveChangesAsync();
		}

		public async Task SetStateAsync(string treeKey, string nodeId, bool isActive)
		{
			var row = await _db.TalentNodeStates
				.FirstOrDefaultAsync(x => x.TreeKey == treeKey && x.NodeId == nodeId);

			if (row == null)
				_db.TalentNodeStates.Add(new TalentNodeState { TreeKey = treeKey, NodeId = nodeId, IsActive = isActive });
			else
				row.IsActive = isActive;

			await _db.SaveChangesAsync();
		}
	}
}
