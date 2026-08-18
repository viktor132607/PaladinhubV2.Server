using System.Collections.Generic;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Talents;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees
{
	/// <summary>
	/// Strategy интерфейс – по един builder за всяка специализация.
	/// </summary>
	public interface ISpecializationTreeBuilder
	{
		/// <summary>Ключ на спека: "holy" | "protection" | "retribution".</summary>
		string BaseKey { get; }

		/// <summary>Построява специализационното дърво.</summary>
		TalentTreeViewModel BuildTree(List<Spell> spells);
	}
}
