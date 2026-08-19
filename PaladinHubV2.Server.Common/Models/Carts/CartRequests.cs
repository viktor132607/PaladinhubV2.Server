namespace PaladinHub.Models.Carts
{
	public sealed class AddCartItemRequest
	{
		public string ProductId { get; init; } = string.Empty;

		public int Quantity { get; init; } = 1;
	}
}
