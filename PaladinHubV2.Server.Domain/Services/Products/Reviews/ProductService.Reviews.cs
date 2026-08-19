using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public partial class ProductService
	{
		public async Task<bool> AddReviewAsync(AddReviewInput input, string userId, CancellationToken ct)
		{
			var hasInCart = await context.CartProducts
				.AnyAsync(cp => cp.ProductId == input.ProductId && cp.Cart.UserId == userId, ct);
			if (!hasInCart) return false;

			var exists = await context.ProductReviews
				.AnyAsync(r => r.ProductId == input.ProductId && r.UserId == userId, ct);
			if (exists) return false;

			var entity = new ProductReview
			{
				ProductId = input.ProductId,
				UserId = userId,
				Rating = input.Rating,
				Content = string.IsNullOrWhiteSpace(input.Content) ? null : input.Content.Trim()
			};

			context.ProductReviews.Add(entity);
			await context.SaveChangesAsync(ct);
			return true;
		}

		public async Task<bool> DeleteReviewAsync(int reviewId, string userId, bool isAdmin, CancellationToken ct)
		{
			var r = await context.ProductReviews.FirstOrDefaultAsync(x => x.Id == reviewId, ct);
			if (r == null) return false;
			if (!isAdmin && r.UserId != userId) return false;

			context.ProductReviews.Remove(r);
			await context.SaveChangesAsync(ct);
			return true;
		}
	}
}
