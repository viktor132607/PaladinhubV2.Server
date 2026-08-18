using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PaladinHub.Models.Products
{
	public class EditProductViewModel
	{
		[Required]
		public string Id { get; set; } = default!;

		[Required, MaxLength(100)]
		public string Name { get; set; } = default!;

		[Range(0, 1_000_000)]
		public decimal Price { get; set; }

		[MaxLength(50)]
		public string Category { get; set; } = "Other";

		[MaxLength(50)]
		public string? NewCategory { get; set; }

		[MaxLength(1000)]
		public string? Description { get; set; }

		public List<ProductImageInputModel> Images { get; set; } = new();

		public int? ThumbnailImageId { get; set; }

		public int? ThumbnailIndex { get; set; }


		public IEnumerable<SelectListItem>? CategorySelectList { get; set; }
	}
}
