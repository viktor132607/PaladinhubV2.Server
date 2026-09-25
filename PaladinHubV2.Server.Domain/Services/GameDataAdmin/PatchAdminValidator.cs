using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class PatchAdminValidator :
    IPatchAdminValidator
{
    private readonly AppDbContext _db;

    public PatchAdminValidator(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task<string?> ValidateAsync(
        int id,
        PatchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                request.Name))
        {
            return "Name is required.";
        }

        string name =
            request.Name
                .Trim()
                .ToLowerInvariant();

        bool exists =
            await _db.GamePatches.AnyAsync(
                item =>
                    item.Id != id &&
                    !item.IsDeleted &&
                    item.Name.ToLower() == name,
                cancellationToken);

        return exists
            ? "A patch with this name already exists."
            : null;
    }
}
