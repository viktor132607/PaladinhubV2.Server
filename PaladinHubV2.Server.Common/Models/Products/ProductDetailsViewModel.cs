using System.ComponentModel.DataAnnotations;

namespace PaladinHub.Models.Products;
public class ProductDetailsViewModel
{
	public string Id { get; init; } = default!;
	public string Name { get; init; } = default!;
	public decimal Price { get; init; }
	public string? ImageUrl { get; init; }
	public string Category { get; init; } = "Other";
	public string? Description { get; init; }

	public double AverageRating { get; init; }
	public int ReviewsCount { get; init; }
	public List<ReviewVm> Reviews { get; init; } = new();

	public List<SimilarVm> Similar { get; init; } = new();
	public List<ImageItem> Images { get; init; } = new();
	public record ImageItem
	{
		public int? Id { get; init; }
		public string Url { get; init; } = default!;
	}

	public bool CanReview { get; init; }
	public bool AlreadyReviewed { get; init; }
}

public class ReviewVm
{
	public int Id { get; init; }
	public string UserName { get; init; } = default!;
	public int Rating { get; init; }
	public string? Content { get; init; }
	public DateTime CreatedAt { get; init; }
	public bool CanDelete { get; init; }
}

public class SimilarVm
{
	public string Id { get; init; } = default!;
	public string Name { get; init; } = default!;
	public decimal Price { get; init; }
	public string? ImageUrl { get; init; }
}

public class AddReviewInput
{
	[Required] public string ProductId { get; set; } = default!;
	[Range(1, 5)] public int Rating { get; set; }
	[MaxLength(2000)] public string? Content { get; set; }  
}
