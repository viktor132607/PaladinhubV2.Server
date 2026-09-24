namespace PaladinHubV2.Server.Domain.Services.Checkout;

public interface IEuroUsdRateProvider
{
    Task<decimal> GetUsdPerEurAsync(CancellationToken cancellationToken);
}
