using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PaladinHubV2.Server.API.Controllers.Store
{
	[ApiController]
	[Authorize]
	[Route("api/checkout")]
	[Route("Checkout")]
	public sealed class CheckoutStatusController : ControllerBase
	{
		[HttpGet("Registered")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public IActionResult Registered(
			[FromQuery] string? orderId)
		{
			return Ok(new
			{
				orderId = orderId?.Trim() ?? string.Empty,
				status = "registered",
				message = "Your order was registered successfully."
			});
		}

		[HttpGet("Success")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public IActionResult Success(
			[FromQuery] string? orderId)
		{
			return Ok(new
			{
				orderId = orderId?.Trim() ?? string.Empty,
				status = "success",
				message = "Payment completed successfully."
			});
		}

		[HttpGet("Failure")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public IActionResult Failure(
			[FromQuery] string? message)
		{
			return Ok(new
			{
				status = "failure",
				message = string.IsNullOrWhiteSpace(message)
					? "Payment failed."
					: message.Trim()
			});
		}

		[HttpGet("~/api/home/thanks-for-purchasing")]
		[HttpGet("~/Home/ThanksForPurchasing")]
		public IActionResult ThanksForPurchasing()
		{
			return Ok(new
			{
				message = "Thank you for your purchase.",
				frontendRoute = "/checkout/ThanksForPurchasing"
			});
		}
	}
}
