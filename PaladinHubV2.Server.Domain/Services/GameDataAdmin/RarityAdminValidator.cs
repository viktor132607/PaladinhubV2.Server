using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class RarityAdminValidator :
    IRarityAdminValidator
{
    private readonly AppDbContext _db;

    public RarityAdminValidator(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task<string?> ValidateAsync(
        int id,
        RarityRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }

        string name =
            request.Name.Trim().ToLowerInvariant();

        bool exists = await _db.ItemRarities.AnyAsync(
            item =>
                item.Id != id &&
                !item.IsDeleted &&
                item.Name.ToLower() == name,
            cancellationToken);

        return exists
            ? "A rarity with this name already exists."
            : null;
    }
}
