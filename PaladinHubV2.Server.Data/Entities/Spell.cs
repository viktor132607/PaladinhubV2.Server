using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities
{
	public class Spell
	{
		[Key]
		public int Id { get; set; }

		[Required]
		[MaxLength(100)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(100)]
		public string? Icon { get; set; }

		[MaxLength(500)]
		public string? Description { get; set; }

		[MaxLength(300)]
		public string? Url { get; set; }

		private string _quality = "spell";

		[MaxLength(50)]
		public string Quality
		{
			get => _quality;
			set => _quality = string.IsNullOrWhiteSpace(value) ? "spell" : value.ToLowerInvariant();
		}

		//public int X { get; set; }
		//public int Y { get; set; }

		// basic item metadata
		//public int itemlevel { get; set; }
		//public int requiredlevel { get; set; }
		//public string? quality { get; set; }

		//// usability and effects
		//public string? useeffect { get; set; }
		//public string? duration { get; set; }
		//public bool persiststhroughdeath { get; set; }

		//// stack and economy
		//public int maxstack { get; set; }
		//public int sellpricegold { get; set; }
		//public int sellpricesilver { get; set; }
		//public int auctionhouseprice { get; set; }

		//// categorization
		//public string? category { get; set; }
		//public string? faction { get; set; }

		//// patch and appearance
		//public string? patchversion { get; set; }
		//public string? iconurl { get; set; }
		//public string? screenshoturl { get; set; }
		//public string? videoembedurl { get; set; }

		// optional display helpers
		//public string tooltip => $"{name} (ilvl {itemlevel}) - {useeffect}";
		//public string pricedisplay => $"{sellpricegold}g {sellpricesilver}s";
	}
}
