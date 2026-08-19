using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Discussions;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Discussions;

namespace PaladinHubV2.Server.API.Controllers.Community
{
	[Authorize]
	public sealed class DiscussionsController : Controller
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
		public async Task<IActionResult> Index()
		{
			var posts = await _discussionService.GetAllAsync();
			return View(posts);
		}

		[AllowAnonymous]
		public async Task<IActionResult> Details(Guid id)
		{
			var post = await _discussionService.GetByIdAsync(id);

			if (post == null)
			{
				return NotFound();
			}

			return View(new PostDetailsViewModel
			{
				Post = post
			});
		}

		public IActionResult Create()
		{
			return View(new CreatePostViewModel());
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(
			CreatePostViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			await _discussionService.CreateAsync(
				CurrentUserId(),
				model);

			return RedirectToAction(nameof(Index));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(Guid id)
		{
			bool deleted = await _discussionService.DeleteAsync(
				id,
				CurrentUserId(),
				User.IsInRole("Admin"));

			return deleted
				? RedirectToAction(nameof(Index))
				: Forbid();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Like(Guid id)
		{
			await _discussionService.ToggleLikeAsync(
				id,
				CurrentUserId());

			return RedirectToDetails(id);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> LikeComment(Guid id)
		{
			await _discussionService.ToggleCommentLikeAsync(
				id,
				CurrentUserId());

			var comment =
				await _discussionService.GetCommentByIdAsync(id);

			return comment == null
				? RedirectToAction(nameof(Index))
				: RedirectToDetails(comment.PostId);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddComment(
			Guid id,
			PostDetailsViewModel model)
		{
			if (string.IsNullOrWhiteSpace(model?.NewComment))
			{
				return RedirectToDetails(id);
			}

			await _discussionService.AddCommentAsync(
				id,
				CurrentUserId(),
				model.NewComment);

			return RedirectToDetails(id);
		}

		private string CurrentUserId()
		{
			return _userManager.GetUserId(User)!;
		}

		private IActionResult RedirectToDetails(Guid id)
		{
			return RedirectToAction(
				nameof(Details),
				new { id });
		}
	}
}
