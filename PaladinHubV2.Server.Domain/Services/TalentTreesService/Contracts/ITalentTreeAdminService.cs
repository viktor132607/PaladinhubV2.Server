using System.Collections.Generic;
using System.Threading.Tasks;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public interface ITalentTreeAdminService
	{
		Task<Dictionary<string, bool>> GetStatesAsync(string key);
		Task SaveStatesAsync(string key, IDictionary<string, bool> states);
	}
}
