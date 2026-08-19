using PaladinHub.Models.Discussions;
using PaladinHubV2.Server.Common.Responses.Discussions;

namespace PaladinHubV2.Server.Domain.Services.Discussions
{
	public interface IDiscussionService
	{
		Task<IReadOnlyList<DiscussionListItemResponse>> GetAllAsync(
			string? currentUserId,
			bool isAdmin);

		Task<DiscussionDetailsResponse?> GetByIdAsync(
			Guid id,
			string? currentUserId,
			bool isAdmin);

		Task<DiscussionDetailsResponse> CreateAsync(
			string userId,
			CreatePostViewModel model,
			bool isAdmin);

		Task<bool> DeleteAsync(
			Guid id,
			string userId,
			bool isAdmin);

		Task<DiscussionDetailsResponse?> ToggleLikeAsync(
			Guid postId,
			string userId,
			bool isAdmin);

		Task<DiscussionDetailsResponse?> ToggleCommentLikeAsync(
			Guid postId,
			Guid commentId,
			string userId,
			bool isAdmin);

		Task<DiscussionDetailsResponse?> AddCommentAsync(
			Guid postId,
			string userId,
			string content,
			bool isAdmin);
	}
}
