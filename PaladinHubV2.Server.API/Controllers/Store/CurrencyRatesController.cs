using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.API.Services;

namespace PaladinHubV2.Server.API.Controllers.Store;

[ApiController]
[Route("api/currency")]
public sealed class CurrencyRatesController : ControllerBase
{
    private readonly EuroUsdRateService _rates;
    public CurrencyRatesController(EuroUsdRateService rates) => _rates = rates;

    [HttpGet("rate")]
    public async Task<IActionResult> Rate(CancellationToken cancellationToken)
    {
        try
        {
            EuroUsdRate rate = await _rates.GetAsync(cancellationToken);
            return Ok(new { baseCurrency = "EUR", rate.UsdPerEur, rate.AsOf });
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or InvalidDataException or System.Xml.XmlException)
        {
            return StatusCode(503, new { message = "The EUR/USD exchange rate is temporarily unavailable." });
        }
    }
}
