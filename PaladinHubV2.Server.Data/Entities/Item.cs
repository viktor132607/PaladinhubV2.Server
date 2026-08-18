using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities
{
	public class Item
	{
		[Key]
		public int Id { get; set; }

		[Required]
		[MaxLength(100)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(100)]
		public string? Icon { get; set; }

		[MaxLength(100)]
		public string? SecondIcon { get; set; }

		[MaxLength(2000)]
		public string? Description { get; set; }

		[MaxLength(300)]
		public string? Url { get; set; }

		public int? ItemLevel { get; set; }

		public int? RequiredLevel { get; set; }

		[MaxLength(50)]
		public string? Quality { get; set; }

		//// Usability and effects
		//public string? UseEffect { get; set; }
		//public string? Duration { get; set; }
		//public bool PersistsThroughDeath { get; set; }

		//// Stack and economy
		//public int MaxStack { get; set; }
		//public int SellPriceGold { get; set; }
		//public int SellPriceSilver { get; set; }
		//public int AuctionHousePrice { get; set; }

		//// Categorization
		//public string? Category { get; set; }
		//public string? Faction { get; set; }

		//// Patch and appearance
		//public string? PatchVersion { get; set; }
		//public string? IconUrl { get; set; }
		//public string? ScreenshotUrl { get; set; }
		//public string? VideoEmbedUrl { get; set; }

		//// Optional display helpers
		//public string Tooltip => $"{Name} (iLvl {ItemLevel}) - {UseEffect}";
		//public string PriceDisplay => $"{SellPriceGold}g {SellPriceSilver}s";


	}
}
