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

		public bool IsArchived { get; set; }
        public bool IsDeleted { get; set; }
        public int Version { get; set; } = 1;

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

namespace PaladinHubV2.Server.Data.Entities
{
    public sealed class PageRevision
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int PageId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore] public ContentPage Page { get; set; } = null!;
        public int Version { get; set; }
        [MaxLength(30)] public string Action { get; set; } = "";
        [MaxLength(256)] public string Actor { get; set; } = "";
        public string Snapshot { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
    public sealed record PageSnapshot(string Section, string Slug, string Title, bool IsPublished, string JsonLayout, bool IsArchived, bool IsDeleted);
    public sealed class PageInUseException() : InvalidOperationException("Remove navigation links to this page before deleting it, or archive it.");
}
