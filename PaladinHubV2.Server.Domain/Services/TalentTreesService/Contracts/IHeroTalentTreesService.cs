using System.Collections.Generic;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Talents;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	/// <summary>Строи Hero дърветата за дадената специализация.</summary>
	public interface IHeroTalentTreesService
	{
		Dictionary<string, TalentTreeViewModel> GetHeroTrees(string specialization, List<Spell> spells);
		TalentTreeViewModel? GetHeroTree(string specialization, string key, List<Spell> spells);
	}
}
