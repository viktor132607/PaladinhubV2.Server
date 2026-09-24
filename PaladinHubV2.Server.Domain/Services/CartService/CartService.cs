using PaladinHub.Models.Carts;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Carts;

public sealed class CartService : ICartService
{
    private readonly ICartActiveService _activeCart;
    private readonly ICartArchiveQueryService _archiveQueries;
    private readonly ICartArchiveMutationService _archiveMutations;

    public CartService(
        ICartActiveService activeCart,
        ICartArchiveQueryService archiveQueries,
        ICartArchiveMutationService archiveMutations)
    {
        _activeCart = activeCart;
        _archiveQueries = archiveQueries;
        _archiveMutations = archiveMutations;
    }

    public Task<MyCartViewModel?> GetCartById(Guid cartId) =>
        _archiveQueries.GetCartById(cartId);

    public Task<ICollection<CartViewModel>> GetArchive() =>
        _archiveQueries.GetArchive();

    public Task<IReadOnlyCollection<ArchivedOrderSummary>>
        GetArchivedOrders() =>
        _archiveQueries.GetArchivedOrders();

    public Task<ArchivedOrderDetails?> GetArchivedOrder(
        Guid cartId) =>
        _archiveQueries.GetArchivedOrder(cartId);

    public Task<bool> UpdateOrderStatus(
        Guid cartId,
        string status) =>
        _archiveMutations.UpdateOrderStatus(
            cartId,
            status);

    public Task<bool> AddProduct(
        string id,
        string userId) =>
        _activeCart.AddProduct(id, userId);

    public Task<bool> IncreaseProduct(
        string id,
        string userId) =>
        _activeCart.IncreaseProduct(id, userId);

    public Task<bool> DecreaseProduct(
        string id,
        string userId) =>
        _activeCart.DecreaseProduct(id, userId);

    public Task<bool> RemoveProduct(
        string id,
        string userId) =>
        _activeCart.RemoveProduct(id, userId);

    public Task ArchiveCart(User user) =>
        _archiveMutations.ArchiveCart(user);

    public Task CleanCart(User user) =>
        _activeCart.CleanCart(user);
}
