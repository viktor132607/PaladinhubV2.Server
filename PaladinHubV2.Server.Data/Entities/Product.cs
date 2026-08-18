using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaladinHubV2.Server.Data.Entities
{
	public class Product
	{
		public Product() { }

		public Product(string name, decimal price)
		{
			Id = Guid.NewGuid().ToString();
			Name = name;
			Price = price;
		}

		/// <summary>
		/// Конструктор за бързо създаване с начална галерия и избрано thumbnail изображение.
		/// </summary>
		public Product(
			string name,
			decimal price,
			IEnumerable<ProductImage>? images,
			int? thumbnailImageId = null)
		{
			Id = Guid.NewGuid().ToString();
			Name = name;
			Price = price;

			if (images != null)
				Images = new List<ProductImage>(images);

			ThumbnailImageId = thumbnailImageId;
		}

		/// <summary>Стрингов Id (GUID).</summary>
		[Required]
		public string Id { get; init; } = Guid.NewGuid().ToString();

		[Required, MaxLength(100)]
		public string Name { get; set; } = default!;

		[Required]
		[Column(TypeName = "decimal(18,2)")]
		public decimal Price { get; set; }

		/// <summary>FK към главното изображение.</summary>
		public int? ThumbnailImageId { get; set; }

		/// <summary>Навигация към главното изображение.</summary>
		public ProductImage? ThumbnailImage { get; set; }

		/// <summary>Пълна галерия от изображения за продукта.</summary>
		public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

		/// <summary>Колекция с ревюта.</summary>
		public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();

		public ICollection<CartProduct> Carts { get; set; } = new List<CartProduct>();

		[MaxLength(50)]
		public string Category { get; set; } = "Other";

		[MaxLength(1000)]
		public string? Description { get; set; }
	}
}
