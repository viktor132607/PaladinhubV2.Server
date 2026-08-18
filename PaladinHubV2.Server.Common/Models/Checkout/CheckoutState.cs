namespace PaladinHub.Models.Checkout
{
	public class CheckoutState
	{
		public ShippingInfoVM? Shipping { get; set; }
		public PaymentMethod? PaymentMethod { get; set; }
		public decimal Total { get; set; }
		public string? OrderId { get; set; }
	}
}
