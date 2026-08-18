using PaladinHub.Models.Products;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public interface IProductReviewService
	{
		Task<bool> AddAsync(
			AddReviewInput input,
			string userId,
			CancellationToken cancellationToken = default);

		Task<bool> DeleteAsync(
			int reviewId,
			string userId,
			bool isAdmin,
			CancellationToken cancellationToken = default);
	}
}
