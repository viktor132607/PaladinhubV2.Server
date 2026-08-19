namespace PaladinHubV2.Server.Domain.Services.Accounts
{
	public sealed partial class AccountUiService
	{
		public string? ReadRegionCookie() =>
			_region.ReadRegionCookie();

		public string GetCurrencyForRegion(string region) =>
			_region.GetCurrencyForRegion(region);

		public string RegionDisplay(string region) =>
			_region.RegionDisplay(region);
	}
}
