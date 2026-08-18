using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities
{
	public class TalentBuild
	{
		[Key] public int Id { get; set; }

		[Required, MaxLength(100)]
		public string TreeKey { get; set; } = string.Empty;

		[Required, MaxLength(100)]
		public string Name { get; set; } = string.Empty;

		public bool IsDefault { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public ICollection<TalentBuildNode> Nodes { get; set; } = new List<TalentBuildNode>();
	}
}
