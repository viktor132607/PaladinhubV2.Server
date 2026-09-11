using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities;

public sealed class SpellIcon
{
    public Guid Id { get; set; }
    [MaxLength(255)] public string Name { get; set; } = string.Empty;
    [MaxLength(32)] public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
}
