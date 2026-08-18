using PaladinHub.Models.Carts;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Carts
{
	public interface ICartApplicationService
	{
		Task<MyCartViewModel> GetCartAsync(
			User user,
			CancellationToken cancellationToken);

		Task<MyCartViewModel> GetMiniCartAsync(
			User? user,
			CancellationToken cancellationToken);

		Task<int> GetCountAsync(
			string ownerKey,
			CancellationToken cancellationToken);

		Task<CartAddResult> AddAsync(
			string? productId,
			int quantity,
			string ownerKey,
			CancellationToken cancellationToken);

		Task<CartDeltaResult> IncreaseAsync(
			string? productId,
			string ownerKey,
			User? user,
			CancellationToken cancellationToken);

		Task<CartDeltaResult> DecreaseAsync(
			string? productId,
			string ownerKey,
			User? user,
			CancellationToken cancellationToken);

		Task<CartDeltaResult> RemoveAsync(
			string? productId,
			string ownerKey,
			User? user,
			CancellationToken cancellationToken);

		Task ClearAsync(
			User user,
			CancellationToken cancellationToken);
	}

	public sealed class CartAddResult
	{
		public bool Succeeded { get; init; }
		public string? Error { get; init; }
		public string ProductId { get; init; } = string.Empty;
		public int QuantityAdded { get; init; }
		public int CartCount { get; init; }
	}

	public sealed class CartDeltaResult
	{
		public bool Succeeded { get; init; }
		public string? Error { get; init; }
		public string ProductId { get; init; } = string.Empty;
		public bool Removed { get; init; }
		public bool HasDetailedTotals { get; init; }
		public int CartCount { get; init; }
		public int Quantity { get; init; }
		public decimal UnitPrice { get; init; }
		public decimal LineTotal { get; init; }
		public decimal CartTotal { get; init; }
	}
}
