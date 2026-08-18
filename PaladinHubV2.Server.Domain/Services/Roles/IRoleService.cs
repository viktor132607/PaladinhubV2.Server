using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Roles
{
	public interface IRoleService
	{
		Task<bool> AddUserToRole(User user, string roleName);
		Task<bool> CreateRole(string name);

	}
}