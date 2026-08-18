using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities
{
	public class DataPreset
	{
		[Key] public int Id { get; set; }

		[Required, MaxLength(150)]
		public string Name { get; set; } = string.Empty;

		[Required, MaxLength(50)]
		public string Entity { get; set; } = string.Empty;   // "items" | "spells" | "buildNodes"

		[MaxLength(50)]
		public string? Section { get; set; }

		[Required]
		public string JsonQuery { get; set; } = "{}";

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	}
}
