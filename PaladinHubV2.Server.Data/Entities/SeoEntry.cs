using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PaladinHubV2.Server.Data.Entities;

[Index(nameof(PageId))]
[Index(nameof(SocialImageMediaId))]
public sealed class SeoEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int? PageId { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ContentPage? Page { get; set; }

    [MaxLength(2048)]
    public string Path { get; set; } = "*";

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string CanonicalUrl { get; set; } = string.Empty;

    [MaxLength(200)]
    public string SocialTitle { get; set; } = string.Empty;

    [MaxLength(500)]
    public string SocialDescription { get; set; } = string.Empty;

    public Guid? SocialImageMediaId { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public SpellIcon? SocialImageMedia { get; set; }

    [MaxLength(2048)]
    public string ImageUrl { get; set; } = string.Empty;

    public bool? Index { get; set; }
    public bool? Follow { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }

    [ConcurrencyCheck]
    public int Version { get; set; } = 1;

    public ICollection<SeoRevision> Revisions { get; set; } = new List<SeoRevision>();
}

[Index(nameof(EntryId), nameof(Version), IsUnique = true)]
public sealed class SeoRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EntryId { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public SeoEntry Entry { get; set; } = null!;

    public int Version { get; set; }

    [MaxLength(30)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Actor { get; set; } = string.Empty;

    public string Snapshot { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
