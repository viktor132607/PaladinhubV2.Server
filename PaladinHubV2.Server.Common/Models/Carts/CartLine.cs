namespace PaladinHub.Models.Carts
{
	public sealed class CartLine
	{
		public Guid ProductId { get; set; } 
		public int Quantity { get; set; }
	}
}
