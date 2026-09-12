using System.ComponentModel.DataAnnotations;
namespace PaladinHubV2.Server.Data.Entities;

public sealed class SiteLanguage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(35)] public string Code { get; set; } = "";
    [MaxLength(100)] public string Name { get; set; } = "";
    public string ResourcesJson { get; set; } = "{}";
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    [ConcurrencyCheck] public int Version { get; set; } = 1;
}
public sealed class LanguageRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LanguageId { get; set; }
    public int Version { get; set; }
    [MaxLength(30)] public string Action { get; set; } = "";
    [MaxLength(100)] public string Actor { get; set; } = "";
    public string Snapshot { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
