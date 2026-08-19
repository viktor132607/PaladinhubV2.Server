namespace PaladinHub.Models.Checkout
{
	public sealed class CardFinalizeRequest
	{
		public string PaymentIntentId { get; init; } = string.Empty;
	}
}
