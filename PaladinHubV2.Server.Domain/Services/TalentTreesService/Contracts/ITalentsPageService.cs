using PaladinHub.Models;
using PaladinHub.Models.Talents;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public interface ITalentsPageService
	{
		string? NormalizeSection(string? section);
		Task<TalentsSectionResult> GetSectionAsync(string section, CancellationToken cancellationToken = default);
		Task<TalentsTreeResult?> GetTreeAsync(string section, string key, CancellationToken cancellationToken = default);
	}

	public sealed record TalentsSectionResult(
		CombinedViewModel Model,
		string[] Keys);

	public sealed record TalentsTreeResult(
		CombinedViewModel Model,
		string ResolvedKey,
		string[] Keys,
		Dictionary<string, TalentTreeViewModel> TalentTrees);
}
