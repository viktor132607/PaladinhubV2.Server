using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Carts
{
	public interface ICartSessionService
	{
		Task<bool> AddProduct(string productId, string userId, CancellationToken ct);
		Task<bool> IncreaseProduct(string productId, string userId, CancellationToken ct);
		Task<bool> DecreaseProduct(string productId, string userId, CancellationToken ct);
		Task<bool> RemoveProduct(string productId, string userId, CancellationToken ct);
		Task ArchiveAndClear(User user, CancellationToken ct);
		Task CleanAndClear(User user, CancellationToken ct);
		Task SyncRedisToPersistent(User user, CancellationToken ct);
		Task<int> GetCount(string userId, CancellationToken ct);

	}
}
