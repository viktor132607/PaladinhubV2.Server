using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PaladinHubV2.Server.API.Controllers.Infrastructure
{
	[Authorize(Roles = "Admin")]
	public abstract class AdminBaseController : ControllerBase
	{
	}
}
