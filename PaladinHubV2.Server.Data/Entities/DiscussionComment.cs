namespace PaladinHubV2.Server.Data.Entities;

public class DiscussionComment
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid PostId { get; set; }
	public DiscussionPost Post { get; set; } = default!;

	public string AuthorId { get; set; } = default!;
	public User Author { get; set; } = default!;

	public string Content { get; set; } = default!;
	public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

	public int Likes { get; set; }
	public ICollection<DiscussionCommentLike> LikesCollection { get; set; } = new List<DiscussionCommentLike>();
}
