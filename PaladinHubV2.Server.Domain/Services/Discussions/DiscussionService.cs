using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Discussions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PaladinHubV2.Server.Domain.Services.Discussions
{

	public class DiscussionService : IDiscussionService
	{
		private readonly AppDbContext _context;
		public DiscussionService(AppDbContext context) => _context = context;

		public async Task<IEnumerable<DiscussionPost>> GetAllAsync()
			=> await _context.DiscussionPosts
				.Include(p => p.Author)
				.Include(p => p.Comments)
				.OrderByDescending(p => p.CreatedOn)
				.ToListAsync();

		public async Task<DiscussionPost?> GetByIdAsync(Guid id)
			=> await _context.DiscussionPosts
				.Include(p => p.Author)
				.Include(p => p.Comments).ThenInclude(c => c.Author)
				.Include(p => p.LikesCollection)
				.FirstOrDefaultAsync(p => p.Id == id);

		public async Task<DiscussionComment?> GetCommentByIdAsync(Guid id)
			=> await _context.DiscussionComments
				.Include(c => c.Post)
				.FirstOrDefaultAsync(c => c.Id == id);

		public async Task CreateAsync(string userId, CreatePostViewModel model)
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
		}

		public async Task<bool> DeleteAsync(Guid id, string userId, bool isAdmin)
		{
			var post = await _context.DiscussionPosts.FirstOrDefaultAsync(p => p.Id == id);
			if (post == null) return false;
			if (!isAdmin && post.AuthorId != userId) return false;

			_context.DiscussionPosts.Remove(post);
			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> ToggleLikeAsync(Guid postId, string userId)
		{
			var post = await _context.DiscussionPosts.FindAsync(postId);
			if (post == null) return false;

			var like = await _context.DiscussionLikes
				.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

			if (like != null)
			{
				_context.DiscussionLikes.Remove(like);
				if (post.Likes > 0) post.Likes--;
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
			return true;
		}

		public async Task<bool> ToggleCommentLikeAsync(Guid commentId, string userId)
		{
			var comment = await _context.DiscussionComments.FindAsync(commentId);
			if (comment == null) return false;

			var like = await _context.DiscussionCommentLikes
				.FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);

			if (like != null)
			{
				_context.DiscussionCommentLikes.Remove(like);
				if (comment.Likes > 0) comment.Likes--;
			}
			else
			{
				_context.DiscussionCommentLikes.Add(new DiscussionCommentLike
				{
					CommentId = commentId,
					UserId = userId
				});
				comment.Likes++;
			}

			await _context.SaveChangesAsync();
			return true;
		}

		public async Task<bool> AddCommentAsync(Guid postId, string userId, string content)
		{
			var postExists = await _context.DiscussionPosts.AnyAsync(p => p.Id == postId);
			if (!postExists) return false;

			_context.DiscussionComments.Add(new DiscussionComment
			{
				PostId = postId,
				AuthorId = userId,
				Content = content,
				CreatedOn = DateTime.UtcNow
			});

			await _context.SaveChangesAsync();
			return true;
		}
	}
}
