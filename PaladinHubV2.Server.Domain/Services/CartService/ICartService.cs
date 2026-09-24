using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Carts;

namespace PaladinHubV2.Server.Domain.Services.Carts
{
    public sealed record ArchivedOrderSummary(
        Guid Id,
        string Username,
        string OrderDate,
        string Status);

    public sealed record ArchivedOrderItem(
        string Id,
        string Name,
        int Quantity,
        decimal Price,
        string ImageUrl);

    public sealed record ArchivedOrderDetails(
        Guid Id,
        string Username,
        string OrderDate,
        string Status,
        IReadOnlyCollection<ArchivedOrderItem> Items,
        decimal TotalPrice);

    public static class OrderStatusCatalog
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Shipped = "Shipped";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";

        public static readonly IReadOnlyCollection<string> All =
        [
            Pending,
            Processing,
            Shipped,
            Completed,
            Cancelled
        ];

        public static bool TryNormalize(
            string? value,
            out string normalized)
        {
            normalized = All.FirstOrDefault(status =>
                string.Equals(
                    status,
                    value?.Trim(),
                    StringComparison.OrdinalIgnoreCase)) ??
                string.Empty;

            return normalized.Length > 0;
        }
    }

    public interface ICartService
    {
        Task<MyCartViewModel?> GetCartById(Guid cartId);
        Task<ICollection<CartViewModel>> GetArchive();
        Task<IReadOnlyCollection<ArchivedOrderSummary>> GetArchivedOrders();
        Task<ArchivedOrderDetails?> GetArchivedOrder(Guid cartId);
        Task<bool> UpdateOrderStatus(Guid cartId, string status);
        Task<bool> AddProduct(string id, string userId);
        Task<bool> IncreaseProduct(string id, string userId);
        Task<bool> DecreaseProduct(string id, string userId);
        Task<bool> RemoveProduct(string id, string userId);
        Task ArchiveCart(User user);
        Task CleanCart(User user);
    }
}
