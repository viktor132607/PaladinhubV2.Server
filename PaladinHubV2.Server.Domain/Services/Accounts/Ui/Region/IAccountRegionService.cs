namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public interface IAccountRegionService
	{
		string? ReadRegionCookie();
		string GetCurrencyForRegion(string region);
		string RegionDisplay(string region);
	}
}
