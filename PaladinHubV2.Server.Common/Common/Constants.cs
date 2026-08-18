namespace PaladinHub.Common
{
	public static class Constants
	{
		public static class Cart
		{
			public const int TtlHours = 2;
			public const string RedisPrefix = "cart:";
		}

		// Roles
		public const string RoleAdmin = "Admin";
		public const string RoleUser = "User";

		// Claim Keys
		public const string ClaimUserId = "UserId";

		// Route Patterns
		public const string RouteDefault = "{controller=Home}/{action=Home}/{id?}";
	}
}
