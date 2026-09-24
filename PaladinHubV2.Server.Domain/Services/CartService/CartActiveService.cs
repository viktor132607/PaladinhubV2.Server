using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Carts;

public sealed class CartActiveService : ICartActiveService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public CartActiveService(
        AppDbContext context,
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<bool> AddProduct(
        string productId,
        string userId)
    {
        bool productExists = await _context.Products
            .AsNoTracking()
            .AnyAsync(product => product.Id == productId);

        if (!productExists)
            return false;

        bool userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId);

        if (!userExists)
            return false;

        Cart cart = await GetOrCreateActiveCartAsync(userId);

        CartProduct? cartProduct = await _context.CartProducts
            .FirstOrDefaultAsync(item =>
                item.CartId == cart.Id &&
                item.ProductId == productId);

        if (cartProduct is null)
        {
            await _context.CartProducts.AddAsync(
                new CartProduct
                {
                    CartId = cart.Id,
                    ProductId = productId,
                    Quantity = 1
                });
        }
        else
        {
            cartProduct.Quantity++;
        }

        cart.UpdatedOn = UtcNow();
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IncreaseProduct(
        string productId,
        string userId)
    {
        CartProduct? cartProduct = await FindCartProductAsync(
            productId,
            userId);

        if (cartProduct is null)
            return false;

        cartProduct.Quantity++;
        cartProduct.Cart.UpdatedOn = UtcNow();
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DecreaseProduct(
        string productId,
        string userId)
    {
        CartProduct? cartProduct = await FindCartProductAsync(
            productId,
            userId);

        if (cartProduct is null)
            return false;

        if (cartProduct.Quantity > 1)
        {
            cartProduct.Quantity--;
        }
        else
        {
            _context.CartProducts.Remove(cartProduct);
        }

        cartProduct.Cart.UpdatedOn = UtcNow();
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveProduct(
        string productId,
        string userId)
    {
        CartProduct? cartProduct = await FindCartProductAsync(
            productId,
            userId);

        if (cartProduct is null)
            return false;

        _context.CartProducts.Remove(cartProduct);
        cartProduct.Cart.UpdatedOn = UtcNow();
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task CleanCart(User user)
    {
        if (user is null)
            return;

        Cart? cart = await _context.Carts
            .FirstOrDefaultAsync(candidate =>
                candidate.UserId == user.Id &&
                !candidate.IsArchived);

        if (cart is null)
            return;

        List<CartProduct> products = await _context.CartProducts
            .Where(item => item.CartId == cart.Id)
            .ToListAsync();

        if (products.Count > 0)
            _context.CartProducts.RemoveRange(products);

        cart.UpdatedOn = UtcNow();
        await _context.SaveChangesAsync();
    }

    private async Task<Cart> GetOrCreateActiveCartAsync(
        string userId)
    {
        Cart? cart = await _context.Carts
            .FirstOrDefaultAsync(candidate =>
                candidate.UserId == userId &&
                !candidate.IsArchived);

        if (cart is not null)
            return cart;

        cart = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = OrderStatusCatalog.Pending,
            UpdatedOn = UtcNow()
        };

        await _context.Carts.AddAsync(cart);
        return cart;
    }

    private async Task<CartProduct?> FindCartProductAsync(
        string productId,
        string userId)
    {
        bool userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId);

        if (!userExists)
            return null;

        Cart? cart = await _context.Carts
            .FirstOrDefaultAsync(candidate =>
                candidate.UserId == userId &&
                !candidate.IsArchived);

        if (cart is null)
            return null;

        return await _context.CartProducts
            .Include(item => item.Cart)
            .FirstOrDefaultAsync(item =>
                item.CartId == cart.Id &&
                item.ProductId == productId);
    }

    private DateTime UtcNow() =>
        _timeProvider.GetUtcNow().UtcDateTime;
}
