using Microsoft.AspNetCore.Http;

namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed class AccountRegionService : IAccountRegionService
	{
		private readonly IHttpContextAccessor _http;

		public AccountRegionService(IHttpContextAccessor http)
		{
			_http = http;
		}

		public string? ReadRegionCookie()
		{
			HttpContext? context = _http.HttpContext;
			if (context?.Request?.Cookies == null)
			{
				return "US";
			}

			return context.Request.Cookies.TryGetValue(
					"region",
					out string? value) &&
				!string.IsNullOrWhiteSpace(value)
					? value
					: "US";
		}

		public string GetCurrencyForRegion(string region) => "USD";

		public string RegionDisplay(string region) => "United States";
	}
}
