using Microsoft.AspNetCore.Identity;

namespace PaladinHubV2.Server.Data.Entities
{
	public class User : IdentityUser
	{
		public string FullName { get; set; } = string.Empty;
		public string? AvatarPath { get; set; }
		public string? StripeCustomerId { get; set; }

		public Cart? Cart { get; set; }
		public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
		public ICollection<PaymentMethod> PaymentMethods { get; set; } = new List<PaymentMethod>();
		public ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
		public ICollection<PromoRedemption> PromoRedemptions { get; set; } = new List<PromoRedemption>();
	}
}
