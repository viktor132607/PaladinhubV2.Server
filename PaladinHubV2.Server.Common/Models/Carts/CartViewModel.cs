

using PaladinHubV2.Server.Data.Entities;

namespace PaladinHub.Models.Carts
{
	public class CartViewModel
	{
		public Guid Id { get; set; }            
		public string? OrderDate { get; set; }
		public string UserId { get; set; } = default!;
		public User? User { get; set; }
		public ICollection<CartProduct> CartProducts { get; set; } = new List<CartProduct>();
		public ICollection<Product> Products { get; set; } = new List<Product>();
	}
}
