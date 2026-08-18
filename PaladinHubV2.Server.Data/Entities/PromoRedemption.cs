namespace PaladinHubV2.Server.Data.Entities
{
	public class PromoRedemption
	{
		public string Id { get; set; } = Guid.NewGuid().ToString("N");

		public string PromoCodeId { get; set; } = "";
		public PromoCode? PromoCode { get; set; }

		public string UserId { get; set; } = "";
		public User? User { get; set; }

		public DateTime RedeemedAtUtc { get; set; } = DateTime.UtcNow;

		public decimal? AmountCredited { get; set; }
		public string? Currency { get; set; }
	}
}
