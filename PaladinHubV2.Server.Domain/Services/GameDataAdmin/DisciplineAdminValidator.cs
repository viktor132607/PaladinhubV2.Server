using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class DisciplineAdminValidator :
    IDisciplineAdminValidator
{
    private readonly AppDbContext _db;

    public DisciplineAdminValidator(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string?> ValidateAsync(
        int id,
        DisciplineRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }

        List<GameDiscipline> disciplines =
            await _db.GameDisciplines
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        string normalizedName = request.Name.Trim();

        if (disciplines.Any(item =>
                item.Id != id &&
                !item.IsDeleted &&
                item.ParentId == request.ParentId &&
                string.Equals(
                    item.Name,
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return "A class or specialization with this name already exists under this class.";
        }

        if (request.ParentId is not null)
        {
            if (request.ParentId == id)
            {
                return "A class cannot belong to itself or its specializations.";
            }

            GameDiscipline? owner = disciplines.Find(item =>
                item.Id == request.ParentId &&
                !item.IsDeleted);

            if (owner is null || owner.ParentId is not null)
            {
                return "A specialization must belong to a top-level class.";
            }

            if (disciplines.Any(item =>
                    item.ParentId == id &&
                    !item.IsDeleted))
            {
                return "A class with specializations cannot become a specialization.";
            }

            if (owner.IsArchived &&
                !request.IsArchived)
            {
                return "An active specialization cannot belong to an archived class.";
            }
        }

        if (request.IsArchived &&
            disciplines.Any(item =>
                item.ParentId == id &&
                !item.IsDeleted &&
                !item.IsArchived))
        {
            return "Archive or move active specializations first.";
        }

        return null;
    }
}
