using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Discussions;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Discussions;

namespace PaladinHubV2.Server.API.Controllers.Community
{
	[ApiController]
	[Authorize]
	[Route("api/discussions")]
	public sealed class DiscussionPostsController : ControllerBase
	{
		private readonly IDiscussionService _discussionService;
		private readonly UserManager<User> _userManager;

		public DiscussionPostsController(
			IDiscussionService discussionService,
			UserManager<User> userManager)
		{
			_discussionService = discussionService;
			_userManager = userManager;
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(
			[FromBody] CreatePostViewModel model)
		{
			model.Title = model.Title?.Trim() ?? string.Empty;
			model.Content = model.Content?.Trim() ?? string.Empty;

			if (string.IsNullOrWhiteSpace(model.Title))
			{
				ModelState.AddModelError(
					nameof(model.Title),
					"Title is required.");
			}

			if (string.IsNullOrWhiteSpace(model.Content))
			{
				ModelState.AddModelError(
					nameof(model.Content),
					"Content is required.");
			}

			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			string? userId = _userManager.GetUserId(User);
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			await _discussionService.CreateAsync(userId, model);
			return Ok(new { ok = true });
		}

		[HttpDelete("{id:guid}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(Guid id)
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
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			bool isAdmin = User.IsInRole("Admin");
			if (!isAdmin && post.AuthorId != userId)
			{
				return Forbid();
			}

			await _discussionService.DeleteAsync(id, userId, isAdmin);
			return NoContent();
		}

		[HttpPost("{id:guid}/like")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Like(Guid id)
		{
			string? userId = _userManager.GetUserId(User);
			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			if (!await _discussionService.ToggleLikeAsync(id, userId))
			{
				return NotFound(new
				{
					message = "Discussion not found."
				});
			}

			DiscussionPost? post =
				await _discussionService.GetByIdAsync(id);

			return Ok(new
			{
				ok = true,
				likes = post?.Likes ?? 0,
				likedByCurrentUser =
					post?.LikesCollection.Any(
						like => like.UserId == userId) ?? false
			});
		}
	}
}
