using PaladinHub.Models.Carts;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Carts;

public interface ICartActiveService
{
    Task<bool> AddProduct(string productId, string userId);
    Task<bool> IncreaseProduct(string productId, string userId);
    Task<bool> DecreaseProduct(string productId, string userId);
    Task<bool> RemoveProduct(string productId, string userId);
    Task CleanCart(User user);
}

public interface ICartArchiveQueryService
{
    Task<ICollection<CartViewModel>> GetArchive();
    Task<IReadOnlyCollection<ArchivedOrderSummary>> GetArchivedOrders();
    Task<ArchivedOrderDetails?> GetArchivedOrder(Guid cartId);
    Task<MyCartViewModel?> GetCartById(Guid cartId);
}

public interface ICartArchiveMutationService
{
    Task ArchiveCart(User user);
    Task<bool> UpdateOrderStatus(Guid cartId, string status);
}
