namespace PaladinHubV2.Server.Data.Entities
{
	public enum TransactionStatus
	{
		Pending = 0,
		Complete = 1,
		Failed = 2,
		Refunded = 3
	}

	public enum TransactionType
	{
		Unknown = 0,
		WalletTopUp = 1,
		WalletCharge = 2,
		Purchase = 3,
		Refund = 4
	}

	public class Transaction
	{
		public Guid Id { get; set; }
		public string UserId { get; set; } = string.Empty;
		public User? User { get; set; }
		public DateTime CreatedAtUtc { get; set; }
		public string PurchaseTitle { get; set; } = string.Empty;
		public decimal Amount { get; set; }
		public string Currency { get; set; } = "USD";
		public TransactionStatus Status { get; set; }
		public string Region { get; set; } = "US";
		public string? ExternalId { get; set; }
		public TransactionType Type { get; set; } = TransactionType.Unknown;
	}
}
