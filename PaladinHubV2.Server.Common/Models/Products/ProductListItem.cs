namespace PaladinHub.Models.Products
{
	public class ProductListItem
	{
		public string Id { get; set; } = default!;
		public string Name { get; set; } = default!;
		public string Category { get; set; } = default!;
		public string? ImageUrl { get; set; }
		public string? Description { get; set; }
		public decimal Price { get; set; }

		public decimal AverageRating { get; set; }
		public int ReviewsCount { get; set; }
	}
}
