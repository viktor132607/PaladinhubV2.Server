using System.Text.Json;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Seo;

internal static class SeoRevisionFactory
{
    internal static SeoRevision Create(
        SeoEntry entry,
        string action,
        string actor)
    {
        string normalizedActor = string.IsNullOrWhiteSpace(actor)
            ? "admin"
            : actor.Trim();

        if (normalizedActor.Length > 256)
        {
            normalizedActor = normalizedActor[..256];
        }

        return new SeoRevision
        {
            EntryId = entry.Id,
            Version = entry.Version,
            Action = action.Length > 30 ? action[..30] : action,
            Actor = normalizedActor,
            Snapshot = JsonSerializer.Serialize(SeoEntryMapper.ToSnapshot(entry)),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
