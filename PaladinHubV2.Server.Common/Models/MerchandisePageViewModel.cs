using System.Collections.Generic;

namespace PaladinHub.Models.Products
{
	public class MerchandisePageViewModel
	{
		public ProductQueryOptions Query { get; set; } = new();
		public List<string> AllCategories { get; set; } = new();
		public PagedResult<ProductListItem> Products { get; set; } = new();

		public Dictionary<int, int> RatingAtLeast { get; set; } =
			new() { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 } };
	}
}
