using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities
{
	public class PaymentMethod
	{
		[Key]
		[MaxLength(64)]
		public string Id { get; set; } = Guid.NewGuid().ToString("N");

		[Required, MaxLength(64)]
		public string UserId { get; set; } = default!;

		public User? User { get; set; }

		[MaxLength(32)]
		public string Brand { get; set; } = "Card";

		[MaxLength(4)]
		public string Last4 { get; set; } = "0000";

		public bool IsDefault { get; set; }

		[MaxLength(64)]
		public string? Label { get; set; }

		[MaxLength(64)]
		public string? ExternalId { get; set; }

		[MaxLength(32)]
		public string? Provider { get; set; } = "Stripe";

		public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
	}
}
