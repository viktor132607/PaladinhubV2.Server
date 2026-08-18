using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PaladinHubV2.Server.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PaladinHubV2.Server.API.Services.Background
{
	public class CleanupCartService : BackgroundService
	{
		private readonly IServiceScopeFactory _scopeFactory;

		public CleanupCartService(IServiceScopeFactory scopeFactory)
		{
			_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				using var scope = _scopeFactory.CreateScope();
				var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

				var expirationDate = DateTime.UtcNow.AddDays(-7);
				var oldCarts = await db.Carts
					.Where(c => c.UpdatedOn < expirationDate)
					.ToListAsync(stoppingToken);

				if (oldCarts.Count > 0)
				{
					db.Carts.RemoveRange(oldCarts);
					await db.SaveChangesAsync(stoppingToken);
					Console.WriteLine($"[CleanupCartService] Removed {oldCarts.Count} expired carts.");
				}

				await Task.Delay(TimeSpan.FromHours(12), stoppingToken); // изпълнява се 2 пъти дневно
			}
		}
	}
}
