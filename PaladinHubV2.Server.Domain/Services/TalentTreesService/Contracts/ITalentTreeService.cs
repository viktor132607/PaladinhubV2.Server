using System.Collections.Generic;
using System.Threading.Tasks;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Talents;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public enum TalentTreeSaveError
	{
		None,
		KeyRequired,
		DataRequired,
		KeyMismatch,
		NodesRequired,
		InvalidNode,
		DuplicateNode
	}

	public sealed record TalentTreeSaveResult(
		TalentTreeSaveError Error,
		string? DuplicateNodeId = null);

	public interface ITalentTreeService
	{
		Task<Dictionary<string, TalentTreeViewModel>> GetTalentTrees(string section, List<Spell> spells);
		Task<TalentTreeViewModel?> GetTalentTree(string key, string section, List<Spell> spells);
		Task SaveActiveStatesAsync(string key, List<NodeState> nodes);
		Task<TalentTreeSaveResult> ValidateAndSaveActiveStatesAsync(
			string? routeKey,
			SaveTreeRequest? request);
	}
}
