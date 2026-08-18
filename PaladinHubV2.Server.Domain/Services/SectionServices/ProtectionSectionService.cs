namespace PaladinHubV2.Server.Domain.Services.SectionServices
{
	public class ProtectionSectionService : BaseSectionService
	{
		public override string ControllerName => "Protection";

		public override string GetCoverImage() => "/images/ProtCoverV5.png";

		public override string GetPageTitle(string actionName) => (actionName ?? string.Empty) switch
		{
			"Overview" => "Protection Paladin Main Guide – The War Within",
			"Talents" => "Best Protection Paladin Talent Tree Builds – The War Within",
			"Gear" => "Protection Paladin Best in Slot – The War Within",
			"Stats" => "Protection Paladin Stat Priority – The War Within",
			"Consumables" => "Protection Paladin Consumables – The War Within",
			"Rotation" => "Protection Paladin Rotation Guide – The War Within",
			_ => "Protection Paladin Guide – The War Within"
		};

		public override string GetPageText(string actionName) => (actionName ?? string.Empty) switch
		{
			"Overview" => "Welcome to the 11.1.7 Season 2 Protection Paladin guide. This guide will help you master your Protection Paladin in all aspects of the game including raids and dungeons.",
			"Talents" => "This page covers the best Protection Paladin talent tree builds for Season 2 in raids and Mythic+, including exports to import these builds directly into the game.",
			"Gear" => "Gear is one of the most important elements in WoW to strengthen your Protection Paladin, providing massive amounts of stats as well as armor, procs, and set bonuses.",
			"Consumables" => "Consumables can add a vital amount of high-value secondary stats and buffs, providing another way for players to improve and customize their stats outside of gear.",
			"Stats" => "Stats are a key component when customizing your Protection Paladin for raiding and Mythic+.",
			"Rotation" => "Learn the best Protection Paladin rotation for The War Within Season 2. Details about how to excel at your Protection Paladin and the optimal rotation for all talent builds in dungeons and raids.",
			_ => "Protection Paladin guide contents for The War Within Season 2."
		};
	}
}
