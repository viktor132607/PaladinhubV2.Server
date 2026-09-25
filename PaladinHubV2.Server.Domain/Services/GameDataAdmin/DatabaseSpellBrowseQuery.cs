using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.ViewModels;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class DatabaseSpellBrowseQuery :
    IDatabaseSpellBrowseQuery
{
    private readonly AppDbContext _db;

    public DatabaseSpellBrowseQuery(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task PopulateAsync(
        AdminDatabaseIndexViewModel model,
        DatabaseBrowseFilters filters,
        CancellationToken cancellationToken)
    {
        var query =
            _db.Spells
                .AsNoTracking()
                .AsQueryable();

        if (filters.CategoryId == 0)
        {
            query = query.Where(spell =>
                spell.CategoryId == null);
        }
        else if (filters.CategoryId > 0)
        {
            query = query.Where(spell =>
                spell.CategoryId != null &&
                filters.CategoryIds.Contains(
                    spell.CategoryId.Value));
        }

        if (filters.DisciplineId == 0)
        {
            query = query.Where(spell =>
                spell.DisciplineId == null);
        }
        else if (filters.DisciplineId > 0)
        {
            query = query.Where(spell =>
                spell.DisciplineId != null &&
                filters.DisciplineIds.Contains(
                    spell.DisciplineId.Value));
        }

        if (filters.PatchId == 0)
        {
            query = query.Where(spell =>
                spell.PatchId == null);
        }
        else if (filters.PatchId > 0)
        {
            query = query.Where(spell =>
                spell.PatchId ==
                filters.PatchId);
        }

        if (filters.TagId == 0)
        {
            query = query.Where(spell =>
                spell.TagIds.Length == 0);
        }
        else if (filters.TagId > 0)
        {
            query = query.Where(spell =>
                spell.TagIds.Contains(
                    filters.TagId.Value));
        }

        if (!string.IsNullOrWhiteSpace(
                filters.Search))
        {
            query = query.Where(spell =>
                spell.Name.Contains(
                    filters.Search) ||
                _db.GameTags.Any(tag =>
                    !tag.IsDeleted &&
                    spell.TagIds.Contains(tag.Id) &&
                    tag.Name.Contains(
                        filters.Search)) ||
                (spell.Description ??
                    string.Empty).Contains(
                    filters.Search));
        }

        model.Total =
            await query.CountAsync(
                cancellationToken);

        DatabaseBrowserService.ClampPage(
            model);

        model.Spells =
            await query
                .OrderBy(spell =>
                    spell.Name)
                .Skip(
                    (model.Page - 1) *
                    model.PageSize)
                .Take(model.PageSize)
                .ToListAsync(
                    cancellationToken);
    }
}
