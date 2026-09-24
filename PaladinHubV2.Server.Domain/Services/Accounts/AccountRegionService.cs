using Microsoft.AspNetCore.Http;

namespace PaladinHubV2.Server.Domain.Services.Accounts;

public sealed class AccountRegionService : IAccountRegionService
{
    private const string DefaultRegion = "US";
    private const string Currency = "USD";
    private const string RegionName = "United States";

    private readonly IHttpContextAccessor _http;

    public AccountRegionService(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string? ReadRegionCookie()
    {
        HttpContext? context = _http.HttpContext;

        if (context?.Request?.Cookies is null)
        {
            return DefaultRegion;
        }

        return context.Request.Cookies.TryGetValue(
                   "region",
                   out string? value) &&
               !string.IsNullOrWhiteSpace(value)
            ? value
            : DefaultRegion;
    }

    public string GetCurrencyForRegion(string region) =>
        Currency;

    public string RegionDisplay(string region) =>
        RegionName;
}
