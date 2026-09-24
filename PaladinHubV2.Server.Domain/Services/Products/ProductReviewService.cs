using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Products;

public sealed class ProductReviewService : IProductReviewService
{
    private readonly AppDbContext _db;

    public ProductReviewService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> AddReviewAsync(
        AddReviewInput input,
        string userId,
        CancellationToken ct)
    {
        bool hasInCart = await _db.CartProducts.AnyAsync(
            item =>
                item.ProductId == input.ProductId &&
                item.Cart.UserId == userId,
            ct);

        if (!hasInCart)
        {
            return false;
        }

        bool exists = await _db.ProductReviews.AnyAsync(
            review =>
                review.ProductId == input.ProductId &&
                review.UserId == userId,
            ct);

        if (exists)
        {
            return false;
        }

        _db.ProductReviews.Add(new ProductReview
        {
            ProductId = input.ProductId,
            UserId = userId,
            Rating = input.Rating,
            Content = string.IsNullOrWhiteSpace(input.Content)
                ? null!
                : input.Content.Trim()
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteReviewAsync(
        int reviewId,
        string userId,
        bool isAdmin,
        CancellationToken ct)
    {
        ProductReview? review =
            await _db.ProductReviews.FirstOrDefaultAsync(
                item => item.Id == reviewId,
                ct);

        if (review is null)
        {
            return false;
        }

        if (!isAdmin && review.UserId != userId)
        {
            return false;
        }

        _db.ProductReviews.Remove(review);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
