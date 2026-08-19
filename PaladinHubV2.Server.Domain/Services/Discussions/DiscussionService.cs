using Microsoft.EntityFrameworkCore;
using PaladinHub.Models.Discussions;
using PaladinHubV2.Server.Common.Responses.Discussions;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Discussions
{
	public sealed class DiscussionService : IDiscussionService
	{
		private readonly AppDbContext _context;

		public DiscussionService(AppDbContext context)
		{
			_context = context;
		}

		public async Task<IReadOnlyList<DiscussionListItemResponse>> GetAllAsync(
			string? currentUserId,
			bool isAdmin)
		{
			var posts = await _context.DiscussionPosts
				.AsNoTracking()
				.Include(post => post.Author)
				.Include(post => post.Comments)
				.OrderByDescending(post => post.CreatedOn)
				.ToListAsync();

			return posts
				.Select(post => new DiscussionListItemResponse(
					post.Id,
					post.Title,
					post.Content,
					post.AuthorId,
					post.Author?.UserName ?? "Unknown user",
					post.CreatedOn,
					post.Comments.Count,
					post.Likes,
					CanDelete(post.AuthorId, currentUserId, isAdmin)))
				.ToList();
		}

		public async Task<DiscussionDetailsResponse?> GetByIdAsync(
			Guid id,
			string? currentUserId,
			bool isAdmin)
		{
			var post = await _context.DiscussionPosts
				.AsNoTracking()
				.Include(current => current.Author)
				.Include(current => current.LikesCollection)
				.Include(current => current.Comments)
					.ThenInclude(comment => comment.Author)
				.Include(current => current.Comments)
					.ThenInclude(comment => comment.LikesCollection)
				.FirstOrDefaultAsync(current => current.Id == id);

			return post == null
				? null
				: ToDetails(post, currentUserId, isAdmin);
		}

		public async Task<DiscussionDetailsResponse> CreateAsync(
			string userId,
			CreatePostViewModel model,
			bool isAdmin)
		{
			var post = new DiscussionPost
			{
				Title = model.Title,
				Content = model.Content,
				AuthorId = userId,
				CreatedOn = DateTime.UtcNow
			};

			_context.DiscussionPosts.Add(post);
			await _context.SaveChangesAsync();

			return (await GetByIdAsync(post.Id, userId, isAdmin))!;
		}

		public async Task<bool> DeleteAsync(
			Guid id,
			string userId,
			bool isAdmin)
		{
			var post = await _context.DiscussionPosts
				.FirstOrDefaultAsync(current => current.Id == id);

			if (post == null)
			{
				return false;
			}

			if (!isAdmin && post.AuthorId != userId)
			{
				return false;
			}

			_context.DiscussionPosts.Remove(post);
			await _context.SaveChangesAsync();

			return true;
		}

		public async Task<DiscussionDetailsResponse?> ToggleLikeAsync(
			Guid postId,
			string userId,
			bool isAdmin)
		{
			var post = await _context.DiscussionPosts.FindAsync(postId);

			if (post == null)
			{
				return null;
			}

			var like = await _context.DiscussionLikes
				.FirstOrDefaultAsync(current =>
					current.PostId == postId &&
					current.UserId == userId);

			if (like != null)
			{
				_context.DiscussionLikes.Remove(like);

				if (post.Likes > 0)
				{
					post.Likes--;
				}
			}
			else
			{
				_context.DiscussionLikes.Add(new DiscussionLike
				{
					PostId = postId,
					UserId = userId
				});

				post.Likes++;
			}

			await _context.SaveChangesAsync();

			return await GetByIdAsync(postId, userId, isAdmin);
		}

		public async Task<DiscussionDetailsResponse?> ToggleCommentLikeAsync(
			Guid postId,
			Guid commentId,
			string userId,
			bool isAdmin)
		{
			var comment = await _context.DiscussionComments
				.FindAsync(commentId);

			if (comment == null || comment.PostId != postId)
			{
				return null;
			}

			var like = await _context.DiscussionCommentLikes
				.FirstOrDefaultAsync(current =>
					current.CommentId == commentId &&
					current.UserId == userId);

			if (like != null)
			{
				_context.DiscussionCommentLikes.Remove(like);

				if (comment.Likes > 0)
				{
					comment.Likes--;
				}
			}
			else
			{
				_context.DiscussionCommentLikes.Add(
					new DiscussionCommentLike
					{
						CommentId = commentId,
						UserId = userId
					});

				comment.Likes++;
			}

			await _context.SaveChangesAsync();

			return await GetByIdAsync(postId, userId, isAdmin);
		}

		public async Task<DiscussionDetailsResponse?> AddCommentAsync(
			Guid postId,
			string userId,
			string content,
			bool isAdmin)
		{
			if (string.IsNullOrWhiteSpace(content))
			{
				return await GetByIdAsync(postId, userId, isAdmin);
			}

			var postExists = await _context.DiscussionPosts
				.AnyAsync(post => post.Id == postId);

			if (!postExists)
			{
				return null;
			}

			_context.DiscussionComments.Add(new DiscussionComment
			{
				PostId = postId,
				AuthorId = userId,
				Content = content,
				CreatedOn = DateTime.UtcNow
			});

			await _context.SaveChangesAsync();

			return await GetByIdAsync(postId, userId, isAdmin);
		}

		private static DiscussionDetailsResponse ToDetails(
			DiscussionPost post,
			string? currentUserId,
			bool isAdmin)
		{
			var comments = post.Comments
				.OrderByDescending(comment => comment.CreatedOn)
				.Select(comment => new DiscussionCommentResponse(
					comment.Id,
					comment.AuthorId,
					comment.Author?.UserName ?? "Unknown user",
					comment.Content,
					comment.CreatedOn,
					comment.Likes,
					currentUserId != null &&
					comment.LikesCollection.Any(like =>
						like.UserId == currentUserId)))
				.ToList();

			return new DiscussionDetailsResponse(
				post.Id,
				post.Title,
				post.Content,
				post.AuthorId,
				post.Author?.UserName ?? "Unknown user",
				post.CreatedOn,
				post.EditedOn,
				post.Likes,
				currentUserId != null &&
				post.LikesCollection.Any(like =>
					like.UserId == currentUserId),
				CanDelete(post.AuthorId, currentUserId, isAdmin),
				comments);
		}

		private static bool CanDelete(
			string authorId,
			string? currentUserId,
			bool isAdmin)
		{
			return currentUserId != null &&
				(isAdmin || authorId == currentUserId);
		}
	}
}
