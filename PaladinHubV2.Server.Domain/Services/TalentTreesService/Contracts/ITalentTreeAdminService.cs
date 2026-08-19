using System.Collections.Generic;
using System.Threading.Tasks;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.Domain.Services.Common;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public interface ITalentTreeAdminService
	{
		Task<Dictionary<string, bool>> GetStatesAsync(string key);
		Task SaveStatesAsync(string key, IDictionary<string, bool> states);

		Task<OperationResult> SaveActiveStatesAsync(
			string key,
			IEnumerable<NodeState>? nodes);
	}
}
