using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Discussions;
using PaladinHubV2.Server.Common.Requests.Discussions;
using PaladinHubV2.Server.Common.Responses.Discussions;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Discussions;

namespace PaladinHubV2.Server.API.Controllers.Community
{
	[ApiController]
	[Authorize]
	[AutoValidateAntiforgeryToken]
	[Route("api/discussions")]
	public sealed class DiscussionsController : ControllerBase
	{
		private readonly IDiscussionService _discussions;
		private readonly UserManager<User> _userManager;

		public DiscussionsController(
			IDiscussionService discussions,
			UserManager<User> userManager)
		{
			_discussions = discussions;
			_userManager = userManager;
		}

		[AllowAnonymous]
		[HttpGet]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Index()
		{
			var posts = await _discussions.GetAllAsync(
				CurrentUserId(),
				IsAdmin());

			return Ok(posts);
		}

		[AllowAnonymous]
		[HttpGet("{id:guid}")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Details(
			[FromRoute] Guid id)
		{
			var post = await _discussions.GetByIdAsync(
				id,
				CurrentUserId(),
				IsAdmin());

			return post == null
				? NotFound(new { message = "Discussion not found." })
				: Ok(post);
		}

		[HttpPost]
		public async Task<IActionResult> Create(
			[FromBody] CreatePostViewModel model)
		{
			var userId = CurrentUserId();

			if (userId == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			DiscussionDetailsResponse created =
				await _discussions.CreateAsync(
					userId,
					model,
					IsAdmin());

			return CreatedAtAction(
				nameof(Details),
				new { id = created.Id },
				created);
		}

		[HttpDelete("{id:guid}")]
		public async Task<IActionResult> Delete(
			[FromRoute] Guid id)
		{
			var userId = CurrentUserId();

			if (userId == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			bool deleted = await _discussions.DeleteAsync(
				id,
				userId,
				IsAdmin());

			return deleted
				? NoContent()
				: Forbid();
		}

		[HttpPost("{id:guid}/like")]
		public async Task<IActionResult> Like(
			[FromRoute] Guid id)
		{
			var userId = CurrentUserId();

			if (userId == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			var post = await _discussions.ToggleLikeAsync(
				id,
				userId,
				IsAdmin());

			return post == null
				? NotFound(new { message = "Discussion not found." })
				: Ok(post);
		}

		[HttpPost("{id:guid}/comments")]
		public async Task<IActionResult> AddComment(
			[FromRoute] Guid id,
			[FromBody] AddDiscussionCommentRequest request)
		{
			var userId = CurrentUserId();

			if (userId == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			var post = await _discussions.AddCommentAsync(
				id,
				userId,
				request.Content,
				IsAdmin());

			return post == null
				? NotFound(new { message = "Discussion not found." })
				: Ok(post);
		}

		[HttpPost("{postId:guid}/comments/{commentId:guid}/like")]
		public async Task<IActionResult> LikeComment(
			[FromRoute] Guid postId,
			[FromRoute] Guid commentId)
		{
			var userId = CurrentUserId();

			if (userId == null)
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			var post = await _discussions.ToggleCommentLikeAsync(
				postId,
				commentId,
				userId,
				IsAdmin());

			return post == null
				? NotFound(new { message = "Comment not found." })
				: Ok(post);
		}

		private string? CurrentUserId()
		{
			return User.Identity?.IsAuthenticated == true
				? _userManager.GetUserId(User)
				: null;
		}

		private bool IsAdmin()
		{
			return User.Identity?.IsAuthenticated == true &&
				User.IsInRole("Admin");
		}
	}
}
