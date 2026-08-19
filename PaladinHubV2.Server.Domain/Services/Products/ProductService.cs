using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.Products
{
	public partial class ProductService : IProductService
	{
		private readonly AppDbContext context;

		public ProductService(AppDbContext context)
		{
			this.context = context;
		}
	}
}
