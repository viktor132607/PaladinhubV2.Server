using Microsoft.AspNetCore.Identity;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Auth
{
	public sealed partial class AuthService : IAuthService
	{
		private const string UserRole = "User";

		private readonly SignInManager<User> _signInManager;
		private readonly UserManager<User> _userManager;
		private readonly RoleManager<IdentityRole> _roleManager;

		public AuthService(
			SignInManager<User> signInManager,
			UserManager<User> userManager,
			RoleManager<IdentityRole> roleManager)
		{
			_signInManager = signInManager;
			_userManager = userManager;
			_roleManager = roleManager;
		}
	}
}
