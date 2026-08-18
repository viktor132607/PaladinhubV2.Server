using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities
{
	public class ContentPage
	{
		public int Id { get; set; }

		[Required, StringLength(50)]
		public string Section { get; set; } = "";

		[Required, StringLength(100)]
		public string Slug { get; set; } = "";

		[Required, StringLength(200)]
		public string Title { get; set; } = "";

		public bool IsPublished { get; set; } = true;

		[Required]
		public string JsonLayout { get; set; } = "[]";

		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }

		// новото поле
		[StringLength(100)]
		public string? UpdatedBy { get; set; }

		// НЕ [Timestamp]; конфигурира се в DbContext
		[ConcurrencyCheck]
		public byte[] RowVersion { get; set; } = Array.Empty<byte>();
	}
}
