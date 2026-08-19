using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.Carts
{
	public sealed partial class CartSessionService : ICartSessionService
	{
		private const string AnonymousOwnerPrefix = "anon:";

		private readonly ICartService _cartService;
		private readonly ICartStore _cartStore;
		private readonly AppDbContext _db;

		public CartSessionService(
			ICartService cartService,
			ICartStore cartStore,
			AppDbContext db)
		{
			_cartService = cartService;
			_cartStore = cartStore;
			_db = db;
		}
	}
}
