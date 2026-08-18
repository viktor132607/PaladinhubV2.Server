namespace PaladinHubV2.Server.Domain.Services.SectionServices
{
	public class RetributionSectionService : BaseSectionService
	{
		public override string ControllerName => "Retribution";

		public override string GetCoverImage() => "/images/RetributionCoverOrig.jpg";

		public override string GetPageTitle(string actionName) => actionName switch
		{
			"Overview" => "Retribution Paladin DPS Guide - The War Within",
			"Talents" => "Best Retribution Paladin Talent Tree Builds - The War Within",
			"Stats" => "Retribution Paladin Stat Priority - The War Within",
			"Consumables" => "Retribution Paladin Enchants & Consumables - The War Within",
			"Gear" => "Retribution Paladin Gear and Best in Slot - The War Within",
			"Rotation" => "Retribution Paladin Rotation Guide - The War Within",
			_ => $"{ControllerName} - {actionName}"
		};

		public override string GetPageText(string actionName) => actionName switch
		{
			"Overview" => "Welcome to Patch 11.1.7 & Season 2 Retribution Paladin guide. This guide will help you master your Retribution Paladin in all aspects of the game including raids and dungeons.",
			"Talents" => "Here are all the best Retribution Paladin Talent Tree builds in the Patch 11.1.7 & Season 2 for raids and Mythic+, including export links to import these builds directly into the game.\r\n\r\nFor recommended talent builds for each raid boss and Mythic+ dungeon, check out our Liberation of Undermine Raid Page and Mythic+ page.",
			"Gear" => "Gear is one of the most important elements in WoW to strengthen your Retribution Paladin, providing massive amounts of stats as well as armor, procs, and set bonuses.\r\n\r\nWe will explain how to obtain the best gear for your Retribution Paladin in Patch 11.1.7 & Season 2 and how to check if a piece is BiS, an upgrade, or just bad. This guide will help you select the best pieces of gear from Dungeons and Raids in The War Within, whether they be weapons, trinkets, or armor.\r\n",
			"Consumables" => "Consumables are a vital part of high-level content in WoW, like Mythic+ Dungeons and Raids, providing additional ways for players to improve and customize their stats outside of gear.\r\n\r\nIn this guide, we will explain the best Retribution Paladin gems, Retribution Paladin flasks, Retribution Paladin potions, and Retribution Paladin enchants in Patch 11.1.7 & Season 2, as well as cheaper alternatives.\r\nBelow you will find the best Retribution Paladin enchants and consumables. Make sure to also check our The War Within Profession Guide for all profession details, updated for Patch 11.1.7 & Season 2.\r\n",
			"Stats" => "Stats are a key component when customizing your Retribution Paladin in World of Warcraft The War Within--having the right combination of them can be crucial to your performance.\r\n\r\nIn this guide, we will detail the best stat priority for your Retribution Paladin, as well as provide explanations covering how to determine Retribution Paladin stat priorities personalized for your character in Patch 11.1.7 & Season 2, as well as how to check if a piece of gear is BiS, upgrade or just bad for you.\r\n\r\nBesides talking about your Retribution Paladin stat priority, we will also cover your stats in-depth, explaining nuances and synergies for niche situations that go beyond a generic Retribution Paladin priority.\r\n",
			"Rotation" => "Learn the best Retribution Paladin rotation for The War Within Season 2. Details about how to excel at your Retribution Paladin and the optimal rotation for all talent builds in dungeons and raids for Patch 11.1.7 & Season 2.",
			_ => string.Empty
		};
	}
}
