using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees;

public sealed class TalentPageContentReader :
    ITalentPageContentReader
{
    private readonly AppDbContext _db;

    public TalentPageContentReader(AppDbContext db)
    {
        _db = db ??
            throw new ArgumentNullException(nameof(db));
    }

    public async Task<TalentPageContent> LoadAsync()
    {
        var spells = await _db.Spells
            .AsNoTracking()
            .ToListAsync();

        var items = await _db.Items
            .AsNoTracking()
            .ToListAsync();

        return new TalentPageContent(
            spells,
            items);
    }
}
