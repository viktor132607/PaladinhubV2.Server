using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using PaladinHubV2.Server.Domain.Services.Checkout;

namespace PaladinHubV2.Server.API.Services;

public sealed record EuroUsdRate(decimal UsdPerEur, string AsOf);

public sealed class EuroUsdRateService : IEuroUsdRateProvider
{
    private const string CacheKey = "currency:eur-usd";
    private readonly HttpClient _client;
    private readonly IMemoryCache _cache;

    public EuroUsdRateService(HttpClient client, IMemoryCache cache)
    {
        _client = client;
        _client.Timeout = TimeSpan.FromSeconds(6);
        _cache = cache;
    }

    public async Task<EuroUsdRate> GetAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out EuroUsdRate? cached) && cached is not null) return cached;
        const string endpoint = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml";
        string xml = await _client.GetStringAsync(endpoint, cancellationToken);
        XDocument document = XDocument.Parse(xml);
        XElement? usd = document.Descendants().FirstOrDefault(element =>
            element.Name.LocalName == "Cube" && (string?)element.Attribute("currency") == "USD");
        if (usd is null || !decimal.TryParse((string?)usd.Attribute("rate"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal rate) || rate <= 0m)
            throw new InvalidDataException("ECB did not provide a valid USD rate.");
        var result = new EuroUsdRate(rate, (string?)usd.Parent?.Attribute("time") ?? "");
        _cache.Set(CacheKey, result, TimeSpan.FromHours(12));
        return result;
    }

    public async Task<decimal> GetUsdPerEurAsync(CancellationToken cancellationToken) =>
        (await GetAsync(cancellationToken)).UsdPerEur;
}
