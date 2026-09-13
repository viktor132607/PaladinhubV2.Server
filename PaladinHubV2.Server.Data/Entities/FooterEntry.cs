using System.ComponentModel.DataAnnotations;
namespace PaladinHubV2.Server.Data.Entities;
public sealed class FooterEntry
{
 public Guid Id { get; set; } = Guid.NewGuid();
 public Guid? ParentId { get; set; }
 [MaxLength(100)] public string Name { get; set; } = "";
 [MaxLength(20)] public string Kind { get; set; } = "text";
 [MaxLength(4000)] public string Text { get; set; } = "";
 [MaxLength(2048)] public string Url { get; set; } = "";
 [MaxLength(30)] public string Icon { get; set; } = "";
 public int SortOrder { get; set; }
 public bool OpenNewTab { get; set; }
 public bool IsArchived { get; set; }
 public bool IsDeleted { get; set; }
 [ConcurrencyCheck] public int Version { get; set; } = 1;
}
public sealed class FooterRevision
{
 public Guid Id { get; set; } = Guid.NewGuid();
 public Guid EntryId { get; set; }
 public int Version { get; set; }
 public string Action { get; set; } = "";
 public string Actor { get; set; } = "";
 public string Snapshot { get; set; } = "";
 public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
