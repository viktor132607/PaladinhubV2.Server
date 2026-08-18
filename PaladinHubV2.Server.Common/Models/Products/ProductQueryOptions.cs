using System.ComponentModel.DataAnnotations;

namespace PaladinHub.Models.Products
{
	public enum ProductSortBy
	{
		Relevance = 0,
		Name = 1,
		Price = 2,
		Newest = 3,
		Rating = 4,
		MostReviewed = 5
	}

	public sealed class ProductQueryOptions
	{
		public string? Search { get; set; }

		public List<string> Categories { get; set; } = new();

		[Range(0, double.MaxValue)]
		public decimal? MinPrice { get; set; }

		[Range(0, double.MaxValue)]
		public decimal? MaxPrice { get; set; }
		public List<string> PriceRanges { get; set; } = new();

		[Range(1, 5)]
		public int? MinRating { get; set; }

		public ProductSortBy SortBy { get; set; } = ProductSortBy.Relevance;
		public bool Desc { get; set; }

		[Range(1, int.MaxValue)]
		public int Page { get; set; } = 1;

		[Range(1, 200)]
		public int PageSize { get; set; } = 20;
	}
}
