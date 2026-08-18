using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Data.Seed.Contracts;

namespace PaladinHubV2.Server.Data.Seed
{
	public class ItemsSeeder : ISeeder
	{
		private readonly IServiceProvider _sp;
		public ItemsSeeder(IServiceProvider sp) => _sp = sp;

		public async Task SeedAsync()
		{
			using var scope = _sp.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

			if (await db.Items.AnyAsync()) return;

			var items = new[]
			{

			new Item
			{
				Id = 1,
				Name = "Greater Rune of the Void Ritual",
				Icon = "inv_inscription_majorglyph20.jpg",
				ItemLevel = 610,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/spell=469411/a-just-reward",
				Description = "Use: Apply Greater Rune of the Void Ritual to a helm. Gain Void Ritual, giving your spells and abilities a chance to increase all secondary stats by 82 every sec for 20 sec.\r\n\r\nCannot be applied to items lower than level 350. This effect is fleeting and will only work during The War Within Season 2."
			},
			new Item
			{
				Id = 2,
				Name = "Enchant Weapon - Council's Guile",
				Icon = "inv_inscription_minorglyph20.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=223759/enchant-weapon-councils-guile",
				Description = "Use: Earthen Enhancements - Wondrous Weapons\r\n\r\nPermanently enchants a weapon to sometimes grant you Keen Prowess, bestowing 3910 Critical Strike to you for 12 sec. Cannot be applied to items lower than level 350."
			},
			new Item
			{
				Id = 3,
				Name = "Lesser Rune of the Void Ritual",
				Icon = "inv_inscription_minorglyph20.jpg",
				ItemLevel = 580,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=212885/lesser-rune-of-the-void-ritual",
				Description = "Use: Apply Lesser Rune of the Void Ritual to a helm. Gain Void Ritual, giving your spells and abilities a chance to increase all secondary stats by 47 every sec for 20 sec.\r\n\r\nCannot be applied to items lower than level 350. This effect is fleeting and will only work during The War Within Season 2."
			},
			new Item
			{
				Id = 4,
				Name = "Flask of Tempered Swiftness",
				Icon = "inv_potion_green.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Common",
				RequiredLevel = 71,
				Url = "https://www.wowhead.com/item=212741/flask-of-tempered-swiftness",
				Description = "Use: Drink to increase your Haste by 2825.\r\n\r\nLasts 1 hour and through death. Consuming an identical flask will add another 1 hour."
			},
			new Item
			{
				Id = 5,
				Name = "Algari Mana Potion",
				Icon = "inv_flask_blue.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Common",
				RequiredLevel = 71,
				Url = "https://www.wowhead.com/item=212739/algari-mana-potion",
				Description = "Use: Restores 270000 mana."
			},
			new Item
			{
				Id = 6,
				Name = "Algari Healing Potion",
				Icon = "inv_flask_red.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Common",
				RequiredLevel = 71,
				Url = "https://www.wowhead.com/item=212738/algari-healing-potion",
				Description = "Use: Restores 3839450 health."
			},
			new Item
			{
				Id = 7,
				Name = "Algari Mana Oil",
				Icon = "trade_alchemy_potiond1.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 70,
				Quality = "Uncommon",
				RequiredLevel = 68,
				Url = "https://www.wowhead.com/item=212740/algari-mana-oil",
				Description = "Use: Coat your weapon in Algari Mana Oil, increasing your Critical Strike and Haste by 232 for 120 min.\r\n\r\n\"The oil radiates a subtle, but noticeable magical aura.\""
			},
			new Item
			{
				Id = 8,
				Name = "Crystallized Augment Rune",
				Icon = "inv_10_enchanting_crystal_color5.jpg",
				ItemLevel = 80,
				Quality = "Rare",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=224572/crystallized-augment-rune",
				Description = "Use: Increases Primary Stat by 733 for 1 hour. Augment Rune.\r\n\r\n\"Can be bought and sold on the auction house.\""
			},
			new Item
			{
				Id = 9,
				Name = "Feast of the Divine Day",
				Icon = "inv_11_cooking_profession_feast_table01.jpg",
				ItemLevel = 80,
				Quality = "Epic",
				RequiredLevel = 68,
				Url = "https://www.wowhead.com/item=222732/feast-of-the-divine-day",
				Description = "Use: Place a Mereldar Feast for all players to enjoy!\r\n\r\nRestores 9000000 health and 3000000 mana over 20 sec. Must remain seated while eating.\r\n\r\nWell Fed: If you spend at least 10 seconds eating you will become Well Fed and gain 446 primary stat for 1 hour."
			},
			new Item
			{
				Id = 10,
				Name = "Elusive Blasphemite",
				Icon = "inv_misc_gem_x4_metagem_cut.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213746/elusive-blasphemite",
				Description = "Unique-Equipped: Algari Diamond (1)\r\n+181 Primary Stat and +2% Movement Speed per unique Algari gem color"
			},
						new Item
			{
				Id = 11,
				Name = "Quick Onyx",
				Icon = "inv_jewelcrafting_cut-standart-gem-hybrid_color2_3.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 12,
				Name = "Flask of Tempered Mastery",
				Icon = "inv_potion_purlple.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Common",
				RequiredLevel = 71,
				Url = "https://www.wowhead.com/item=212280/flask-of-tempered-mastery",
				Description = "Use: Drink to increase your Mastery by 2825.\r\n\r\nLasts 1 hour and through death. Consuming an identical flask will add another 1 hour."
			},
			new Item
			{
				Id = 13,
				Name = "Slumbering Soul Serum",
				Icon = "inv_flask_green.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Common",
				RequiredLevel = 71,
				Url = "https://www.wowhead.com/item=212247/slumbering-soul-serum",
				Description = "Use: Elevate your focus to restore 375000 mana over 10 sec, but you are defenseless until your focus is broken."
			},
			new Item
			{
				Id = 14,
				Name = "Jester's Board",
				Icon = "inv_misc_food_meat_cooked_06.jpg",
				ItemLevel = 80,
				Quality = "Rare",
				RequiredLevel = 68,
				Url = "https://www.wowhead.com/item=222730/jesters-board",
				Description = "Use: Restores 5400000 health over 20 sec. Must remain seated while eating.\r\n\r\nWell Fed: If you spend at least 10 seconds eating you will become Well Fed and gain 470 of your highest secondary stat for 1 hour."
			},
			new Item
			{
				Id = 15,
				Name = "Deadly Emerald",
				Icon = "inv_jewelcrafting_cut-standart-gem-hybrid_color1_2.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213479/deadly-emerald",
				Description = "+147 Haste and +49 Critical Strike"
			},
			new Item
			{
				Id = 16,
				Name = "Tempered Potion",
				Icon = "trade_alchemy_potiona4.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Common",
				RequiredLevel = 71,
				Url = "https://www.wowhead.com/item=212265/tempered-potion",
				Description = "Use: Gain the effects of all inactive Tempered Flasks, increasing their associated secondary stats by 2617 for 30 sec."
			},
			new Item
			{
				Id = 17,
				Name = "Feast of the Midnight Masquerade",
				Icon = "inv_11_cooking_profession_feast_table02.jpg",
				ItemLevel = 80,
				Quality = "Epic",
				RequiredLevel = 68,
				Url = "https://www.wowhead.com/item=222733/feast-of-the-midnight-masquerade",
				Description = "Use: Place a Mereldar Feast for all players to enjoy!\r\n\r\nRestores 9000000 health and 3000000 mana over 20 sec. Must remain seated while eating.\r\n\r\nWell Fed: If you spend at least 10 seconds eating you will become Well Fed and gain 446 primary stat for 1 hour."
			},
			new Item
			{
				Id = 18,
				Name = "Outsider's Provisions",
				Icon = "inv_cooking_10_draconicdelicacies.jpg",
				ItemLevel = 80,
				Quality = "Rare",
				RequiredLevel = 68,
				Url = "https://www.wowhead.com/item=222731/outsiders-provisions",
				Description = "Use: Restores 5400000 health over 20 sec. Must remain seated while eating.\r\n\r\nWell Fed: If you spend at least 10 seconds eating you will become Well Fed and gain 470 of your highest secondary stat for 1 hour."
			},
			new Item
			{
				Id = 19,
				Name = "Empress' Farewell",
				Icon = "inv_misc_food_meat_cooked_02_color02.jpg",
				ItemLevel = 80,
				Quality = "Rare",
				RequiredLevel = 68,
				Url = "https://www.wowhead.com/item=222729/empress-farewell",
				Description = "Use: Restores 5400000 health over 20 sec. Must remain seated while eating.\r\n\r\nWell Fed: If you spend at least 10 seconds eating you will become Well Fed and gain 470 of your highest secondary stat for 1 hour."
			},
			new Item
			{
				Id = 20,
				Name = "Culminating Blasphemite",
				Icon = "item_cutmetagemb.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213743/culminating-blasphemite",
				Description = "Unique-Equipped: Algari Diamond (1)\r\n+181 Primary Stat and +0.15% Critical Effect per unique Algari gem color"
			},
						new Item
			{
				Id = 21,
				Name = "Beledar's Bounty",
				Icon = "inv_cooking_100_roastduck_color02.jpg",
				ItemLevel = 80,
				Quality = "Rare",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=222728/beledars-bounty",
				Description = "Use: Restores 5400000 health over 20 sec. Must remain seated while eating.\r\n\r\nWell Fed: If you spend at least 10 seconds eating you will become Well Fed and gain 470 of your highest secondary stat for 1 hour."
			},
			new Item
			{
				Id = 22,
				Name = "Masterful Emerald",
				Icon = "inv_jewelcrafting_cut-standart-gem-hybrid_color1_3.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213482/masterful-emerald",
				Description = "+147 Haste and +49 Mastery"
			},
			new Item
			{
				Id = 23,
				Name = "Masterful Ruby",
				Icon = "inv_jewelcrafting_cut-standart-gem-hybrid_color4_1.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213458/masterful-ruby",
				Description = "+147 Critical Strike and +49 Mastery"
			},
			new Item
			{
				Id = 24,
				Name = "Quick Ruby",
				Icon = "inv_jewelcrafting_cut-standart-gem-hybrid_color4_3.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213455/quick-ruby",
				Description = "+147 Critical Strike and +49 Haste"
			},
			new Item
			{
				Id = 25,
				Name = "Quick Sapphire",
				Icon = "inv_jewelcrafting_cut-standart-gem-hybrid_color5_3.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213470/quick-sapphire",
				Description = "+147 Versatility and +49 Haste"
			},
			new Item
			{
				Id = 26,
				Name = "Quick Onyx",
				Icon = "inv_jewelcrafting_cut-standart-gem-hybrid_color2_3.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 27,
				Name = "Masterful Sapphire",
				Icon = "inv_jewelcrafting_cut-standart-gem-hybrid_color5_1.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213473/masterful-sapphire",
				Description = "+147 Versatility and +49 Mastery"
			},
			new Item
			{
				Id = 28,
				Name = "Deadly Sapphire",
				Icon = "inv_jewelcrafting_cut-standart-gem-hybrid_color5_2.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213477/deadly-sapphire",
				Description = "+147 Versatility and +49 Critical Strike"
			},
			new Item
			{
				Id = 29,
				Name = "Daybreak Spellthread",
				Icon = "inv_10_tailoring_craftingoptionalreagent_enhancedspellthread_color3.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=222896/daybreak-spellthread",
				Description = "Use: Apply Daybreak Spellthread to your leggings, permanently increasing its Intellect by 930 and increasing the wearer's maximum mana by 5%."
			},
			new Item
			{
				Id = 30,
				Name = "Enchant Weapon - Authority of the Depths",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=223784/enchant-weapon-authority-of-the-depths",
				Description = "Use: Nerubian Novelties\r\n\r\nPermanently enchants a weapon with the Authority of the Depths. Damaging foes may invoke it, applying Suffocating Darkness which periodically inflicts 35003 Shadow damage. The darkness may deepen up to 3 times. Cannot be applied to items lower than level 350."
			},
						new Item
			{
				Id = 31,
				Name = "Daybreak Spellthread",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=222896/daybreak-spellthread",
				Description = "Use: Apply Daybreak Spellthread to your leggings, permanently increasing its Intellect by 930 and increasing the wearer's maximum mana by 5%."
			},
			new Item
			{
				Id = 32,
				Name = "Daybreak Spellthread",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=222896/daybreak-spellthread",
				Description = "Use: Apply Daybreak Spellthread to your leggings, permanently increasing its Intellect by 930 and increasing the wearer's maximum mana by 5%."
			},
			new Item
			{
				Id = 33,
				Name = "Daybreak Spellthread",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=222896/daybreak-spellthread",
				Description = "Use: Apply Daybreak Spellthread to your leggings, permanently increasing its Intellect by 930 and increasing the wearer's maximum mana by 5%."
			},
			new Item
			{
				Id = 34,
				Name = "Daybreak Spellthread",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=222896/daybreak-spellthread",
				Description = "Use: Apply Daybreak Spellthread to your leggings, permanently increasing its Intellect by 930 and increasing the wearer's maximum mana by 5%."
			},
			new Item
			{
				Id = 35,
				Name = "Weavercloth Spellthread",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 36,
				Name = "Enchant Boots - Defender's March",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 37,
				Name = "Enchant Ring - Glimmering Haste",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 38,
				Name = "Enchant Ring - Radiant Haste",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 39,
				Name = "Enchant Chest - Crystalline Radiance",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 40,
				Name = "Enchant Cloak - Chant of Winged Grace",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=223731/enchant-cloak-chant-of-winged-grace",
				Description = "Use: Nerubian Novelties - Tertiary Trivialities\r\n\r\nPermanently enchants a cloak with ancient magic that chants with power, granting 545 Avoidance and reducing fall damage by 0%. Cannot be applied to items lower than level 350."
			},
						new Item
			{
				Id = 41,
				Name = "Enchant Cloak - Whisper of Silken Avoidance",
				Icon = "inv_misc_enchantedscroll.jpg",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=223728/enchant-cloak-whisper-of-silken-avoidance",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 42,
				Name = "Enchant Bracer - Whisper of Armored Leech",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 43,
				Name = "Enchant Bracer - Chant of Armored Leech",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 44,
				Name = "Enchant Cloak - Whisper of Silken Leech",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 45,
				Name = "Enchant Cloak - Chant of Leeching Fangs",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 46,
				Name = "Enchant Weapon - Stonebound Artistry",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 47,
				Name = "Enchant Weapon - Authority of Fiery Resolve",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213494/quick-onyx",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 48,
				Name = "Enchant Bracer - Chant of Armored Avoidance",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=223713/enchant-bracer-chant-of-armored-avoidance",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 49,
				Name = "Enchant Bracer - Chant of Armored Speed",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=223725/enchant-bracer-chant-of-armored-speed",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 50,
				Name = "Enchant Bracer - Chant of Armored Avoidance",
				Icon = "inv_misc_enchantedscroll.jpg",
				SecondIcon = "professions-chaticon-quality-tier3.png",
				ItemLevel = 610,
				Quality = "Rare",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=223713/enchant-bracer-chant-of-armored-avoidance",
				Description = "+147 Mastery and +49 Haste"
			},
			new Item
			{
				Id = 51,
				Name = "Aureate Sentry’s Pledge",
				Icon = "inv_plate_raidpaladingoblin_d_01_helm.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=229244/aureate-sentrys-pledge?bonus=7981:12042:5878:11996",
				Description = "Dropped by One-Armed Bandit in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 52,
				Name = "Strapped Rescue-Keg",
				Icon = "Strapped Rescue-Keg.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=221060/strapped-rescue-keg?bonus=657:12042:5878:7981:11996",
				Description = "+5449 Haste (6.23% @ L80), +2335 Mastery (3.30% @ L80), +2240 Stamina.\nMythic Upgrade Level: 6/8."
			},
			new Item
			{
				Id = 53,
				Name = "Aureate Sentry’s Roaring Will",
				Icon = "inv_plate_raidpaladingoblin_d_01_shoulder.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=229242/aureate-sentrys-roaring-will?bonus=7981:12042:5878:11996",
				Description = "Dropped by Rik Reverb in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 54,
				Name = "Test Pilot’s Go-Pack",
				Icon = "Test Pilot.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=228844/test-pilots-go-pack?bonus=7981:12042:5878:11996",
				Description = "Dropped by Sprocketmonger in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 55,
				Name = "Aureate Sentry’s Encasement",
				Icon = "inv_plate_raidpaladingoblin_d_01_chest.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=229247/aureate-sentrys-encasement?bonus=7981:12042:5878:11996",
				Description = "+5457 Strength (or Intellect), +9972 Stamina, +800 Haste (0.72% @ L80), +1180 Mastery (1.06% @ L80)\nDurability: 165/165\nPart of: Oath of the Aureate Sentry"
			},
			new Item
			{
				Id = 56,
				Name = "Revved-Up Vambraces",
				Icon = "Revved-Up Vambraces.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=228868/revved-up-vambraces?bonus=7981:12042:5878:11996",
				Description = "Dropped by Vexie in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 57,
				Name = "Jumpstarter’s Scaffold-Scrapers",
				Icon = "inv_plate_outdoorundermine_c_01_glove.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=234504/jumpstarters-scaffold-scrapers?bonus=657:12042:5878:7981:11996",
				Description = "Dropped in Operation: Floodgate dungeon."
			},
			new Item
			{
				Id = 58,
				Name = "Venture Contractor’s Floodlight",
				Icon = "Venture Contractors Floodlight.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=234505/venture-contractors-floodlight?bonus=657:12042:5878:7981:11996",
				Description = "Dropped in Operation: Floodgate dungeon."
			},
			new Item
			{
				Id = 59,
				Name = "Aureate Sentry’s Legguards",
				Icon = "inv_plate_raidpaladingoblin_d_01_pant.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=229243/aureate-sentrys-legguards?bonus=7981:12042:5878:11996",
				Description = "+5457 Strength (or Intellect), +9972 Stamina, +1748 Haste (1.60% @ L80), +733 Mastery (0.62% @ L80)\nDurability: 120/120.\nPart of: Oath of the Aureate Sentry (5/5)"
			},
			new Item
			{
				Id = 60,
				Name = "Rik's Walkin' Boots",
				Icon = "RiksWalkinBoots.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=228874/riks-walkin-boots?bonus=7981:12042:5878:11996",
				Description = "+4405 Strength (or Intellect), +9972 Stamina, +1180 Haste (1.07% @ L80), +1180 Mastery (1.06% @ L80).\nDropped by: Rik Reverb (Drop Chance: 9.84%)"
			},
			new Item
			{
				Id = 61,
				Name = "The Jastor Diamond",
				Icon = "inv_jewelry_ring_63.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=231265/the-jastor-diamond?bonus=7981:12042:5878:11996",
				Description = "+22487 Stamina, +1179 Haste (1.07% @ L80), +1606 Mastery (1.44% @ L80)\nEquip: Take Credit for your allies’ deeds up to once every 12 sec, gaining 83 of their highest secondary stat up to 10 times until shortly after leaving combat. 10% chance to transfer all accumulated credit to your ally instead.\nDropped by: Chrome King Gallywix (18.52%)"
			},
			new Item
			{
				Id = 62,
				Name = "Miniature Roulette Wheel",
				Icon = "MiniatureRouletteWheel.png",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=228843/miniature-roulette-wheel?bonus=7981:12042:5878:11996",
				Description = "Dropped by One-Armed Bandit in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 63,
				Name = "Eye of Kezan",
				Icon = "EyeOfKezan.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230198/eye-of-kezan?bonus=7981:12042:5878:11996",
				Description = "Dropped by Chrome King Gallywix in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 64,
				Name = "Mister Pick-Me-Up",
				Icon = "Misterpick-me-up.png",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230186/mister-pick-me-up?bonus=7981:12042:5878:11996",
				Description = "Dropped by Sprocketmonger in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 65,
				Name = "Big Earner’s Bludgeon",
				Icon = "BigEarnersBludgeon.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=228901/big-earners-bludgeon?bonus=7981:12042:5878:11996",
				Description = "Dropped by Mug’zee, Heads of Security in Liberation of Undermine raid."
			},
			new Item
				{
					Id = 66,
					Name = "Titan of Industry",
					Icon = "TitanOfIndustry.jpg",
					ItemLevel = 678,
					Quality = "Epic",
					RequiredLevel = 80,
					Url = "https://www.wowhead.com/item=228889/titan-of-industry?bonus=7981:12042:5878:11996",
					Description = "37103 Armor, +2739 Strength, +8790 Intellect, +19198 Stamina\n+937 Critical Strike (0.81% @ L80), +833 Haste (0.72% @ L80)\nEquip: Your attacks have a very high chance to contaminate your target with industrial runoff (up to 25 times), dealing 2240 Shadow damage per stack every 3 sec until their demise.\nDropped by: Chrome King Gallywix (25.03%)"
				},
			new Item
			{
				Id = 67,
				Name = "Cloak of Questionable Intent",
				Icon = "inv_bracer_plate_earthendungeon_c_01.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=159287/cloak-of-questionable-intent?bonus=657:12042:5878:7981:11996",
				Description = "Dropped from The MOTHERLODE!! dungeon."
			},
			new Item
			{
				Id = 68,
				Name = "Slashproof Business Plate",
				Icon = "inv_chest_plate_earthendungeon_c_01.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=221069/slashproof-business-plate?bonus=657:12042:5878:7981:11996",
				Description = "Dropped from Cinderbrew Meadery dungeon."
			},
			new Item
			{
				Id = 69,
				Name = "Fuzzy Cindercuffs",
				Icon = "inv_bracer_plate_earthendungeon_c_01.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=221064/fuzzy-cindercuffs?bonus=657:12042:5878:7981:11996",
				Description = "Dropped from Cinderbrew Meadery dungeon."
			},
			new Item
			{
				Id = 70,
				Name = "Aureate Sentry's Gauntlets",
				Icon = "inv_plate_raidpaladingoblin_d_01_glove.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=229245/aureate-sentrys-gauntlets?bonus=7981:12042:5878:11996",
				Description = "+4203 Strength (or Intellect), +9892 Stamina, +1166 Versatility (1.09% @ L80), +1166 Haste (1.07% @ L80)\nDurability: 55/55\nPart of: Oath of the Aureate Sentry"
			},
			new Item
			{
				Id = 71,
				Name = "Sabatons of Rampaging Elements",
				Icon = "inv_boot_plate_zandalardungeon_c_01.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=159679/sabatons-of-rampaging-elements?bonus=657:12042:5878:7981:11996",
				Description = "Dropped from The MOTHERLODE!! dungeon."
			},
			new Item
			{
				Id = 72,
				Name = "Wick's Golden Loop",
				Icon = "inv_11_0_earthen_earthenring_color2.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=221099/wicks-golden-loop?bonus=657:12042:5878:7981:11996",
				Description = "Dropped from Darkflame Cleft dungeon."
			},
			new Item
			{
				Id = 73,
				Name = "Bloodoath Signet",
				Icon = "inv_ring_maldraxxus_01_red.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=178871/bloodoath-signet?bonus=657:12042:5878:7981:11996",
				Description = "Dropped from Theater of Pain dungeon."
			},
			new Item
			{
				Id = 74,
				Name = "Signet of the Priory",
				Icon = "Signetofthepriory.png",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=219308/signet-of-the-priory?bonus=657:12042:5878:7981:11996",
				Description = "Dropped from Priory of the Sacred Flame dungeon."
			},
			new Item
			{
				Id = 75,
				Name = "Carved Blazikon Wax",
				Icon = "inv_misc_candlekobold_color1.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=219305/carved-blazikon-wax?bonus=657:12042:5878:7981:11996",
				Description = "Dropped from Darkflame Cleft dungeon."
			},
			new Item
			{
				Id = 76,
				Name = "Electrifying Cognitive Amplifier",
				Icon = "inv_mace_1h_enprofession_c_01.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=168955/electrifying-cognitive-amplifier?bonus=657:12042:5878:7981:11996",
				Description = "Dropped from Operation: Mechagon dungeon."
			},
			new Item
			{
				Id = 77,
				Name = "Galebreaker Bulwark",
				Icon = "inv_shield_1h_earthendungeon_c_02.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=221045/galebreaker-bulwark?bonus=657:12042:5878:7981:11996",
				Description = "Dropped from The Rookery dungeon."
			},
			new Item
			{
				Id = 78,
				Name = "Gobfather's Gifted Bling",
				Icon = "inv_11_0_ventureco_necklace01_color2.jpg", 
			    ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=228842/gobfathers-gifted-bling?bonus=7981:12042:5878:11996",
				Description = "Dropped by Mug’zee, Heads of Security in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 79,
				Name = "Dumpmech Compactors",
				Icon = "inv_glove_plate_raidwarriorgoblin_d_01.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=228849/dumpmech-compactors?bonus=7981:12042:5878:11996",
				Description = "Dropped by Stix Bunkjunker in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 80,
				Name = "Coin-Operated Girdle",
				Icon = "inv_plate_raidpaladingoblin_d_01_belt.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=228886/coin-operated-girdle?bonus=7981:12042:5878:11996",
				Description = "Dropped by One-Armed Bandit in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 81,
				Name = "Test Pilot’s Go-Pack",
				Icon = "Test Pilot.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=228844/test-pilots-go-pack?bonus=7981:12042:5878:11996",
				Description = "Dropped by Sprocketmonger in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 82,
				Name = "Miniature Roulette Wheel",
				Icon = "MiniatureRouletteWheel.png",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=228843/miniature-roulette-wheel?bonus=7981:12042:5878:11996",
				Description = "Dropped by One-Armed Bandit in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 83,
				Name = "The Jastor Diamond",
				Icon = "TheJastorDiamond.png",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=231265/the-jastor-diamond?bonus=7981:12042:5878:11996",
				Description = "Dropped by Chrome King Gallywix in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 84,
				Name = "Mug's Moxie Jug",
				Icon = "Mug'sMoxieJug.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230185/mugs-moxie-jug",
				Description = "A-tier trinket, possibly dropped from Liberation of Undermine raid (boss info not confirmed)."
			},
			new Item
			{
				Id = 85,
				Name = "Reverb Radio",
				Icon = "Reverbradio.png",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230199/reverb-radio",
				Description = "A-tier trinket, likely dropped in Liberation of Undermine raid."
			},
			new Item
			{
				Id = 86,
				Name = "Algari Alchemist Stone",
				Icon = "AlgarialchemistStone.png",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213700/algari-alchemist-stone",
				Description = "Crafted trinket using Alchemy; provides utility and healing bonuses."
			},
			new Item
			{
				Id = 87,
				Name = "Soulletting Ruby",
				Icon = "SoullettingRuby.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=178809/soulletting-ruby",
				Description = "Dungeon drop from Theater of Pain, older expansion trinket reused in M+ rotation."
			},
			new Item
			{
				Id = 88,
				Name = "Unstable Power Suit Core",
				Icon = "Unstable Power Suit Core.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230195/unstable-power-suit-core",
				Description = "Likely dropped in Operation: Mechagon or similar dungeon content."
			},
			new Item
			{
				Id = 89,
				Name = "Funhouse Lens",
				Icon = "Funhouse Lens.png",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230194/funhouse-lens",
				Description = "B-tier trinket, likely dropped in Liberation of Undermine or Mythic+ dungeons."
			},
			new Item
			{
				Id = 90,
				Name = "House of Cards",
				Icon = "HouseOfCards.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230192/house-of-cards",
				Description = "Unique deck-themed trinket with reactive effects."
			},
			new Item
			{
				Id = 91,
				Name = "Entropic Skardyn Core",
				Icon = "Entropic Skardyn Core.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230197/entropic-skardyn-core",
				Description = "Trinket with chaotic stat procs or damage."
			},
			new Item
			{
				Id = 92,
				Name = "Sigil of Algari Concordance",
				Icon = "Sigil of Algari Concordance.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=213701/sigil-of-algari-concordance",
				Description = "Likely crafted or dropped, offers minor utility or healing support."
			},
			new Item
			{
				Id = 93,
				Name = "Darkfuse Medichopper",
				Icon = "Darkfuse Medichopper.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230191/darkfuse-medichopper",
				Description = "Trinket with health restoration effects."
			},
			new Item
			{
				Id = 94,
				Name = "Burin of the Candle King",
				Icon = "Burin of the Candle King.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230196/burin-of-the-candle-king",
				Description = "Candle-themed trinket with a small utility or niche effect."
			},
			new Item
			{
				Id = 95,
				Name = "Synergistic Brewtializer",
				Icon = "Synergistic Brewterializer.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230190/synergistic-brewtializer",
				Description = "Beer-themed trinket with synergistic healing or stat-sharing effects."
			},
			new Item
			{
				Id = 96,
				Name = "Vial of Spectral Essence",
				Icon = "Vial of Spectral Essence.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230193/vial-of-spectral-essence",
				Description = "Trinket offering versatility boosts or spectral stat triggers."
			},
			new Item
			{
				Id = 97,
				Name = "Remnant of Darkness",
				Icon = "Remnant of Darkness.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230200/remnant-of-darkness",
				Description = "Dark magic-themed trinket with passive or reactive shadow effects."
			},
			new Item
			{
				Id = 98,
				Name = "Ingenious Mana Battery",
				Icon = "Ingenious Mana Battery.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230201/ingenious-mana-battery",
				Description = "Restores mana over time or on use; excellent for healers."
			},
			new Item
			{
				Id = 99,
				Name = "Gallagio Bottle Service",
				Icon = "GallagioBottleService.jpg",
				ItemLevel = 676,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=230202/gallagio-bottle-service",
				Description = "Trinket with party-wide benefits; festive or chaotic theme."
			},
			new Item
			{
				Id = 100,
				Name = "Consecrated Cloak",
				Icon = "inv_cloth_outdoorarathor_d_01_cape.jpg",
				ItemLevel = 675,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=222817/consecrated-cloak?bonus=12040:1515:10520",
				Description = "Equip: Your damaging spells and abilities have a chance to deal 17860 damage to your target. The magic school chosen is based upon your selection of socketed Khaz Algar gems."
			},
			new Item
			{
				Id = 101,
				Name = "Aureate Sentry's Roaring Will",
				Icon = "inv_plate_raidpaladingoblin_d_01_shoulder.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=229242/aureate-sentrys-roaring-will?bonus=7981:12042:5878:11996",
				Description = "Set: Aureate Sentry\n\n(2) Set: Protection: Each time you take damage you have a chance to activate Luck of the Draw, causing your next source of incoming healing to restore 15% of your max health. Holy: Your healing spells have a chance to apply Insurance! The effect heals an ally whose health drops below 40%.\n\n(4) Set: Protection: Your spells and abilities have a chance to grant Winning Streak, increasing the chance to trigger Luck of the Draw by 15% and Divine Steed movement speed by 100% for 8 sec. Spending Holy Power while this is active has a 12% chance to fully refund it. Holy: Holy Power abilities restore 1% of your maximum mana when cast with Insurance active."
			},
			new Item
			{
				Id = 102,
				Name = "Waxsteel Greathelm",
				Icon = "inv_helm_plate_earthendungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=221100/waxsteel-greathelm?bonus=657:12042:5878:7981:11996",
				Description = "+5457 Strength (or Intellect), +9576 Stamina, +1935 Versatility (1.80% @ L80), +1445 Haste (1.29% @ L80).\nDropped by: Blazikon (Drop Chance: 7.78%)"
			},
			new Item
			{
				Id = 103,
				Name = "Durable Information Securing Container",
				Icon = "inv_armor_waistoftime_d_01_belt_titan-copy.jpg",
				ItemLevel = 684,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=245966/durable-information-securing-container",
				Description = "+4328 Strength (or Intellect), +32281 Stamina\nPrismatic Socket\nEquip: Your spells and abilities have a chance to turn you into a Lightning Rod striking a random enemy every 2 sec for 294 Nature damage over 15 sec.\nUse: Open the device."
			},
			new Item
			{
				Id = 104,
				Name = "Footbomb Championship Ring",
				Icon = "inv_ring_80_06e.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=159462/footbomb-championship-ring?bonus=657:12042:5878:7981:11996",
				Description = "+22487 Stamina, +3741 Haste (5.73% @ L80), +4004 Mastery (5.72% @ L80)"
			},
			new Item
			{
				Id = 105,
				Name = "Everforged Vambraces",
				Icon = "inv_plate_outdoorarathor_d_01_bracer.jpg",
				ItemLevel = 675,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=222345/everforged-vambraces?bonus=12040:1515:10520:8793",
				Description = "+2299 Strength (or Intellect), +21668 Stamina, +662 Haste (0.59% @ L80), +662 Mastery (0.56% @ L80)\nEquip: Your damaging spells and abilities have a chance to deal 17860 damage to your target. The magic school is based on your socketed Khaz Algar gems."
			},
			new Item
			{
				Id = 106,
				Name = "Rail Rider's Bisector",
				Icon = "inv_axe_1h_earthendungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=221096/rail-riders-bisector?bonus=657:12042:5878:7981:11996",
				Description = "One-Hand Axe\n5241 - 10886 Damage (Speed 2.60 / 3101 dps)\n+2798 Strength, +19988 Stamina\n+993 Critical Strike (0.70% @ L80), +697 Haste (0.56% @ L80)"
			},
			new Item
			{
				Id = 107,
				Name = "Barricade of the Endless Empire",
				Icon = "inv_shield_1h_oribosdungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=178867/barricade-of-the-endless-empire?bonus=657:12042:5878:7981:11996",
				Description = "+2739 Strength, +8790 Intellect, +19198 Stamina, +942 Critical Strike (0.63%), +748 Haste (0.51%)"
			},
			new Item
			{
				Id = 108,
				Name = "Fullthrottle Facerig",
				Icon = "inv_plate_raidpaladingoblin_d_01_helm.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=228855/fullthrottle-facerig?bonus=7981:12042:5878:11996",
				Description = "9497 Armor, +3457 Strength (or Intellect), +9597 Stamina, +1269 Critical Strike (2.43% @ L80), +628 Haste (0.75% @ L80)"
			},
			new Item
			{
				Id = 109,
				Name = "Semi-Charmed Amulet",
				Icon = "inv_111_necklace_gallywix.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=228841/semi-charmed-amulet?bonus=7981:12042:5878:11996",
				Description = "+22487 Stamina, +1779 Critical Strike (2.54% @ L80), +6005 Haste (1.01% @ L80)\nDropped by: Rik Reverb (31%)"
			},
			new Item
			{
				Id = 110,
				Name = "",
				Icon = ".jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "",
				Description = ""
			},
			new Item
			{
				Id = 111,
				Name = "",
				Icon = ".jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "",
				Description = ""
			},
			new Item
			{
				Id = 112,
				Name = "Coin-Operated Girdle",
				Icon = "inv_plate_raidpaladingoblin_d_01_belt.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=228886/coin-operated-girdle?bonus=7981:12042:5878:11996",
				Description = "+7123 Armor, +4092 Strength/Intellect, +29982 Stamina, +1241 Haste (1.88% @ L80), +574 Mastery (0.83% @ L80)"
			},
			new Item
			{
				Id = 113,
				Name = "Faded Championship Ring",
				Icon = "inv_111_ring_bilgewater.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=228840/faded-championship-ring?bonus=7981:12042:5878:11996",
				Description = "+22487 Stamina, +1906 Crit (2.45% @ L80), +1866 Haste (2.83% @ L80)\nDropped by: Fazende (25.39%)"
			},
			new Item
			{
				Id = 114,
				Name = "Remixed Ignition Saber",
				Icon = "inv_sword_1h_goblinraid_d_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=228895/remixed-ignition-saber?bonus=7981:12042:5878:11996",
				Description = "One-Hand Sword\n6047 - 10890 Damage (2.60 speed / 3101 DPS)\n+2729 Strength, +19988 Stamina, +929 Crit (1.18% @ L80), +361 Haste (0.59% @ L80)"
			},
			new Item
			{
				Id = 115,
				Name = "Electrician's Siphoning Filter",
				Icon = "inv_mail_outdoorundermine_c_01_cape.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=234057/electricians-siphoning-filter?bonus=7981:12042:5878:11996",
				Description = "+2286 Armor, +3070 Agi/Str/Int, +22487 Stamina, +854 Haste (1.29% @ L80), +845 Mastery (1.20% @ L80)"
			},
			new Item
			{
				Id = 116,
				Name = "Stonefury Vambraces",
				Icon = "inv_bracer_plate_zandalardungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=158359/stonefury-vambraces?bonus=7981:12042:5878:11996",
				Description = "+6331 Armor, +3070 Strength/Intellect, +22487 Stamina, +784 Haste (1.19% @ L80), +555 Mastery (0.79% @ L80)"
			},
			new Item
			{
				Id = 117,
				Name = "Lightning-Conductor's Bands",
				Icon = "inv_belt_plate_earthendungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=221004/lightning-conductors-bands?bonus=7981:12042:5878:11996",
				Description = "+7123 Armor, +4092 Strength/Intellect, +29982 Stamina, +1101 Haste (1.66% @ L80), +1048 Mastery (1.55% @ L80)"
			},
			new Item
			{
				Id = 118,
				Name = "Bloodoath Signet",
				Icon = "inv_ring_maldraxxus_01_red.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=178871/bloodoath-signet?bonus=657:12042:5878:7981:11996",
				Description = "+22487 Stamina, +3255 Critical Strike (6.61%), +2656 Haste (6.08%)"
			},
			new Item
			{
				Id = 119,
				Name = "Signet of the Priory",
				Icon = "inv_arathordungeon_signet_color1.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=219308/signet-of-the-priory?bonus=657:12042:5878:7981:11996",
				Description = "+5188 Agility/Strength/Intellect. Use: Increases your highest secondary stat by 7755 for 20 sec. Allies gain 204 of the same stat for 20 sec. (2 min CD)"
			},
			new Item
			{
				Id = 120,
				Name = "Tome of Light's Devotion",
				Icon = "inv_7xp_inscription_talenttome01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=219309/tome-of-lights-devotion?bonus=657:12042:5878:7981:11996",
				Description = "Equip: Increases armor by 136, attacks may absorb 3781 Magic Damage. Use: Gain increased Radiance. (1.5 min CD, tanks only)"
			},
			new Item
			{
				Id = 121,
				Name = "House of Cards",
				Icon = "inv_111_gallyjack_gallywix.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=230027/house-of-cards?bonus=7981:12042:5878:11996",
				Description = "+5188 Agility/Strength/Intellect. Use: Grants 8376.3–10237.7 Mastery for 15 sec and stacks the deck. Increases minimum Mastery by 310.23 up to 3 times."
			},
			new Item
			{
				Id = 122,
				Name = "",
				Icon = ".jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=230198/eye-of-kezan?bonus=7981:12042:5878:11996",
				Description = "+1700 Mastery (2.43%). Equip: Chance to gain 450 Primary Stat up to 20 times, then explode for 124126 Fire damage or heal for 186333."
			},
			new Item
			{
				Id = 123,
				Name = "Mister Lock-N-Stalk",
				Icon = "inv_111_healraydrone_gallywix.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=230193/mister-lock-n-stalk?bonus=7981:12042:5878:11996",
				Description = "+1700 Haste (2.58%). Equip: Chance to call Precision Blasting for 4777 Physical damage. (20s CD)"
			},
			new Item
			{
				Id = 124,
				Name = "Improvised Seaforium Pacemaker",
				Icon = "ability_blackhand_attachedslagbombs.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=232541/improvised-seaforium-pacemaker?bonus=657:12042:5878:7981:11996",
				Description = "Use: Grants Explosive Adrenaline, boosting your first ability every 1s for 15s. Crits extend by 1s, up to 30s. (1 min CD)"
			},
			new Item
			{
				Id = 125,
				Name = "Torq's Big Red Button",
				Icon = "inv_111_redbutton_bilgewater.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=230109/torqs-big-red-button?bonus=7981:12042:5878:11996",
				Description = "+1700 Mastery (2.43%). Use: Grants 11205 Strength for 15s. Next 3 abilities cause 62768 Nature damage. Doubles per cast. (2 min CD)"
			},
			new Item
			{
				Id = 126,
				Name = "Reverb Radio",
				Icon = "inv_111_statsoundwaveemitter_blackwater.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=230194/reverb-radio?bonus=7981:12042:5878:11996",
				Description = "+5188 Agility/Strength/Intellect. Equip: Gain 398 Haste up to 5 times. At max, double the bonus for 15s. (then reset)"
			},
			new Item
			{
				Id = 127,
				Name = "Zee's Thug Hotline",
				Icon = "inv_111_remotecontrol_gallywix.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=230199/zees-thug-hotline?bonus=7981:12042:5878:11996",
				Description = "+5188 Agility/Strength. Equip: Chance to summon a Goon Squad member to attack your target for 10s, gaining Bloodlust or similar. Whole crew may appear."
			},
			new Item
			{
				Id = 128,
				Name = "Chromebustible Bomb Suit",
				Icon = "inv_111_bombsuit_gallywix.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=230029/chromebustible-bomb-suit?bonus=7981:12042:5878:11996",
				Description = "+1700 Haste (2.58%). Use: Absorbs 109797141 damage or lasts 20s, then explodes for 1019886 Fire split between nearby enemies. (1.5 min CD)"
			},
			new Item
			{
				Id = 129,
				Name = "Vexie's Pit Whistle",
				Icon = "inv_111_sapper_bilgewater.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=230019/vexies-pit-whistle?bonus=7981:12042:5878:11996",
				Description = "+5188 [Agility or Strength]\nUse: Summon Ribbot Geardo to assist you for 5 sec, coating enemies with rancid motor oil. Geardo departs explosively to deal 1,348,592 Fire damage split between nearby enemies, increased by 15% if recently oiled. (1 min 30 sec cooldown)\nRequires completion of Vexie encounter in Liberation of Undermine on Mythic difficulty."
			},
			new Item
			{
				Id = 130,
				Name = "Scrapfield 9001",
				Icon = "inv_111_forcefieldmodule_steamwheedle.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=230026/scrapfield-9001?bonus=7981:12042:5878:11996",
				Description = "+5188 [Agility or Strength]\nEquip: Falling below 60% health surrounds you with a protective vortex of junk, reducing damage taken by 30% for 15 sec or until 1,918,844 damage is prevented. (30 sec ICD)\nPassive: After 20 sec of inactivity outside combat, the Scrapfield overloads to energize you with 3340 Haste for 12 sec."
			},
			new Item
			{
				Id = 131,
				Name = "Ravenous Honey Buzzer",
				Icon = "inv_10_engineering_device_gadget1_color1.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=219298/ravenous-honey-buzzer?bonus=657:12042:5878:11996",
				Description = "+5188 [Agility or Strength]\nUse: Call in a ravenous ally and ride off into the sunset (or 3438373 total damage split between enemies you ride through, whichever is reached first). (1 min 30 sec cooldown)"
			},
			new Item
			{
				Id = 132,
				Name = "Cinderbrew Stein",
				Icon = "inv_misc_lh_earthenpitcher_b_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=219297/cinderbrew-stein?bonus=657:12042:5878:11996",
				Description = "+5188 [Tank Specializations Only]\nEquip: Occasionally share a drink with allies for 15 sec, granting 6610 Primary Stat and absorbing 307,480 damage. You also take a sip for 3599 Primary Stat and 1,578,383 absorption.\nPassive: Below 50% health, take an emergency sip. (1 min ICD)"
			},
			new Item
			{
				Id = 133,
				Name = "Geargrinder's Spare Keys",
				Icon = "inv_111_goblintinketkeychain_gallywix.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=230197/geargrinders-spare-keys?bonus=7981:12042:5878:11996",
				Description = "+5188 [Agility or Strength or Intellect]\nUse: Launch a Geargrinder trike to the first enemy in its path, exploding for 3,050,169 Fire damage split between all nearby enemies. (2 min cooldown)"
			},
			new Item
			{
				Id = 134,
				Name = "Suspicious Energy Drink",
				Icon = "inv_drink_31_embalmingfluid_color04.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=235365/suspicious-energy-drink?bonus=657:12042:5865:11990",
				Description = "+4,596 [Agility or Strength or Intellect]\nEquip: When you use harmful spells or abilities, there is a chance you also take a sip of the energy drink, increasing your Mastery by 4693 for 10 sec. Gain an additional 689 Mastery if you are below 35% health.\n\"A teeth-rattling concoction made with Mama's secret recipe to numb your sense of self-preservation.\""
			},
			new Item
			{
				Id = 135,
				Name = "Ratfang Toxin",
				Icon = "spell_nature_nullifypoison.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=235359/ratfang-toxin?bonus=657:12042:5865:11990",
				Description = "+4,596 [Agility or Strength or Intellect]\nEquip: Your harmful spells and abilities apply Ratfang Toxin to your target, stacking up to 5 times.\nUse: Release all toxin stacks to deal 1,136,150 Nature damage, plus 129,845 per stack over 5 sec. (1 min, 30 sec cooldown)"
			},
			new Item
			{
				Id = 136,
				Name = "Remnant of Darkness",
				Icon = "ability_demonhunter_darkness.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=219307/remnant-of-darkness?bonus=657:12042:5878:7981:11996",
				Description = "Equip: Your abilities have a chance to call the Darkness to you, increasing your Primary Stat by 2659, up to 13,295.\nUpon reaching full power, the Darkness is unleashed, inflicting 2,891,265 Shadow damage split between nearby enemies over 15 sec before fading."
			},
			new Item
			{
				Id = 137,
				Name = "Amorphous Relic",
				Icon = "inv_misc_trinket6oog_gronn2.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=232891/amorphous-relic?bonus=657:12042:5865:11990",
				Description = "Equip: Upon entering combat, and every 60 sec, gain either Massive or Miniature for 30 sec:\n🧱 *Miniature*: Reduces your size by 20%, and increases Speed by 1298 and Haste by 8627.\n💪 *Massive*: Increases size by 20%, maximum health by 350111, and Primary Stat by 8749."
			},
			new Item
			{
				Id = 138,
				Name = "Funhouse Lens",
				Icon = "inv_jewelcrafting_icediamond_01.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=232417/funhouse-lens?bonus=657:12042:5865:11990",
				Description = "+4,596 [Agility or Strength or Intellect]\nUse: Look through the lens and randomly scale up or down, gaining 10,017 Critical Strike or Haste for 15 sec. (1 min, 30 sec cooldown)\n\"Eat today, lens tomorrow.\""
			},
			new Item
			{
				Id = 139,
				Name = "Siphoning Lightbrand",
				Icon = "70_inscription_vantus_rune_light.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=225653/siphoning-lightbrand?bonus=657:12042:5865:11990",
				Description = "+4,596 [Agility or Strength]\nEquip: Taking damage has a high chance to brand your assailant, dealing 127,695 Holy Fire damage and healing you for 50% of the damage dealt."
			},
			new Item
			{
				Id = 140,
				Name = "Mechano-Core Amplifier",
				Icon = "inv_misc_weathermachine_01.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=232485/mechano-core-amplifier?bonus=657:12042:5865:11990",
				Description = "+4,596 [Agility or Strength]\nEquip: Your harmful abilities have a chance to power the amplifier, randomly increasing either your highest secondary stat by 4212 or your lowest secondary stat by 5315 for 10 sec.\n\"Intended for machine use only. Use at your own risk.\""
			},
			new Item
			{
				Id = 141,
				Name = "Algari Alchemist Stone",
				Icon = "inv_10_alchemy_alchemystone_color4.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=210816/algari-alchemist-stone?bonus=7981:12042:5878:11996",
				Description = "+1700 Versatility (2.18% @ L80)\nEquip: When you heal or deal damage, you have a chance to increase your Strength, Agility, or Intellect by 20,754 for 15 sec. Your highest stat is always chosen.\nEquip: Increases the effect that healing and mana potions have on the wearer by 40%. This effect does not stack.\n\"Can be used for transmutations in place of a Philosopher's Stone.\""
			},
			new Item
			{
				Id = 142,
				Name = "K.U.J.O.'s Flame Vents",
				Icon = "achievement_cooking_masterofthegrill.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=232546/k-u-j-o-s-flame-vents?bonus=657:12042:5878:7981:11996",
				Description = "+5,188 [Agility or Strength]\nUse: Channel to vent flames for 2 sec, dealing 3,929,944 Fire damage split between all nearby enemies. (2 min cooldown)\nMechanical enemies become Superheated, taking 166,131 additional fire damage when struck by your next harmful ability."
			},
			new Item
			{
				Id = 143,
				Name = "Papa's Prized Putter",
				Icon = "inv_gizmo_rocketlauncher.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=234821/papas-prized-putter?bonus=657:12042:5865:11990",
				Description = "+4,596 [Agility or Strength]\nEquip: Your melee attacks have a chance to putt a bomb toward your enemy, dealing 206,722 Fire damage on impact.\nPassive: When you exit combat, gain a burst of speed for 10 sec if your score is a Hole-in-One, Birdie, or Par."
			},
			new Item
			{
				Id = 144,
				Name = "Razdunk's Big Red Button",
				Icon = "inv_misc_enggizmos_27.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=159611/razdunks-big-red-button?bonus=657:12042:5878:7981:11996",
				Description = "+5,188 Strength\nUse: Push the Big Red Button, launching a missile barrage which deals 2,491,876 Fire damage split among all enemies in the area. Deals increased damage when striking multiple targets. (2 min cooldown)"
			},
			new Item
			{
				Id = 145,
				Name = "Viscera of Coalesced Hatred",
				Icon = "inv_misc_organ_04.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=178800/viscera-of-coalesced-hatred?bonus=657:12042:5878:7981:11996",
				Description = "+5,188 Strength\nEquip: Your abilities have a very high chance to lash out with a Hateful Strike, inflicting 328,754 Physical damage to an enemy and healing you for 12,714. These effects are increased by 100% while you are below 35% health."
			},
			new Item
			{
				Id = 146,
				Name = "Grim Codex",
				Icon = "inv_misc_book_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=178811/grim-codex?bonus=657:12042:5878:7981:11996",
				Description = "+5,188 [Agility or Strength]\nUse: Conjure a Spectral Scythe, dealing 1,794,165 Shadow damage to your target and 2,491,819 Shadow damage in a 15 yd cone to enemies behind your target. (1 min 30 sec cooldown)"
			},
			new Item
			{
				Id = 147,
				Name = "Sigil of Algari Concordance",
				Icon = "inv_misc_dungeonsignetearthen02_color1.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=219295/sigil-of-algari-concordance?bonus=657:12042:5878:7981:11996",
				Description = "+5,188 [Agility or Strength or Intellect]\nEquip: Your abilities have a chance to call an earthen ally to your aid, supporting you in combat. (15 sec cooldown)"
			},
			new Item
			{
				Id = 148,
				Name = "Turbo-Drain 5000",
				Icon = "spell_winston_zap.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=232883/turbo-drain-5000?bonus=657:12042:5865:11990",
				Description = "+4,596 [Agility or Strength or Intellect]\nEquip: Your harmful spells and abilities have a chance to drain your target’s electrical power, dealing 373,411 Nature damage over 5 sec. If the target dies while being drained, you gain 25% movement speed for 5 sec.\n\"The 8th Rule of Commerce: Fast goblins are smart goblins!\""
			},
			new Item
			{
				Id = 149,
				Name = "Ominous Oil Residue",
				Icon = "inv_misc_food_legion_goooil_pool.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=235467/ominous-oil-residue?bonus=657:12042:5865:11990",
				Description = "+4,596 [Agility or Strength]\nEquip: Taking damage has a chance to erupt the ground, covering you in a layer of dark oil that absorbs 559,292 damage for 15 sec. When the shield is broken, it splatters nearby enemies for 133,018 Shadow damage."
			},
			new Item
			{
				Id = 150,
				Name = "Modular Platinum Plating",
				Icon = "inv_shield_68.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=168965/modular-platinum-plating?bonus=657:12042:5878:7981:11996",
				Description = "+5,188 [Agility or Strength]\nUse: Gain 4 stacks of Platinum Plating for 30 sec, increasing your Armor by 11,257. Receiving more than 10% of your health from Physical damage will remove one stack. (2 min cooldown)"
			},
			new Item
			{
				Id = 151,
				Name = "Core Recycling Unit",
				Icon = "inv_qiraj_jewelencased.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=234326/core-recycling-unit?bonus=657:12042:5865:11990",
				Description = "+4,596 [Agility or Strength]\nEquip: Add an energy core to the container for every enemy slain, up to a maximum of 20.\nUse: Consume all stored energy cores to heal for 2,117,293 instantly and 90,741 over 10 sec for each core. (1 min 30 sec cooldown)"
			},
			new Item
			{
				Id = 152,
				Name = "Ringing Ritual Mud",
				Icon = "inv_misc_food_legion_goomolasses_pool.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=232543/ringing-ritual-mud?bonus=657:12042:5878:7981:11996",
				Description = "+1,700 Versatility (2.18% @ L80)\nUse: Become Mudborne, absorbing 1,084,618 damage and pulsing 534,340 Nature damage split between nearby enemies over 10 sec. (2 min cooldown)\nPeriodic damage taken reduces this cooldown by 22 sec."
			},
			new Item
			{
				Id = 153,
				Name = "Garbagemancer's Last Resort",
				Icon = "creatureportrait_infernal_ball_02.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=235984/garbagemancers-last-resort?bonus=657:12042:5865:11990",
				Description = "+4,596 [Agility or Strength or Intellect]\nUse: Fashion nearby garbage into a sphere and launch it into the sky. After 3 sec, it crashes into the targeted location dealing 2,518,413 Physical damage split among all enemies. (2 min cooldown)"
			},
			new Item
			{
				Id = 154,
				Name = "Noggenfogger Ultimate Deluxe",
				Icon = "inv_potion_83.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=232486/noggenfogger-ultimate-deluxe?bonus=657:12042:5865:11990",
				Description = "+1,615 Critical Strike (2.31% @ L80)\nUse: Summons a Blackwater Pirate to aid you in combat for 20 sec. (1 min cooldown)\n\"With an elixir as unpredictable as Noggenfogger's, you never know if to expect pirates or confusion.\""
			},
			new Item
			{
				Id = 155,
				Name = "Spelunker's Waning Candle",
				Icon = "inv_trinket_maldraxxus_01_purple.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=225638/spelunkers-waning-candle?bonus=657:12042:5865:11990",
				Description = "+4,596 [Agility or Strength]\nEquip: Your damaging spells and abilities have a chance to place a Spelunker’s Candle on your head, granting 3,653 Critical Strike for 15 sec.\nMoving within 3 yards of an ally places the candle on both heads, sharing the effect for the remaining duration."
			},
			new Item
			{
				Id = 156,
				Name = "Concoction: Kiss of Death",
				Icon = "inv_10_alchemy_engineersgrease_color1.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=215174/concoction-kiss-of-death?bonus=657:12042:5865:11990",
				Description = "+4,596 [Agility or Strength or Intellect]\nUse: Drink from the vial and increase all secondary stats by 1,795 for 30 sec.\nJump to administer antidote to end effect.\nIf not administered, be stunned for 5 sec. (2 min 30 sec cooldown)"
			},
			new Item
			{
				Id = 157,
				Name = "Shadow-Binding Ritual Knife",
				Icon = "inv_knife_1h_grimbatolraid_d_03.jpg",
				ItemLevel = 665,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=215178/shadow-binding-ritual-knife?bonus=657:12042:5865:11990",
				Description = "+1,615 Mastery (2.31% @ L80)\nEquip: Engrave a ritual seal into your skin, granting 5,239 Primary Stat.\nSpells and abilities have a chance to reduce one of your secondary stats by 4,477 for 10 sec during combat."
			},
			new Item
			{
				Id = 158,
				Name = "Grim Codex",
				Icon = "inv_misc_book_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=178811/grim-codex?bonus=657:12042:5878:7981:11996",
				Description = "+5,188 [Agility or Strength]\nUse: Conjure a Spectral Scythe, dealing 1,794,165 Shadow damage to target and 2,491,896 Shadow damage to enemies behind (1 min 30 sec cooldown)"
			},
			new Item
			{
				Id = 159,
				Name = "Ravenous Honey Buzzer",
				Icon = "inv_10_engineering_device_gadget1_color1.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=219298/ravenous-honey-buzzer?bonus=657:12042:5878:7981:11996",
				Description = "+5,188 [Agility or Strength]\nUse: Call in a ravenous ally and ride off, inflicting 2,239,582 Fire damage split between enemies in your path. (1 min 30 sec cooldown)"
			},
			new Item
			{
				Id = 160,
				Name = "K.U.-J.O.'s Flame Vents",
				Icon = "achievement_cooking_masterofthegrill.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=232546/k-u-j-o-s-flame-vents?bonus=657:12042:5878:7981:11996",
				Description = "+5,188 [Agility or Strength]\nUse: Channel to vent flames for 2 sec, dealing 3,493,944 Fire damage to nearby enemies.\nMechanical enemies take +1,631 Fire damage when struck after. (2 min cooldown)"
			},
			new Item
			{
				Id = 161,
				Name = "Cutthroat Competition Stompers",
				Icon = "inv_boot_plate_raidwarriorgoblin_d_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=228887/cutthroat-competition-stompers?bonus=7981:12042:5878:11996",
				Description = "Plate Feet\n+4,093 [Strength or Intellect]\n+29,982 Stamina\n+1,256 Critical Strike (1.79% @ L80)\n+529 Mastery (0.76% @ L80)"
			},
			new Item
			{
				Id = 162,
				Name = "Ring of Perpetual Conflict",
				Icon = "inv_ring_maldraxxus_01_violet.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=178872/ring-of-perpetual-conflict?bonus=657:12042:5878:7981:11996",
				Description = "Finger\n+22,487 Stamina\n+5,116 Critical Strike (7.31% @ L80)\n+2,669 Mastery (3.81% @ L80)"
			},
			new Item
			{
				Id = 163,
				Name = "Best-in-Slots",
				Icon = "inv_mace_2h_goblinraid_d_02.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=232526/best-in-slots?bonus=7981:12042:5878:11996",
				Description = "Two-Hand Mace\n12,938 - 16,036 Damage (4,019 DPS) Speed 3.60\n+5,457 [Agility or Strength]\n+39,976 Stamina\nEquip: Spells and abilities may grant 420.7 to 5142.5 of random secondary stat for 15 sec.\nUse: Cheat to gain 5142.5 of highest secondary stat for 15 sec, reconfigures out of combat. (2 min cooldown)"
			},
			new Item
			{
				Id = 164,
				Name = "Gauntlets of Absolute Authority",
				Icon = "inv_glove_plate_kultirasdungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=168980/gauntlets-of-absolute-authority?bonus=657:12042:5878:7981:11996",
				Description = "Plate Hands\n+4,093 [Strength or Intellect]\n+29,982 Stamina\n+911 Critical Strike (1.30% @ L80)\n+875 Mastery (1.25% @ L80)"
			},
			new Item
			{
				Id = 165,
				Name = "Everforged Vambraces",
				Icon = "inv_plate_outdoorarathor_d_01_bracer.jpg",
				ItemLevel = 675,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=222435/everforged-vambraces?bonus=12040:1515:11303:8960:8791",
				Description = "Plate Wrist\n+2,895 [Strength or Intellect]\n+21,686 Stamina\n+662 Crit (0.95% @ L80)\n+662 Mastery (0.95% @ L80)\nEquip: Your gear warms with Woven Dawnthread, gaining 504 Critical Strike when over 80% health."
			},
			new Item
			{
				Id = 166,
				Name = "Faded Championship Ring",
				Icon = "inv_111_ring_bilgewater.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=228840/faded-championship-ring?bonus=7981:12042:5878:11996",
				Description = "Finger\n+22,487 Stamina\n+5,916 Critical Strike (8.45% @ L80)\n+1,866 Haste (2.83% @ L80)"
			},
			new Item
			{
				Id = 167,
				Name = "Test Subject's Clasps",
				Icon = "inv_plate_raidpaladingoblin_d_01_bracer.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=228884/test-subjects-clasps?bonus=7981:12042:5878:11996",
				Description = "Plate Wrist\n+3,070 [Strength or Intellect]\n+22,487 Stamina\n+922 Critical Strike (1.32% @ L80)\n+417 Mastery (0.60% @ L80)"
			},
			new Item
			{
				Id = 168,
				Name = "Semi-Charmed Amulet",
				Icon = "inv_111_necklace_gallywix.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=228841/semi-charmed-amulet?bonus=7981:12042:5878:11996",
				Description = "Neck\n+22,487 Stamina\n+1,779 Critical Strike (2.54% @ L80)\n+6,005 Haste (9.10% @ L80)"
			},
			new Item
			{
				Id = 169,
				Name = "Fullthrottle Facerig",
				Icon = "inv_plate_raidpaladingoblin_d_01_helm.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=228858/fullthrottle-facerig?bonus=7981:12042:5878:11996",
				Description = "Plate Head\n+5,457 [Strength or Intellect]\n+39,976 Stamina\n+1,699 Critical Strike (2.43% @ L80)\n+682 Haste (1.03% @ L80)"
			},
			new Item
			{
				Id = 170,
				Name = "Gauntlets of Absolute Authority",
				Icon = "inv_glove_plate_kultirasdungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=168980/gauntlets-of-absolute-authority?bonus=657:12042:5878:7981:11996",
				Description = "Plate Hands\n+4,093 [Strength or Intellect]\n+29,982 Stamina\n+911 Critical Strike (1.30% @ L80)\n+875 Mastery (1.25% @ L80)"
			},
			new Item
			{
				Id = 171,
				Name = "Hops-Laden Greatboots",
				Icon = "inv_boot_plate_earthendungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=221061/hops-laden-greatboots?bonus=657:12042:5878:7981:11996",
				Description = "Plate Feet\n+4,093 [Strength or Intellect]\n+29,982 Stamina\n+701 Critical Strike (1.00% @ L80)\n+1,084 Mastery (1.55% @ L80)"
			},
			new Item
			{
				Id = 172,
				Name = "Automatic Waist Tightener",
				Icon = "inv_belt_plate_kultirasdungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=168976/automatic-waist-tightener?bonus=657:12042:5878:7981:11996",
				Description = "Plate Waist\n+4,093 [Strength or Intellect]\n+29,982 Stamina\n+821 Critical Strike (1.17% @ L80)\n+964 Mastery (1.38% @ L80)"
			},
			new Item
			{
				Id = 173,
				Name = "Stonefury Vambraces",
				Icon = "inv_bracer_plate_zandalandungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=158359/stonefury-vambraces?bonus=657:12042:5878:7981:11996",
				Description = "Plate Wrist\n+3,070 [Strength or Intellect]\n+22,487 Stamina\n+784 Haste (1.19% @ L80)\n+555 Mastery (0.79% @ L80)"
			},
			new Item
			{
				Id = 174,
				Name = "Chef Chewie's Towel",
				Icon = "inv_cape_cloth_earthendungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=221054/chef-chewies-towel?bonus=657:12042:5878:7981:11996",
				Description = "Back\n+3,070 [Agility or Strength or Intellect]\n+22,487 Stamina\n+870 Critical Strike (1.24% @ L80)\n+469 Mastery (0.67% @ L80)"
			},
			new Item
			{
				Id = 175,
				Name = "Coin-Operated Girdle",
				Icon = "inv_plate_raidpaladingoblin_d_01_belt.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=228886/coin-operated-girdle?bonus=7981:12042:5878:11996",
				Description = "Plate Waist\n+4,093 [Strength or Intellect]\n+29,982 Stamina\n+1,221 Haste (1.85% @ L80)\n+564 Mastery (0.81% @ L80)"
			},
			new Item
			{
				Id = 176,
				Name = "Hoop of the Blighted",
				Icon = "inv_11_0_earthen_earthenring_color5.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=221197/hoop-of-the-blighted?bonus=657:12042:5878:7981:11996",
				Description = "Finger\n+22,487 Stamina\n+4,671 Critical Strike (6.67% @ L80)\n+1,314 Mastery (4.45% @ L80)"
			},
			new Item
			{
				Id = 177,
				Name = "Ring of Perpetual Conflict",
				Icon = "inv_ring_maldraxxus_01_violet.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=178872/ring-of-perpetual-conflict?bonus=657:12042:5878:7981:11996",
				Description = "Finger\n+22,487 Stamina\n+5,116 Critical Strike (7.31% @ L80)\n+2,669 Mastery (3.81% @ L80)"
			},
			new Item
			{
				Id = 178,
				Name = "Durable Information Securing Container",
				Icon = "inv_armor_waistoftime_d_01_belt_titan-copy.jpg",
				ItemLevel = 684,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=245966/durable-information-securing-container",
				Description = "Plate Waist\n+7,425 Armor\n+4,328 [Agility or Strength or Intellect]\n+32,281 Stamina\nPrismatic Socket\nEquip: Your spells and abilities have a chance to turn you into a Lightning Rod, striking a random enemy within 40 yds for 234 Nature damage every 3 sec for 15 sec.\nUse: Open the device."
			},
			new Item
			{
				Id = 179,
				Name = "Dessia's Decimating Decapitator",
				Icon = "inv_axe_2h_oribosdungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Mythic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=178866/dessias-decimating-decapitator?bonus=657:12042:5878:7981:11996",
				Description = "Two-Hand Axe\n9,611 - 19,963 Damage (4,100 damage per second)\n+5,457 Strength\n+39,976 Stamina\n+1,496 Critical Strike (2.14% @ L80)\n+884 Mastery (1.26% @ L80)"
			},
			new Item
			{
				Id = 180,
				Name = "Critical Chain",
				Icon = "ability_thunderking_lightningwhip.jpg",
				ItemLevel = 0,
				Quality = "Spell Effect",
				RequiredLevel = 0,
				Url = "https://www.wowhead.com/spell=1236272/critical-chain",
				Description = "Approximately 1.5 procs per minute\nYour spells and abilities have a chance to trigger Critical Overload, increasing your Critical Strike by 3% every 2 sec for 20 sec. (20s cooldown)"
			},
			new Item
			{
				Id = 181,
				Name = "Windsinger's Runed Citrine",
				Icon = "inv_siren_isle_searuned_citrine_pink",
				ItemLevel = 619,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=228640/windsingers-runed-citrine",
				Description = "Grants 925.007 of your highest secondary stat."
			},
			new Item
			{
				Id = 182,
				Name = "Fathomdweller's Runed Citrine",
				Icon = "inv_siren_isle_searuned_citrine_blue",
				ItemLevel = 619,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=228639/fathomdwellers-runed-citrine",
				Description = "Grants 39740.006 / 1510.133 Mastery.\n\nIn addition, all other Singing Citrine effects are increased based on your total Mastery."
			},
			new Item
			{
				Id = 183,
				Name = "Stormbringer's Runed Citrine",
				Icon = "inv_siren_isle_searuned_citrine_red",
				ItemLevel = 619,
				Quality = "Epic",
				RequiredLevel = 70,
				Url = "https://www.wowhead.com/item=228638/stormbringers-runed-citrine",
				Description = "Grants 12231.252 / 471.917 of every secondary stat."
			},          
			new Item
			{
				Id = 184,
				Name = "Sabatons of Rampaging Elements",
				Icon = "inv_boot_plate_zandalardungeon_c_01.jpg",
				ItemLevel = 678,
				Quality = "Epic",
				RequiredLevel = 80,
				Url = "https://www.wowhead.com/item=159679/sabatons-of-rampaging-elements?bonus=657:12042:5878:7981:11996",
				Description = "+4093 Strength, +29892 Stamina, +1199 Critical Strike (1.71%), +587 Haste (0.84%)"
			},
			};

			await db.Items.AddRangeAsync(items);
			await db.SaveChangesAsync();
		}
	}
}
