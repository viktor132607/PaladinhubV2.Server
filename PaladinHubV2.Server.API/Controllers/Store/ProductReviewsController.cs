using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/products")]
	[Route("Products")]
	public sealed class ProductReviewsController : ControllerBase
	{
		private readonly IProductReviewService _reviews;

		public ProductReviewsController(IProductReviewService reviews)
		{
			_reviews = reviews;
		}

		[HttpPost("{id}/reviews")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> AddReviewApi(
			[FromRoute] string id,
			[FromBody] AddReviewInput? input,
			CancellationToken cancellationToken)
		{
			if (input != null &&
				!string.Equals(
					id,
					input.ProductId,
					StringComparison.Ordinal))
			{
				return Task.FromResult<IActionResult>(
					BadRequest(new
					{
						message =
							"The route product ID does not match the review product ID."
					}));
			}

			return AddReviewCore(input, cancellationToken);
		}

		[HttpPost("AddReview")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> AddReviewLegacy(
			[FromForm] AddReviewInput? input,
			CancellationToken cancellationToken)
		{
			return AddReviewCore(input, cancellationToken);
		}

		[HttpDelete("{productId}/reviews/{reviewId:int}")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteReviewApi(
			[FromRoute] string productId,
			[FromRoute] int reviewId,
			CancellationToken cancellationToken)
		{
			return DeleteReviewCore(
				reviewId,
				productId,
				cancellationToken);
		}

		[HttpPost("DeleteReview")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteReviewLegacy(
			[FromForm] int id,
			[FromForm] string productId,
			CancellationToken cancellationToken)
		{
			return DeleteReviewCore(
				id,
				productId,
				cancellationToken);
		}

		private async Task<IActionResult> AddReviewCore(
			AddReviewInput? input,
			CancellationToken cancellationToken)
		{
			string? userId =
				User.FindFirstValue(ClaimTypes.NameIdentifier);

			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			if (input == null)
			{
				return BadRequest(new
				{
					message = "Review data is required."
				});
			}

			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			bool added =
				await _reviews.AddAsync(
					input,
					userId,
					cancellationToken);

			if (!added)
			{
				return Conflict(new
				{
					message =
						"You can review only products in your cart and each product can be reviewed only once."
				});
			}

			return StatusCode(
				StatusCodes.Status201Created,
				new
				{
					ok = true,
					productId = input.ProductId,
					message = "Review added successfully."
				});
		}

		private async Task<IActionResult> DeleteReviewCore(
			int reviewId,
			string? productId,
			CancellationToken cancellationToken)
		{
			_ = productId;

			string? userId =
				User.FindFirstValue(ClaimTypes.NameIdentifier);

			if (string.IsNullOrWhiteSpace(userId))
			{
				return Unauthorized(new
				{
					message = "Authentication required."
				});
			}

			if (reviewId <= 0)
			{
				return BadRequest(new
				{
					message = "Invalid review ID."
				});
			}

			bool deleted =
				await _reviews.DeleteAsync(
					reviewId,
					userId,
					User.IsInRole("Admin"),
					cancellationToken);

			if (!deleted)
			{
				return StatusCode(
					StatusCodes.Status403Forbidden,
					new
					{
						message =
							"Review not found or you are not allowed to delete it."
					});
			}

			return NoContent();
		}
	}
}
