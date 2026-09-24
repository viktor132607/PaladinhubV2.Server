namespace PaladinHub.Models.Checkout
{
	public class CheckoutState
	{
		public ShippingInfoVM? Shipping { get; set; }
		public PaymentMethod? PaymentMethod { get; set; }
		public decimal Total { get; set; }
		public string? OrderId { get; set; }
		public string Currency { get; set; } = "EUR";
		public decimal UsdPerEur { get; set; }
	}
}
