using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHub.Models.Discussions;         
using PaladinHubV2.Server.Domain.Services.Discussions;       

namespace PaladinHubV2.Server.API.Controllers.Community
{
	[Authorize]
	public class DiscussionsController : Controller
	{
		private readonly IDiscussionService _discussionService;
		private readonly UserManager<User> _userManager;

		public DiscussionsController(IDiscussionService discussionService, UserManager<User> userManager)
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
			if (post == null) return NotFound();
			var vm = new PostDetailsViewModel { Post = post };
			return View(vm);
		}

		public IActionResult Create() => View(new CreatePostViewModel());

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(CreatePostViewModel model)
		{
			if (!ModelState.IsValid) return View(model);
			var userId = _userManager.GetUserId(User)!;
			await _discussionService.CreateAsync(userId, model);
			return RedirectToAction(nameof(Index));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(Guid id)
		{
			var userId = _userManager.GetUserId(User)!;
			var isAdmin = User.IsInRole("Admin");
			var ok = await _discussionService.DeleteAsync(id, userId, isAdmin);
			if (!ok) return Forbid();
			return RedirectToAction(nameof(Index));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Like(Guid id)
		{
			var userId = _userManager.GetUserId(User)!;
			await _discussionService.ToggleLikeAsync(id, userId);
			return RedirectToAction(nameof(Details), new { id });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> LikeComment(Guid id)
		{
			var userId = _userManager.GetUserId(User)!;
			await _discussionService.ToggleCommentLikeAsync(id, userId);

			var comment = await _discussionService.GetCommentByIdAsync(id);
			if (comment == null) return RedirectToAction(nameof(Index));
			return RedirectToAction(nameof(Details), new { id = comment.PostId });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddComment(Guid id, PostDetailsViewModel model)
		{
			if (string.IsNullOrWhiteSpace(model?.NewComment))
				return RedirectToAction(nameof(Details), new { id });

			var userId = _userManager.GetUserId(User)!;
			await _discussionService.AddCommentAsync(id, userId, model.NewComment);

			return RedirectToAction(nameof(Details), new { id });
		}
	}
}
