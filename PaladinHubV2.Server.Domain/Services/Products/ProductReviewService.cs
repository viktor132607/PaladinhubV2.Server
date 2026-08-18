using PaladinHub.Models.Products;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public sealed class ProductReviewService : IProductReviewService
	{
		private readonly IProductService _products;

		public ProductReviewService(IProductService products)
		{
			_products = products;
		}

		public Task<bool> AddAsync(
			AddReviewInput input,
			string userId,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(input);

			return _products.AddReviewAsync(
				input,
				userId,
				cancellationToken);
		}

		public Task<bool> DeleteAsync(
			int reviewId,
			string userId,
			bool isAdmin,
			CancellationToken cancellationToken = default)
		{
			return _products.DeleteReviewAsync(
				reviewId,
				userId,
				isAdmin,
				cancellationToken);
		}
	}
}
