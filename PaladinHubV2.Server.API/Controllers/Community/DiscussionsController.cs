using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Discussions;
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
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

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
                CanDelete = userId != null && (isAdmin || post.AuthorId == userId)
            }));
        }

        [AllowAnonymous]
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Details(Guid id)
        {
            var post = await _discussionService.GetByIdAsync(id);
            if (post == null)
                return NotFound(new { message = "Discussion not found." });

            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            return Ok(new
            {
                post.Id,
                post.Title,
                post.Content,
                post.AuthorId,
                AuthorName = post.Author?.UserName ?? "Unknown user",
                post.CreatedOn,
                post.Likes,
                LikedByCurrentUser = userId != null && post.LikesCollection.Any(like => like.UserId == userId),
                CanDelete = userId != null && (isAdmin || post.AuthorId == userId),
                Comments = post.Comments
                    .OrderByDescending(comment => comment.CreatedOn)
                    .Select(comment => new
                    {
                        comment.Id,
                        comment.AuthorId,
                        AuthorName = comment.Author?.UserName ?? "Unknown user",
                        comment.Content,
                        comment.CreatedOn,
                        comment.Likes,
                        LikedByCurrentUser = userId != null && comment.LikesCollection.Any(like => like.UserId == userId)
                    })
            });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreatePostViewModel model)
        {
            model.Title = model.Title?.Trim() ?? string.Empty;
            model.Content = model.Content?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Title is required.");

            if (string.IsNullOrWhiteSpace(model.Content))
                ModelState.AddModelError(nameof(model.Content), "Content is required.");

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Authentication required." });

            await _discussionService.CreateAsync(userId, model);
            return Ok(new { ok = true });
        }

        [Authorize]
        [HttpDelete("{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var post = await _discussionService.GetByIdAsync(id);
            if (post == null)
                return NotFound(new { message = "Discussion not found." });

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Authentication required." });

            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && post.AuthorId != userId)
                return Forbid();

            await _discussionService.DeleteAsync(id, userId, isAdmin);
            return NoContent();
        }

        [Authorize]
        [HttpPost("{id:guid}/like")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Like(Guid id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Authentication required." });

            if (!await _discussionService.ToggleLikeAsync(id, userId))
                return NotFound(new { message = "Discussion not found." });

            var post = await _discussionService.GetByIdAsync(id);
            return Ok(new
            {
                ok = true,
                likes = post?.Likes ?? 0,
                likedByCurrentUser = post?.LikesCollection.Any(like => like.UserId == userId) ?? false
            });
        }

        [Authorize]
        [HttpPost("{id:guid}/comments")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(Guid id, [FromBody] AddCommentRequest request)
        {
            var content = request.Content?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
                return BadRequest(new { message = "Comment is required." });

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Authentication required." });

            if (!await _discussionService.AddCommentAsync(id, userId, content))
                return NotFound(new { message = "Discussion not found." });

            return Ok(new { ok = true });
        }

        [Authorize]
        [HttpPost("{postId:guid}/comments/{commentId:guid}/like")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LikeComment(Guid postId, Guid commentId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Authentication required." });

            var comment = await _discussionService.GetCommentByIdAsync(commentId);
            if (comment == null || comment.PostId != postId)
                return NotFound(new { message = "Comment not found." });

            if (!await _discussionService.ToggleCommentLikeAsync(commentId, userId))
                return NotFound(new { message = "Comment not found." });

            return Ok(new { ok = true });
        }

        public sealed class AddCommentRequest
        {
            public string Content { get; set; } = string.Empty;
        }
    }
}
