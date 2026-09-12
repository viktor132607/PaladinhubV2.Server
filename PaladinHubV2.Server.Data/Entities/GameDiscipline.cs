using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities;

public sealed class GameDiscipline
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(2000)] public string Description { get; set; } = "";
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    [ConcurrencyCheck] public int Version { get; set; } = 1;
}

public sealed class DisciplineRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int DisciplineId { get; set; }
    public int Version { get; set; }
    [MaxLength(30)] public string Action { get; set; } = "";
    [MaxLength(256)] public string Actor { get; set; } = "";
    public string Snapshot { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

