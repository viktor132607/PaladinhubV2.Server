using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities
{
	public class ProductImage
	{
		public int Id { get; set; }

		// FK към Product (Product.Id е string GUID)
		[Required]
		public string ProductId { get; set; } = default!;

		/// <summary>Навигация назад към продукта.</summary>
		public Product Product { get; set; } = default!;

		// Пълен URL към изображението
		[Required, Url, MaxLength(2048)]
		public string Url { get; set; } = default!;

		// Подредба на изображението в галерията (0,1,2...)
		public int SortOrder { get; set; }

		// Алтернативен текст (по избор)
		[MaxLength(300)]
		public string? AltText { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
