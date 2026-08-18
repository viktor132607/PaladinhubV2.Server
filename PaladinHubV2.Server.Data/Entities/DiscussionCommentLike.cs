namespace PaladinHubV2.Server.Data.Entities
{
	public class DiscussionCommentLike
	{
		public Guid Id { get; set; } = Guid.NewGuid();

		public Guid CommentId { get; set; }
		public DiscussionComment Comment { get; set; } = default!;

		public string UserId { get; set; } = default!;
		public User User { get; set; } = default!;
	}
}
