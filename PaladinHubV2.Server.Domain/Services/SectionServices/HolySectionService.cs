namespace PaladinHubV2.Server.Domain.Services.SectionServices
{
	public class HolySectionService : BaseSectionService
	{
		public override string ControllerName => "Holy";

		public override string GetCoverImage() => "/images/TheHolyCover2.jpg";

		public override string GetPageTitle(string actionName) => (actionName ?? string.Empty) switch
		{
			"Overview" => "Holy Paladin Healer Guide - The War Within",
			"Talents" => "Best Holy Paladin Talent Tree Builds - The War Within",
			"Stats" => "Holy Paladin Stat Priority - The War Within",
			"Consumables" => "Holy Paladin Enchants & Consumables - The War Within",
			"Gear" => "Holy Paladin Gear and Best in Slot - The War Within",
			"Rotation" => "Holy Paladin Rotation Guide - The War Within",
			_ => "Holy Paladin Guide - The War Within"
		};

		public override string GetPageText(string actionName) => (actionName ?? string.Empty) switch
		{
			"Overview" => "\t\t\t\t\tHoly Paladin is a plate-wearing Healer specialization with a wide range of damage reduction and defensive abilities. We specialize in healing specific targets with large single-target heals, commonly referred to as “spot healing”. Holy Paladin gets access to the iconic Beacon of Light ability at level 16, which allows us to keep a consistent stream of healing on a specific target while healing other allies who might need it!\r\n\r\n\t\t\t\t\tBesides the classic Healer resource, Mana, Holy Paladins also utilize a secondary resource known as Holy Power, which functions similarly to Combo Points. Most of our spells generate this resource, which we can then use to cast our most powerful heals, Word of Glory and Light of Dawn.\r\n",
			"Talents" => "Here are all the best Holy Paladin Talent Tree builds in the Patch 11.1.7 & Season 2 for raids and Mythic+, including export links to import these builds directly into the game.\r\n\r\nFor recommended talent builds for each raid boss and Mythic+ dungeon, check out our Liberation of Undermine Raid Page and Mythic+ page.",
			"Gear" => "\t\t\t\t\tGear is one of the most important elements in WoW to strengthen your Holy Paladin, providing massive amounts of stats as well as armor, procs, and set bonuses.\r\n\r\n\t\t\t\t\tThis guide will explain how to obtain the best gear for your Holy Paladin in Patch 11.1.5 & Season 2 and how to check if a piece is Best in Slot (BiS), an upgrade, or just bad.\r\n\t\t\t\t\tThis guide will help you select the best pieces of gear from Dungeons and Raids in The War Within, whether they be weapons, trinkets, or armor.\r\n",
			"Consumables" => "Consumables are a vital part of high-level content in WoW, like Mythic+ Dungeons and Raids, providing additional ways for players to improve and customize their stats outside of gear.\r\n\r\nIn this guide, we will explain the best Holy Paladin gems, Holy Paladin flasks, Holy Paladin potions, and Holy Paladin enchants in Patch 11.1.7 & Season 2, as well as cheaper alternatives.\r\nBelow you will find the best Holy Paladin enchants and consumables. Make sure to also check our The War Within Profession Guide for all profession details, updated for Patch 11.1.7 & Season 2.",
			"Stats" => "Stats are a key component when customizing your Holy Paladin in World of Warcraft The War Within--having the right combination of them can be crucial to your performance.\r\n\r\nIn this guide, we will detail the best stat priority for your Holy Paladin, as well as provide explanations covering how to determine Holy Paladin stat priorities personalized for your character in Patch 11.1.7 & Season 2, as well as how to check if a piece of gear is BiS, upgrade or just bad for you.\r\n\r\nBesides talking about your Holy Paladin stat priority, we will also cover your stats in-depth, explaining nuances and synergies for niche situations that go beyond a generic Holy Paladin priority.",
			"Rotation" => "Learn the best Holy Paladin rotation for The War Within Season 2. Details about how to excel at your Holy Paladin and the optimal rotation for all talent builds in dungeons and raids for Patch 11.1.7 & Season 2.",
			_ => "Holy Paladin guide contents for The War Within Season 2."
		};
	}
}
