using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities;

public sealed class RecordType
{
    [Key, MaxLength(50)]
    public string Name { get; set; } = string.Empty;
}
