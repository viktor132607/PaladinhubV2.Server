using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Carts;

public sealed class CartArchiveMutationService :
    ICartArchiveMutationService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public CartArchiveMutationService(
        AppDbContext context,
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task ArchiveCart(User user)
    {
        if (user is null)
            return;

        Cart? cart = await _context.Carts
            .FirstOrDefaultAsync(candidate =>
                candidate.UserId == user.Id &&
                !candidate.IsArchived);

        if (cart is null)
            return;

        DateTime now = UtcNow();

        cart.IsArchived = true;
        cart.OrderDate =
            now.ToString("yyyy-MM-dd HH:mm:ss");
        cart.Status = OrderStatusCatalog.Pending;
        cart.UpdatedOn = now;

        await _context.Carts.AddAsync(
            new Cart
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Status = OrderStatusCatalog.Pending,
                UpdatedOn = now
            });

        await _context.SaveChangesAsync();
    }

    public async Task<bool> UpdateOrderStatus(
        Guid cartId,
        string status)
    {
        if (!OrderStatusCatalog.TryNormalize(
                status,
                out string normalizedStatus))
        {
            return false;
        }

        Cart? cart = await _context.Carts
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == cartId &&
                candidate.IsArchived);

        if (cart is null)
            return false;

        cart.Status = normalizedStatus;
        cart.UpdatedOn = UtcNow();
        await _context.SaveChangesAsync();
        return true;
    }

    private DateTime UtcNow() =>
        _timeProvider.GetUtcNow().UtcDateTime;
}
