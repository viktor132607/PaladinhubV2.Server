using System.Collections.Generic;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Talents;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	public interface IClassTreeBuilder
	{
		/// <summary>Базов ключ за дървото, напр. "paladin".</summary>
		string BaseKey { get; }

		/// <summary>Билдва класовото дърво (не-hero, не-специализация).</summary>
		TalentTreeViewModel BuildTree(List<Spell> spells);
	}
}
