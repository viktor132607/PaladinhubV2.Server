using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities
{
	public class Cart
	{
		[Key]
		public Guid Id { get; init; } = Guid.NewGuid();

		public bool IsArchived { get; set; }

		public string? OrderDate { get; set; }

		[Required]
		public string UserId { get; set; } = default!;

		public User User { get; set; } = default!;

		public ICollection<CartProduct> CartProducts { get; set; } = new List<CartProduct>();

		public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;
	}
}
