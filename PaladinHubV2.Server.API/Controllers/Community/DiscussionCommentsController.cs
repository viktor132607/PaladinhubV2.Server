using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Discussions;

namespace PaladinHubV2.Server.API.Controllers.Community
{
	[ApiController]
	[Authorize]
	[Route("api/discussions")]
	public sealed class DiscussionCommentsController : ControllerBase
	{
		private readonly IDiscussionService _discussionService;
		private readonly UserManager<User> _userManager;

		public DiscussionCommentsController(
			IDiscussionService discussionService,
			UserManager<User> userManager)
		{
			_discussionService = discussionService;
			_userManager = userManager;
		}

		[HttpPost("{id:guid}/comments")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddComment(
			Guid id,
			[FromBody] AddCommentRequest request)
		{
			string content = request.Content?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(content))
			{
				return BadRequest(new
				{
					message = "Comment is required."
				});
			}

			string? userId = _userManager.GetUserId(User);
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			if (!await _discussionService.AddCommentAsync(
					id,
					userId,
					content))
			{
				return NotFound(new
				{
					message = "Discussion not found."
				});
			}

			return Ok(new { ok = true });
		}

		[HttpPost("{postId:guid}/comments/{commentId:guid}/like")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> LikeComment(
			Guid postId,
			Guid commentId)
		{
			string? userId = _userManager.GetUserId(User);
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			DiscussionComment? comment =
				await _discussionService.GetCommentByIdAsync(commentId);

			if (comment == null || comment.PostId != postId)
			{
				return NotFound(new
				{
					message = "Comment not found."
				});
			}

			if (!await _discussionService.ToggleCommentLikeAsync(
					commentId,
					userId))
			{
				return NotFound(new
				{
					message = "Comment not found."
				});
			}

			return Ok(new { ok = true });
		}

		public sealed class AddCommentRequest
		{
			public string Content { get; set; } = string.Empty;
		}
	}
}
