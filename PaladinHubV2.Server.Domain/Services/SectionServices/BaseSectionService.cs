using PaladinHubV2.Server.Domain.Services.IService;
using PaladinHub.Models;

namespace PaladinHubV2.Server.Domain.Services.SectionServices
{
	public abstract class BaseSectionService : ISectionService
	{
		public abstract string ControllerName { get; }

		public virtual string GetCoverImage() => string.Empty;

		public virtual string GetPageTitle(string actionName) => ControllerName;
		public virtual string GetPageText(string actionName) => string.Empty;

		public virtual List<NavButton> GetCurrentSectionButtons(string actionName)
		{
			if (SectionButtonsMap.TryGetValue(actionName ?? string.Empty, out var buttons) && buttons is { Count: > 0 })
				return buttons;

			return GetInternalAnchorButtons();
		}

		public virtual List<NavButton> GetOtherSectionButtons() =>
			AllButtons.Where(b => !b.IsAnchor).ToList();

		protected virtual List<NavButton> GetInternalAnchorButtons() => new()
		{
			new() { Url = "#rotation", Text = "Scroll to Rotation", Icon = "/images/icons/ui_spellbook_onebutton.jpg", IsAnchor = true },
			new() { Url = "#talents", Text = "Scroll to Talents", Icon = "/images/itemIcons/talents.jpg", IsAnchor = true }
		};

		protected virtual List<NavButton> AllButtons => new()
		{
			new() { Url = $"/{ControllerName}/Overview",     Text = "Overview",      Icon = "/images/SpellIcons/Divine Hammer.jpg" },
			new() { Url = $"/{ControllerName}/Gear",         Text = "BiS Gear",      Icon = "/images/itemIcons/inv_chest_plate_earthendungeon_c_01.jpg" },
			new() { Url = $"/{ControllerName}/Talents",      Text = "Talent Builds", Icon = "/images/itemIcons/talents.jpg" },
			new() { Url = $"/{ControllerName}/Consumables",  Text = "Consumables",   Icon = "/images/itemIcons/inv_potion_green.jpg" },
			new() { Url = $"/{ControllerName}/Rotation",     Text = "Rotation",      Icon = "/images/icons/ui_spellbook_onebutton.jpg" },
			new() { Url = $"/{ControllerName}/Stats",        Text = "Stats",         Icon = "/images/icons/inv_10_inscription2_repcontracts_scroll_02_uprez_color2.jpg" },
			new() { Url = $"/{ControllerName}/Overview",     Text = "CheatSheet",    Icon = "/images/itemIcons/inv_misc_note_03.jpg" },
			new() { Url = $"/{ControllerName}/WA-Addons",    Text = "WA & Addons",   Icon = "/images/icons/WA.png" }
		};

		protected virtual Dictionary<string, List<NavButton>> SectionButtonsMap => new()
		{
			["Consumables"] = new()
			{
				new() { Url = "#enchants",        Text = "Enchants",           Icon = "/images/itemIcons/inv_misc_enchantedscroll.jpg",                          IsAnchor = true },
				new() { Url = "#consumables",     Text = "Consumables",        Icon = "/images/itemIcons/inv_potion_green.jpg",                                   IsAnchor = true },
				new() { Url = "#food",            Text = "Food",               Icon = "/images/itemIcons/inv_misc_food_meat_cooked_02_color02.jpg",               IsAnchor = true },
				new() { Url = "#gems",            Text = "Gems",               Icon = "/images/itemIcons/inv_10_jewelcrafting_gem3primal_cut_red.jpg",            IsAnchor = true }
			},
			["Gear"] = new()
			{
				new() { Url = "#overall-bis",        Text = "Overall BiS",         Icon = "/images/SpellIcons/Divine Hammer.jpg",                                  IsAnchor = true },
				new() { Url = "#raid-mythic-bis",     Text = "Raid / Mythic+ BiS",  Icon = "/images/icons/inv_plate_raidpaladingoblin_d_01_helm.jpg",               IsAnchor = true },
				new() { Url = "#best-trinkets",       Text = "Best Trinkets",       Icon = "/images/icons/EyeOfKezan.jpg",                                           IsAnchor = true },
				new() { Url = "#upgrade-priorities",  Text = "Upgrade Priorities",  Icon = "/images/icons/inv_crestupgrade_undermine_gilded.jpg",                   IsAnchor = true },
				new() { Url = "#cyrces-circlet",      Text = "Cyrce's Circlet",     Icon = "/images/icons/Cyrces Circlet.jpg",                                       IsAnchor = true },
				new() { Url = "#corruptions",         Text = "Corruptions",         Icon = "/images/icons/inv_eyeofnzothpet.jpg",                                    IsAnchor = true },
				new() { Url = "#cartel-chip-usage",   Text = "Cartel Chip Usage",   Icon = "/images/icons/inv_misc_curiouscoin.jpg",                                 IsAnchor = true },
				new() { Url = "#crafted-gear",        Text = "Crafted Gear",        Icon = "/images/icons/inv_spark_whole_orange (1).jpg",                           IsAnchor = true }
			},
			["Overview"] = new()
			{
				new() { Url = "#rotation", Text = "Scroll to Rotation", Icon = "/images/icons/ui_spellbook_onebutton.jpg", IsAnchor = true },
				new() { Url = "#talents",  Text = "Scroll to Talents",  Icon = "/images/itemIcons/talents.jpg",             IsAnchor = true }
			},
			["Rotation"] = new()
			{
				new() { Url = "#how-to-play",                 Text = "How to Play",                      Icon = "/images/SpellIcons/Unending Light.jpg",             IsAnchor = true },
				new() { Url = "#rotation-and-spell-priority", Text = "Rotation and Spell Priority",      Icon = "/images/SpellIcons/Aura Mastery.jpg",               IsAnchor = true },
				new() { Url = "#single-button-rotation",      Text = "Single Button Rotation Assistant", Icon = "/images/icons/ui_spellbook_onebutton.jpg",          IsAnchor = true },
				new() { Url = "#major-cooldown-usage",        Text = "Major Cooldown Usage",             Icon = "/images/SpellIcons/Divine Toll.jpg",                IsAnchor = true },
				new() { Url = "#advanced-insights",           Text = "Advanced Insights",                 Icon = "/images/SpellIcons/Beacon of Virtue.jpg",           IsAnchor = true }
			},
			["Stats"] = new()
			{
				new() { Url = "#stats-overview-section", Text = "Stats Overview", Icon = "/images/SpellIcons/Divine Hammer.jpg",                           IsAnchor = true },
				new() { Url = "#best-stats-section",     Text = "Best Stats",     Icon = "/images/icons/inv_10_inscription2_repcontracts_scroll_02_uprez_color2.jpg", IsAnchor = true }
			},
			["Talents"] = new()
			{
				new() { Url = "#talents-tree-1", Text = "Scroll to Talents", Icon = "/images/itemIcons/talents.jpg", IsAnchor = true },
				new() { Url = "#talents-tree-2", Text = "Scroll to Talents", Icon = "/images/itemIcons/talents.jpg", IsAnchor = true }
			}
		};
	}
}
