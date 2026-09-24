using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Carts;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Carts;

public sealed class CartArchiveQueryService :
    ICartArchiveQueryService
{
    private readonly AppDbContext _context;

    public CartArchiveQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ICollection<CartViewModel>> GetArchive()
    {
        return await _context.Carts
            .AsNoTracking()
            .Include(cart => cart.User)
            .Include(cart => cart.CartProducts)
                .ThenInclude(item => item.Product)
            .Where(cart => cart.IsArchived)
            .Select(cart => new CartViewModel
            {
                Id = cart.Id,
                UserId = cart.UserId,
                User = cart.User,
                CartProducts = cart.CartProducts,
                OrderDate = cart.OrderDate ?? string.Empty,
                Products = cart.CartProducts
                    .Select(item => item.Product!)
                    .Where(product => product != null!)
                    .ToList()
            })
            .OrderByDescending(cart => cart.OrderDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<ArchivedOrderSummary>>
        GetArchivedOrders()
    {
        return await _context.Carts
            .AsNoTracking()
            .Where(cart => cart.IsArchived)
            .OrderByDescending(cart => cart.OrderDate)
            .Select(cart => new ArchivedOrderSummary(
                cart.Id,
                cart.User.UserName ?? "Unknown",
                cart.OrderDate ?? string.Empty,
                string.IsNullOrWhiteSpace(cart.Status)
                    ? OrderStatusCatalog.Pending
                    : cart.Status))
            .ToListAsync();
    }

    public async Task<ArchivedOrderDetails?> GetArchivedOrder(
        Guid cartId)
    {
        Cart? cart = await _context.Carts
            .AsNoTracking()
            .Include(candidate => candidate.User)
            .Include(candidate => candidate.CartProducts)
                .ThenInclude(item => item.Product)
                    .ThenInclude(product =>
                        product!.ThumbnailImage)
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == cartId &&
                candidate.IsArchived);

        if (cart is null)
            return null;

        ArchivedOrderItem[] items = cart.CartProducts
            .Where(item => item.Product is not null)
            .Select(item => new ArchivedOrderItem(
                item.ProductId,
                item.Product!.Name,
                item.Quantity,
                item.Product.Price,
                item.Product.ThumbnailImage?.Url ??
                    string.Empty))
            .ToArray();

        return new ArchivedOrderDetails(
            cart.Id,
            cart.User?.UserName ?? "Unknown",
            cart.OrderDate ?? string.Empty,
            string.IsNullOrWhiteSpace(cart.Status)
                ? OrderStatusCatalog.Pending
                : cart.Status,
            items,
            items.Sum(item =>
                item.Price * item.Quantity));
    }

    public async Task<MyCartViewModel?> GetCartById(
        Guid cartId)
    {
        bool archivedCartExists = await _context.Carts
            .AsNoTracking()
            .AnyAsync(cart =>
                cart.Id == cartId &&
                cart.IsArchived);

        if (!archivedCartExists)
            return null;

        List<CartProduct> cartProducts =
            await _context.CartProducts
                .AsNoTracking()
                .Include(item => item.Product)
                .Where(item => item.CartId == cartId)
                .ToListAsync();

        var model = new MyCartViewModel();

        foreach (CartProduct item in cartProducts)
        {
            model.MyProducts.Add(
                new ProductViewModel
                {
                    Id = item.ProductId,
                    Name = item.Product?.Name ??
                        string.Empty,
                    Price = item.Product?.Price ?? 0m,
                    Quantity = item.Quantity,
                    CartId = item.CartId,
                    Cart = null!
                });

            model.TotalPrice +=
                (item.Product?.Price ?? 0m) *
                item.Quantity;
        }

        return model;
    }
}
