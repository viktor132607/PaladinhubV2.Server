using PaladinHubV2.Server.Data.Entities;

namespace PaladinHub.Models.Discussions
{
	public class PostDetailsViewModel
	{
		public DiscussionPost Post { get; set; } = default!;
		public string NewComment { get; set; } = "";
	}
}
