using System;
using System.Collections.Generic;

namespace PaladinHubV2.Server.Common.Responses.Discussions
{
	public sealed record DiscussionListItemResponse(
		Guid Id,
		string Title,
		string Content,
		string AuthorId,
		string AuthorName,
		DateTime CreatedOn,
		int CommentsCount,
		int Likes,
		bool CanDelete);

	public sealed record DiscussionCommentResponse(
		Guid Id,
		string AuthorId,
		string AuthorName,
		string Content,
		DateTime CreatedOn,
		int Likes,
		bool LikedByCurrentUser);

	public sealed record DiscussionDetailsResponse(
		Guid Id,
		string Title,
		string Content,
		string AuthorId,
		string AuthorName,
		DateTime CreatedOn,
		DateTime? EditedOn,
		int Likes,
		bool LikedByCurrentUser,
		bool CanDelete,
		IReadOnlyList<DiscussionCommentResponse> Comments);
}
