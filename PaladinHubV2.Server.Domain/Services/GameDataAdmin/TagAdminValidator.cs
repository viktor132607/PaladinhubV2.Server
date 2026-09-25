using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class TagAdminValidator :
    ITagAdminValidator
{
    private readonly AppDbContext _db;

    public TagAdminValidator(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task<string?> ValidateAsync(
        int id,
        TagRequest request,
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
            await _db.GameTags.AnyAsync(
                item =>
                    item.Id != id &&
                    !item.IsDeleted &&
                    item.Name.ToLower() == name,
                cancellationToken);

        return exists
            ? "A tag with this name already exists."
            : null;
    }
}
