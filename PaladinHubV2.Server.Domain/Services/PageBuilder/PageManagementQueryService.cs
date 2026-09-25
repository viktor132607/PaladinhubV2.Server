using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder;

public sealed class PageManagementQueryService :
    IPageManagementQueryService
{
    private readonly AppDbContext _db;

    public PageManagementQueryService(
        AppDbContext db)
    {
        _db = db;
    }

    public Task<List<ContentPage>> ListAsync(
        CancellationToken cancellationToken)
    {
        return _db.ContentPages
            .AsNoTracking()
            .OrderBy(page => page.Section)
            .ThenBy(page => page.Title)
            .ToListAsync(cancellationToken);
    }

    public Task<ContentPage?> GetAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return _db.ContentPages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id,
                cancellationToken);
    }
}
