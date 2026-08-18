using PaladinHub.Models;
using System.Collections.Generic;

namespace PaladinHubV2.Server.Domain.Services.IService
{
	public interface ISectionService
	{
		string GetCoverImage();

		string GetPageTitle(string actionName);
		string GetPageText(string actionName);

		List<NavButton> GetCurrentSectionButtons(string actionName);

		List<NavButton> GetOtherSectionButtons();
	}
}
