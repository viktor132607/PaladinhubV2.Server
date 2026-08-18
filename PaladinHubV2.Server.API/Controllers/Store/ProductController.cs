using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PaladinHub.Models.Products;
using PaladinHubV2.Server.Domain.Services.Products;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Route("api/products")]
	[Route("Products")]
	public sealed class ProductsController : ControllerBase
	{
		private readonly IProductService _productService;

		public ProductsController(
			IProductService productService)
		{
			_productService = productService;
		}

		[AllowAnonymous]
		[HttpGet]
		public IActionResult Index()
		{
			var target =
				"/Merchandise/List" +
				Request.QueryString;

			return Redirect(target);
		}

		[AllowAnonymous]
		[HttpGet("categories")]
		public async Task<IActionResult> Categories()
		{
			var categories =
				await _productService.GetCategories();

			return Ok(categories);
		}

		[Authorize(Roles = "Admin")]
		[HttpGet("Create")]
		public async Task<IActionResult> Create()
		{
			var model = await BuildCreateModelAsync();

			return Ok(model);
		}

		[Authorize(Roles = "Admin")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> CreateApi(
			[FromBody] CreateProductViewModel? model,
			CancellationToken cancellationToken)
		{
			return CreateCore(model, cancellationToken);
		}

		[Authorize(Roles = "Admin")]
		[HttpPost("Create")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> CreateLegacy(
			[FromForm] CreateProductViewModel? model,
			CancellationToken cancellationToken)
		{
			return CreateCore(model, cancellationToken);
		}

		[Authorize(Roles = "Admin")]
		[HttpGet("{id}/edit")]
		public Task<IActionResult> EditApi(
			[FromRoute] string id,
			CancellationToken cancellationToken)
		{
			return EditGetCore(id, cancellationToken);
		}

		[Authorize(Roles = "Admin")]
		[HttpGet("Edit")]
		public Task<IActionResult> EditLegacy(
			[FromQuery] string id,
			CancellationToken cancellationToken)
		{
			return EditGetCore(id, cancellationToken);
		}

		[Authorize(Roles = "Admin")]
		[HttpPut("{id}")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> EditApi(
			[FromRoute] string id,
			[FromBody] EditProductViewModel? model,
			CancellationToken cancellationToken)
		{
			if (model != null &&
				!string.Equals(
					id,
					model.Id,
					StringComparison.Ordinal))
			{
				return Task.FromResult<IActionResult>(
					BadRequest(new
					{
						message =
							"The route product ID does not match the request product ID."
					}));
			}

			return EditCore(model, cancellationToken);
		}

		[Authorize(Roles = "Admin")]
		[HttpPost("Edit")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> EditLegacy(
			[FromForm] EditProductViewModel? model,
			CancellationToken cancellationToken)
		{
			return EditCore(model, cancellationToken);
		}

		[Authorize(Roles = "Admin")]
		[HttpDelete("{id}")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteApi(
			[FromRoute] string id)
		{
			return DeleteCore(id);
		}

		[Authorize(Roles = "Admin")]
		[HttpGet("DeleteProduct")]
		public Task<IActionResult> DeleteLegacy(
			[FromQuery] string id)
		{
			return DeleteCore(id);
		}

		[AllowAnonymous]
		[HttpGet("{id}")]
		public Task<IActionResult> DetailsApi(
			[FromRoute] string id,
			CancellationToken cancellationToken)
		{
			return DetailsCore(id, cancellationToken);
		}

		[AllowAnonymous]
		[HttpGet("Details")]
		public Task<IActionResult> DetailsLegacy(
			[FromQuery] string id,
			CancellationToken cancellationToken)
		{
			return DetailsCore(id, cancellationToken);
		}

		[Authorize]
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

		[Authorize]
		[HttpPost("AddReview")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> AddReviewLegacy(
			[FromForm] AddReviewInput? input,
			CancellationToken cancellationToken)
		{
			return AddReviewCore(input, cancellationToken);
		}

		[Authorize]
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

		[Authorize]
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

		private async Task<IActionResult> CreateCore(
			CreateProductViewModel? model,
			CancellationToken cancellationToken)
		{
			if (model == null)
			{
				return BadRequest(new
				{
					message = "Product data is required."
				});
			}

			if (!string.IsNullOrWhiteSpace(model.NewCategory))
			{
				model.Category = model.NewCategory.Trim();
			}

			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			var created =
				await _productService.Create(model);

			if (created == null)
			{
				return Conflict(new
				{
					message =
						"Product with this name already exists."
				});
			}

			return StatusCode(
				StatusCodes.Status201Created,
				new
				{
					ok = true,
					product = created
				});
		}

		private async Task<IActionResult> EditGetCore(
			string? id,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return BadRequest(new
				{
					message = "Product ID is required."
				});
			}

			var model =
				await _productService.GetForEditAsync(
					id.Trim(),
					cancellationToken);

			if (model == null)
			{
				return NotFound(new
				{
					message = "Product not found."
				});
			}

			var categories =
				await _productService.GetCategories();

			model.CategorySelectList =
				categories.Select(category =>
					new SelectListItem
					{
						Value = category,
						Text = category,
						Selected = string.Equals(
							category,
							model.Category,
							StringComparison.OrdinalIgnoreCase)
					});

			model.Images ??= new();

			if (model.Images.Count == 0)
			{
				model.Images.Add(
					new ProductImageInputModel
					{
						Url = string.Empty,
						SortOrder = 0
					});
			}

			return Ok(model);
		}

		private async Task<IActionResult> EditCore(
			EditProductViewModel? model,
			CancellationToken cancellationToken)
		{
			if (model == null)
			{
				return BadRequest(new
				{
					message = "Product data is required."
				});
			}

			if (string.IsNullOrWhiteSpace(model.Id))
			{
				return BadRequest(new
				{
					message = "Product ID is required."
				});
			}

			if (!string.IsNullOrWhiteSpace(model.NewCategory))
			{
				model.Category = model.NewCategory.Trim();
			}

			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			var updated =
				await _productService.UpdateAsync(
					model,
					cancellationToken);

			if (!updated)
			{
				return Conflict(new
				{
					message =
						"Product was not found or another product already uses this name."
				});
			}

			return Ok(new
			{
				ok = true,
				id = model.Id,
				message = "Product updated successfully."
			});
		}

		private async Task<IActionResult> DeleteCore(
			string? id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return BadRequest(new
				{
					message = "Product ID is required."
				});
			}

			var deleted =
				await _productService.Delete(id.Trim());

			if (!deleted)
			{
				return NotFound(new
				{
					message = "Product not found."
				});
			}

			return NoContent();
		}

		private async Task<IActionResult> DetailsCore(
			string? id,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return BadRequest(new
				{
					message = "Product ID is required."
				});
			}

			var userId =
				User.FindFirstValue(
					ClaimTypes.NameIdentifier);

			var isAdmin =
				User.IsInRole("Admin");

			var model =
				await _productService.GetDetailsAsync(
					id.Trim(),
					userId,
					isAdmin,
					cancellationToken);

			if (model == null)
			{
				return NotFound(new
				{
					message = "Product not found."
				});
			}

			return Ok(model);
		}

		private async Task<IActionResult> AddReviewCore(
			AddReviewInput? input,
			CancellationToken cancellationToken)
		{
			var userId =
				User.FindFirstValue(
					ClaimTypes.NameIdentifier);

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

			var added =
				await _productService.AddReviewAsync(
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
			var userId =
				User.FindFirstValue(
					ClaimTypes.NameIdentifier);

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

			var isAdmin =
				User.IsInRole("Admin");

			var deleted =
				await _productService.DeleteReviewAsync(
					reviewId,
					userId,
					isAdmin,
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

		private async Task<CreateProductViewModel>
			BuildCreateModelAsync()
		{
			var categories =
				await _productService.GetCategories();

			var model =
				new CreateProductViewModel
				{
					Category = "Other",
					CategorySelectList =
						categories.Select(category =>
							new SelectListItem
							{
								Value = category,
								Text = category
							})
				};

			model.Images.Add(
				new ProductImageInputModel
				{
					Url = string.Empty,
					SortOrder = 0
				});

			return model;
		}
	}
}
