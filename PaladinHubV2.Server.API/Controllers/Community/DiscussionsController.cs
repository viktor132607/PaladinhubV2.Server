using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Discussions;

namespace PaladinHubV2.Server.API.Controllers.Community
{
	[ApiController]
	[Route("api/discussions")]
	public sealed class DiscussionsController : ControllerBase
	{
		private readonly IDiscussionService _discussionService;
		private readonly UserManager<User> _userManager;

		public DiscussionsController(
			IDiscussionService discussionService,
			UserManager<User> userManager)
		{
			_discussionService = discussionService;
			_userManager = userManager;
		}

		[AllowAnonymous]
		[HttpGet]
		public async Task<IActionResult> Index()
		{
			var posts = await _discussionService.GetAllAsync();
			string? userId = _userManager.GetUserId(User);
			bool isAdmin = User.IsInRole("Admin");

			return Ok(posts.Select(post => new
			{
				post.Id,
				post.Title,
				post.Content,
				post.AuthorId,
				AuthorName = post.Author?.UserName ?? "Unknown user",
				post.CreatedOn,
				CommentsCount = post.Comments?.Count ?? 0,
				post.Likes,
				CanDelete =
					userId != null &&
					(isAdmin || post.AuthorId == userId)
			}));
		}

		[AllowAnonymous]
		[HttpGet("{id:guid}")]
		public async Task<IActionResult> Details(Guid id)
		{
			DiscussionPost? post =
				await _discussionService.GetByIdAsync(id);

			if (post == null)
			{
				return NotFound(new
				{
					message = "Discussion not found."
				});
			}

			string? userId = _userManager.GetUserId(User);
			bool isAdmin = User.IsInRole("Admin");

			return Ok(new
			{
				post.Id,
				post.Title,
				post.Content,
				post.AuthorId,
				AuthorName = post.Author?.UserName ?? "Unknown user",
				post.CreatedOn,
				post.Likes,
				LikedByCurrentUser =
					userId != null &&
					post.LikesCollection.Any(
						like => like.UserId == userId),
				CanDelete =
					userId != null &&
					(isAdmin || post.AuthorId == userId),
				Comments = post.Comments
					.OrderByDescending(comment => comment.CreatedOn)
					.Select(comment => new
					{
						comment.Id,
						comment.AuthorId,
						AuthorName =
							comment.Author?.UserName ??
							"Unknown user",
						comment.Content,
						comment.CreatedOn,
						comment.Likes,
						LikedByCurrentUser =
							userId != null &&
							comment.LikesCollection.Any(
								like => like.UserId == userId)
					})
			});
		}
	}
}
