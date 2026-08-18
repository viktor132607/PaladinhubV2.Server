using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Data.Entities
{
	public class DiscussionPost
	{
		[Key] public Guid Id { get; set; } = Guid.NewGuid();

		[Required, MaxLength(120)]
		public string Title { get; set; } = default!;

		[Required]
		public string Content { get; set; } = default!;

		[Required] public string AuthorId { get; set; } = default!;
		public User Author { get; set; } = default!;

		public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
		public DateTime? EditedOn { get; set; }

		public ICollection<DiscussionComment> Comments { get; set; } = new List<DiscussionComment>();

		public int Likes { get; set; }

		public ICollection<DiscussionLike> LikesCollection { get; set; } = new List<DiscussionLike>();
	}
}
