namespace PaladinHubV2.Server.Data.Entities
{
	public enum PromoCodeType
	{
		Balance = 1,          // добавя баланс
		DiscountPercent = 2   // отстъпка в %
	}

	public class PromoCode
	{
		public string Id { get; set; } = Guid.NewGuid().ToString("N");
		public string Code { get; set; } = "";            // UPPERCASE, уникален
		public PromoCodeType Type { get; set; }
		public decimal Value { get; set; }                 // сума (Balance) или % (Discount)
		public string? Currency { get; set; }              // за Balance (например "EUR"), optional
		public int? MaxUses { get; set; }                  // максимум общ брой ползвания (null = без лимит)
		public int UsedCount { get; set; }                 // колко пъти е ползван общо
		public DateTime? ExpiresAtUtc { get; set; }        // валидност
		public bool IsActive { get; set; } = true;
		public string? Notes { get; set; }
		public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
	}
}
