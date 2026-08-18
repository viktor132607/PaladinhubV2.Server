
namespace PaladinHub.Models.Products
{
	public class ProductReviewViewModel
	{
		public string Id { get; set; } = default!;
		public string Author { get; set; } = default!;
		public string Email { get; set; } = default!;
		public string Text { get; set; } = default!;
		public int Stars { get; set; } // 1..5
		public DateTime CreatedAt { get; set; }
	}
}
