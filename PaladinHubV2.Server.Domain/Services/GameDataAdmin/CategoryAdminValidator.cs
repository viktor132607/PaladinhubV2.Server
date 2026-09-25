using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class CategoryAdminValidator :
    ICategoryAdminValidator
{
    private readonly AppDbContext _db;

    public CategoryAdminValidator(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string?> ValidateAsync(
        int id,
        CategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }

        List<Category> categories =
            await _db.Categories
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        string normalizedName = request.Name.Trim();

        if (categories.Any(item =>
                item.Id != id &&
                !item.IsDeleted &&
                item.ParentId == request.ParentId &&
                string.Equals(
                    item.Name,
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return "A category with this name already exists under this parent.";
        }

        var seen = new HashSet<int> { id };
        int? parentId = request.ParentId;

        while (parentId is not null)
        {
            if (!seen.Add(parentId.Value))
            {
                return "A category cannot be placed inside itself or its descendants.";
            }

            Category? parent = categories.Find(item =>
                item.Id == parentId &&
                !item.IsDeleted);

            if (parent is null)
            {
                return "Parent category does not exist. Restore it first.";
            }

            if (parent.IsArchived &&
                !request.IsArchived)
            {
                return "An active category cannot have an archived parent.";
            }

            parentId = parent.ParentId;
        }

        if (request.IsArchived &&
            categories.Any(item =>
                item.ParentId == id &&
                !item.IsDeleted &&
                !item.IsArchived))
        {
            return "Archive or move the active subcategories first.";
        }

        return null;
    }
}
