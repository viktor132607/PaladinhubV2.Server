using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Data.Seed.Contracts;

namespace PaladinHubV2.Server.Data.Seed
{
	public class ProductsSeeder : ISeeder
	{
		private readonly IServiceProvider _sp;
		public ProductsSeeder(IServiceProvider sp) => _sp = sp;

		// DTO за seed
		private sealed record ProductSeed(
			string Name,
			decimal Price,
			string Category,
			string PrimaryImage,
			List<string> Images // допълнителни
		);

		public async Task SeedAsync()
		{
			using var scope = _sp.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

			if (await db.Products.AnyAsync()) return;

			// 1) Данни за продукти
			var productsData = new List<ProductSeed>
			{
				new("Warcraft Archive", 19.99m, "Books",
					"https://m.media-amazon.com/images/I/718GtfGk2yL._SL1500_.jpg",
					new()),

				new("Warcraft: Durotan", 19.99m, "Books",
					"https://m.media-amazon.com/images/I/81ZmJcJUuKL._SL1500_.jpg",
					new()),

				new("Warcraft: Lord of the Clans", 19.99m, "Books",
					"https://m.media-amazon.com/images/I/81-6nYRhoZL._SL1500_.jpg",
					new()),

				new("Warcraft: Day of the Dragon", 19.99m, "Books",
					"https://m.media-amazon.com/images/I/81tHEuDMFxL._SL1500_.jpg",
					new()),

				new("Warcraft: Night of the Dragon", 19.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/fcc8320929b59a2a84cb4338015164fb/world-of-warcraft--night-of-the-dragon-31.jpg",
					new()),

				new("Warcraft: Tides of Darkness", 19.99m, "Books",
					"https://m.media-amazon.com/images/I/71d76vhPILL._SL1500_.jpg",
					new()),

				new("Warcraft: The Sunwell Trilogy – Dragon Hunt (Vol. 1)", 24.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/a/b788bac4b9ea19fb3927117a93fae0fe/warcraft--the-sunwell-trilogy---dragon-hunt-30.jpg",
					new()),

				new("Warcraft: The Sunwell Trilogy – Shadows of Ice (Vol. 2)", 24.99m, "Books",
					"https://m.media-amazon.com/images/I/71MlPU6dsHL._SL1500_.jpg",
					new()),

				new("Warcraft: The Sunwell Trilogy – Ghostlands (Vol. 3)", 24.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/w/a/warcraft_the_sunwell_trilogy_ghostlands_vol_3_1670859497_0.jpg",
					new()),

				new("Warcraft: The Official Movie Novelization", 19.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/w/a/warcraft-the-official-movie-novelization.jpg",
					new()),

				new("Warcraft: Legends, Vol. 1", 22.90m, "Books",
					"https://m.media-amazon.com/images/I/81CEgx12p6L._SL1500_.jpg",
					new()),
				new("Warcraft: Legends, Vol. 2", 22.90m, "Books",
					"https://m.media-amazon.com/images/I/81dgYRiV1OL._SL1500_.jpg",
					new()),
				new("Warcraft: Legends, Vol. 3", 22.90m, "Books",
					"https://m.media-amazon.com/images/I/61jl-5XEHDL._SL1000_.jpg",
					new()),
				new("Warcraft: Legends, Vol. 4", 22.90m, "Books",
					"https://m.media-amazon.com/images/I/61KHHCZTXKL._SL1000_.jpg",
					new()),
				new("Warcraft: Legends, Vol. 5", 22.90m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/w/a/warcraft_legends_vol_5_1670860569_0.jpg",
					new()),

				new("World of Warcraft: Exploring Azeroth – The Eastern Kingdoms", 59.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/f458462f10b714ef15890eeb7c14792f/world-of-warcraft--exploring-azeroth---the-eastern-kingdoms-30.jpg",
					new()),
				new("World of Warcraft: Exploring Azeroth – Kalimdor", 59.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/w/o/world_of_warcraft_exploring_azeroth_kalimdor_1636632329_0.jpg",
					new()),
				new("World of Warcraft: Exploring Azeroth – Northrend", 59.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/w/o/world_of_warcraft_exploring_azeroth_northrend_1667388674_0.jpg",
					new()),
				new("World of Warcraft: Exploring Azeroth – Pandaria", 59.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/cb49e703df5ee4d80de5e1be83e9deca/world-of-warcraft--exploring-azeroth---pandaria-30.jpg",
					new()),
				new("World of Warcraft: Exploring Azeroth - Islands and Isles", 59.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/0b6beb968c8e79a6380c20ef42f7d37d/world-of-warcraft--exploring-azeroth---islands-and-isles-30.jpg",
					new()),
				new("World of Warcraft: Exploring Azeroth - (The Complete Collection)", 299.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/90a182ff376dc51ff2af1712d0d40d23/world-of-warcraft--exploring-azeroth-the-complete-collection-30.jpg",
					new()),

				new("World of Warcraft: Chronicle: Volume 1", 59.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/dc9712d86d5e8cea63a2b3d7c6734ece/world-of-warcraft-chronicle--volume-1-31.jpg",
					new()),
				new("World of Warcraft: Chronicle: Volume 2", 59.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/c5b13f83186d69cf82aaed2a48149b4a/world-of-warcraft-chronicle--volume-2-31.jpg",
					new()),
				new("World of Warcraft: Chronicle: Volume 3", 59.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/2d78405527d644b0332ab8b805ca10f4/world-of-warcraft-chronicle--volume-3-31.jpg",
					new()),
				new("World of Warcraft: Chronicle: Volume 4", 59.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/efab9e5f4362ad98f5bde4ab5cd4415d/world-of-warcraft-chronicle--volume-4-30.jpg",
					new()),

				new("World of Warcraft: The Voices Within (Short Story Collection)", 34.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/ead12927ce5e1ace189fc4b546a52b8b/world-of-warcraft--the-voices-within-short-story-collection-30.jpg",
					new()),

				new("World of Warcraft: Sylvanas (Paperback)", 34.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/w/o/world_of_warcraft_sylvanas_paperback_1678190113_0.jpg",
					new()),

				new("World of Warcraft: Arthas – Rise of the Lich King", 19.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/w/o/world-of-warcraft-arthas-rise-of-the-lich-king.jpg",
					new()),

				new("World of Warcraft: Illidan", 20.90m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/3ed8aaba2d36f6b790ad9d8d5a0fbe52/world-of-warcraft--illidan-31.jpg",
					new()),
				new("World of Warcraft: Wolfheart", 19.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/73788c8cc1168de4d6073a7a97f13050/world-of-warcraft--wolfheart-31.jpg",
					new()),
				new("World of Warcraft: Shadows Rising", 20.90m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/0e34f724809388ca147eb69a65392ebb/world-of-warcraft-shadowlands--shadows-rising-30.jpg",
					new()),
				new("World of Warcraft: The Dragonflight Codex", 64.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/w/o/world_of_warcraft_the_dragonflight_codex_1699350680_0.jpg",
					new()),
				new("World of Warcraft: War of the Scaleborn", 20.90m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/d696fcfc6591145a86f06c79bfe72cb3/world-of-warcraft--war-of-the-scaleborn-30.jpg",
					new()),
				new("World of Warcraft: Thrall. Twilight of the Aspects", 19.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/21083e03d64f0c2608d0d7d40589cd57/world-of-warcraft--thrall-twilight-of-the-aspects-31.jpg",
					new()),
				new("World of Warcraft: The Shattering (Prelude to Cataclysm)", 19.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/28eef9bd46231a877ab3009cb83825e1/world-of-warcraft--the-shattering-prelude-to-cataclysm-31.jpg",
					new()),
				new("World of Warcraft: Dawn of the Aspects", 20.90m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/36d44b1a7ca4df04bed1f92ca30ef3bd/world-of-warcraft--dawn-of-the-aspects-31.jpg",
					new()),
				new("World of Warcraft: Vol'jin: Shadows of the Horde", 19.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/33c349a5f2d0810ff4dbe5ae5d659e40/world-of-warcraft-vol-jin--shadows-of-the-horde-31.jpg",
					new()),
				new("World of Warcraft: Before the Storm", 20.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/6cc5074daee503355d5a10f48a065780/world-of-warcraft--before-the-storm-31.jpg",
					new()),
				new("World of Warcraft: Stormrage", 19.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/2fce200fe79cf4118c29f59083a52483/world-of-warcraft--stormrage-31.jpg",
					new()),
				new("World of Warcraft: War Crimes", 19.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/f3c1479e1474f3125a36829606e4728d/world-of-warcraft--war-crimes-31.jpg",
					new()),

				new("World of Warcraft: The Official Tarot Deck and Guidebook", 65.99m, "Miscellaneous",
					"https://cdn.ozone.bg/media/catalog/product/w/o/world_of_warcraft_the_official_tarot_deck_and_guidebook_78_cards_and_booklet_1744201049_0.jpg",
					new()
					{
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/3a543ee6eacfdea7f8c0955dc7fe35d2/world-of-warcraft--the-official-tarot-deck-and-guidebook-78-cards-and-booklet-34.jpg",
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/95aefb922efcedbf7634fab666c9b0c8/world-of-warcraft--the-official-tarot-deck-and-guidebook-78-cards-and-booklet-33.jpg",
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/d9b1e68b94ccde906dd74cf5575bed37/world-of-warcraft--the-official-tarot-deck-and-guidebook-78-cards-and-booklet-32.jpg",
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/cb2d2d5241199e3e7546612b4dca38fd/world-of-warcraft--the-official-tarot-deck-and-guidebook-78-cards-and-booklet-31.jpg",
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/w/o/152fb30bd9ed6e11ad114d39cd5369cd/world-of-warcraft--the-official-tarot-deck-and-guidebook-78-cards-and-booklet-30.jpg"
					}),

				new("World of Warcraft: Hoodie – Alliance", 59.99m, "Miscellaneous",
					"https://i.ebayimg.com/images/g/ZBIAAOSw6dFniiYl/s-l1600.webp",
					new() 
					{ 
						"https://i.ebayimg.com/images/g/FpEAAOSwiOxniiYi/s-l1600.webp" 
					}),

				new("World of Warcraft: Hoodie – Horde", 59.99m, "Miscellaneous",
					"https://i.ebayimg.com/images/g/gocAAOSwv6lnk7v6/s-l1600.webp",
					new() 
					{ 
						"https://i.ebayimg.com/images/g/z2MAAOSw5DFnk7v6/s-l1600.webp" 
					}),

				new("World of Warcraft: Alexstrasza – Premium Statue 52 cm", 599.99m, "Statues",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/3c7eac90bc60a3369fe121740a9a88bd/statuetka-blizzard-games--world-of-warcraft---alexstrasza-30.jpg",
					new()
					{
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/88f26bba331331c69119dbdccc61dc03/statuetka-blizzard-games--world-of-warcraft---alexstrasza-33.jpg",
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/4c76e3b412afe87e947f03298e21be05/statuetka-blizzard-games--world-of-warcraft---alexstrasza-32.jpg",
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/bef8e85722900721a8515dd65aa84f62/statuetka-blizzard-games--world-of-warcraft---alexstrasza-31.jpg"
					}),

				new("World of Warcraft: Illidan Stormrage – Premium Statue 60 cm", 699.99m, "Statues",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/6bdb0aeebd1996d2f1eb785612cbe019/statuetka-blizzard-games--world-of-warcraft---illidan--60-cm-33.jpg",
					new()
					{
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/2e3c24f4df5504ac7a66483b42db1cf5/statuetka-blizzard-games--world-of-warcraft---illidan--60-cm-32.jpg",
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/aeff3f4747688111d64223defbe8a195/statuetka-blizzard-games--world-of-warcraft---illidan--60-cm-31.jpg",
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/6436cdfa7fb1a67ef07dcb0428b9308e/statuetka-blizzard-games--world-of-warcraft---illidan--60-cm-30.jpg"
					}),

				new("World of Warcraft: Jaina – Premium Statue 46 cm", 499.99m, "Statues",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/cc227c48cae0d85daf510545be1ab01b/statuetka-blizzard-games--world-of-warcraft---jaina--46-cm-34.jpg",
					new()
					{
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/a1e91e62a6a318970705f55030da313b/statuetka-blizzard-games--world-of-warcraft---jaina--46-cm-35.jpg",
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/fe3828ec570f8294e85a8d2d6b0326a6/statuetka-blizzard-games--world-of-warcraft---jaina--46-cm-33.jpg",
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/c59f310902ff0570338fcf32f2849c61/statuetka-blizzard-games--world-of-warcraft---jaina--46-cm-31.jpg",
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/s/t/8db5edcbc7d8c0f16d7d3758617bcba3/statuetka-blizzard-games--world-of-warcraft---jaina--46-cm-30.jpg"
					}),

				new("World of Warcraft: Pandaren (Monk/Rogue), 15 cm", 59.99m, "Statues",
					"https://cdn.ozone.bg/media/catalog/product/s/t/statuetka_mcfarlane_games_world_of_warcraft_pandaren_monk_rogue_15_cm_1743583317_0.jpg",
					new()),

				new("World of Warcraft: Dwarf Hunter (Beast Master/Marksman), 15 cm", 59.99m, "Statues",
					"https://cdn.ozone.bg/media/catalog/product/s/t/statuetka_mcfarlane_games_world_of_warcraft_dwarf_hunter_beast_master_marksman_15_cm_1743583319_0.jpg",
					new()),

				new("World of Warcraft: Mug 500ml - Horde", 14.99m, "Cups",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/h/a/5cd6b34a24c9bdfc8bb2917cd6a36f30/halba-abystyle-games--world-of-warcraft---horde-31.jpg",
					new()),

				new("World of Warcraft: Mug 500ml - Alliance", 14.99m, "Cups",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/h/a/b1ef9bb9a7f853fd542e62974d9845c7/halba-abystyle-games--world-of-warcraft---alliance-30.jpg",
					new()),

				new("Funko POP! Games: Thrall", 34.99m, "Mini Figures Funko POP!",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/f/i/21620af0f948d9d93c4cce5630c33308/figura-funko-pop!-games--world-of-warcraft---thrall--1046-30.jpg",
					new()
					{
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/f/i/91764acf7e697110e6f2d0d055a00a9c/figura-funko-pop!-games--world-of-warcraft---thrall--1046-31.jpg"
					}),

				new("Funko POP! Games: Sylvanas Windrunner", 34.99m, "Mini Figures Funko POP!",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/f/i/ecd34ac77a611bc54746def7e1c79381/figura-funko-pop!-games--world-of-warcraft---sylvanas--990-30.jpg",
					new()
					{
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/f/i/bde59247dd7410539371e90bdee3b2c6/figura-funko-pop!-games--world-of-warcraft---sylvanas--990-32.jpg"
					}),

				new("Funko POP! Games: Alleria Windrunner", 34.99m, "Mini Figures Funko POP!",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/f/i/ee3ad517b3a5aba8df013a6e41751ca8/figura-funko-pop!-games--world-of-warcraft---alleria-windrunner--1045-30.jpg",
					new()
					{
						"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/f/i/d12f4a21285bce326044faa1d180021b/figura-funko-pop!-games--world-of-warcraft---alleria-windrunner--1045-31.jpg"
					}),

				new("The World of Warcraft: Comic Collection, Vol. 1", 59.99m, "Books",
					"https://cdn.ozone.bg/media/catalog/product/cache/1/image/a4e40ebdc3e371adff845072e1c73f37/t/h/93eff9b2eda8db576034dd570968a000/the-world-of-warcraft--comic-collection--vol-1-30.jpg",
					new())
			};

			// 2) Products (без ImageUrl)
			var products = productsData.Select(p => new Product(p.Name, p.Price)
			{
				Category = p.Category,
				// Description отсъства в seed DTO – оставяме null
			}).ToList();

			await db.Products.AddRangeAsync(products);
			await db.SaveChangesAsync();

			// 3) Галерия (включително PrimaryImage като SortOrder = 0)
			var imagesToAdd = new List<ProductImage>();

			foreach (var prod in products)
			{
				var seed = productsData.First(s => s.Name == prod.Name);

				// PrimaryImage – първи
				imagesToAdd.Add(new ProductImage
				{
					ProductId = prod.Id,
					Url = seed.PrimaryImage,
					SortOrder = 0
				});

				// Останалите, без дубликати спрямо primary (case-insensitive)
				var others = seed.Images
					.Where(u => !string.Equals(u, seed.PrimaryImage, StringComparison.OrdinalIgnoreCase))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.Select((u, idx) => new ProductImage
					{
						ProductId = prod.Id,
						Url = u,
						SortOrder = idx + 1
					});

				imagesToAdd.AddRange(others);
			}

			if (imagesToAdd.Count > 0)
			{
				await db.ProductImages.AddRangeAsync(imagesToAdd);
				await db.SaveChangesAsync();
			}

			// 4) Задаваме ThumbnailImageId по PrimaryImage (fallback: първата по SortOrder)
			foreach (var prod in products)
			{
				var seed = productsData.First(s => s.Name == prod.Name);

				var thumb = await db.ProductImages
					.Where(i => i.ProductId == prod.Id && i.Url == seed.PrimaryImage)
					.OrderBy(i => i.Id)
					.FirstOrDefaultAsync();

				if (thumb == null)
				{
					thumb = await db.ProductImages
						.Where(i => i.ProductId == prod.Id)
						.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
						.FirstOrDefaultAsync();
				}

				prod.ThumbnailImageId = thumb?.Id;
			}

			await db.SaveChangesAsync();
		}
	}
}
