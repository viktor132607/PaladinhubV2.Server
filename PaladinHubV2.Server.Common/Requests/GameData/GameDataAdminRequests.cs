using System.ComponentModel.DataAnnotations;

namespace PaladinHub.Models.GameData
{
	public sealed class ItemAdminRequest
	{
		public int Id { get; init; }

		[Required]
		[MaxLength(100)]
		public string Name { get; init; } = string.Empty;

		[MaxLength(100)]
		public string? Icon { get; init; }

		[MaxLength(100)]
		public string? SecondIcon { get; init; }

		[MaxLength(2000)]
		public string? Description { get; init; }

		[MaxLength(300)]
		public string? Url { get; init; }

		public int? ItemLevel { get; init; }

		public int? RequiredLevel { get; init; }

		[MaxLength(50)]
		public string? Quality { get; init; }
	}

	public sealed class SpellAdminRequest
	{
		private string _quality = "spell";

		public int Id { get; init; }

		[Required]
		[MaxLength(100)]
		public string Name { get; init; } = string.Empty;

		[MaxLength(100)]
		public string? Icon { get; init; }

		[MaxLength(500)]
		public string? Description { get; init; }

		[MaxLength(300)]
		public string? Url { get; init; }

		[MaxLength(50)]
		public string Quality
		{
			get => _quality;
			init => _quality =
				string.IsNullOrWhiteSpace(value)
					? "spell"
					: value.ToLowerInvariant();
		}
	}
}
