using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Data.Seed.Contracts;

namespace PaladinHubV2.Server.Data.Seed
{
	public class UsersSeeder : ISeeder
	{
		private readonly IServiceProvider _sp;
		public UsersSeeder(IServiceProvider sp) => _sp = sp;

		public async Task SeedAsync()
		{
			using var scope = _sp.CreateScope();
			var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
			var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

			// Existing production role names are preserved for backwards compatibility.
			foreach (var role in new[] { "Admin", "User" })
			{
				if (!await roleManager.RoleExistsAsync(role))
				{
					IdentityResult roleResult = await roleManager.CreateAsync(new IdentityRole(role));
					if (!roleResult.Succeeded)
					{
						throw new InvalidOperationException(
							$"Role create failed ({role}): " +
							string.Join("; ", roleResult.Errors.Select(error => error.Description)));
					}
				}
			}

			await EnsureAccessControlFoundationAsync(db, roleManager);

			// Четем админските данни от .env
			var adminName = Environment.GetEnvironmentVariable("ADMIN_NAME") ?? "Default Admin";
			var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? throw new Exception("ADMIN_EMAIL not set in .env");
			var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? throw new Exception("ADMIN_PASSWORD not set in .env");

			// Създаваме или обновяваме админа
			var admin = await userManager.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
			if (admin is null)
			{
				admin = new User
				{
					UserName = adminEmail,
					Email = adminEmail,
					EmailConfirmed = true,
					FullName = adminName
				};
				var result = await userManager.CreateAsync(admin, adminPassword);
				if (!result.Succeeded)
				{
					var msg = string.Join("; ", result.Errors.Select(e => e.Description));
					throw new Exception("Admin create failed: " + msg);
				}
			}
			else
			{
				// Автоматично обновяване на паролата
				var token = await userManager.GeneratePasswordResetTokenAsync(admin);
				var result = await userManager.ResetPasswordAsync(admin, token, adminPassword);
				if (!result.Succeeded)
				{
					var msg = string.Join("; ", result.Errors.Select(e => e.Description));
					throw new Exception("Admin password update failed: " + msg);
				}
			}

			if (!await userManager.IsInRoleAsync(admin, "Admin"))
			{
				IdentityResult addAdminRole = await userManager.AddToRoleAsync(admin, "Admin");
				if (!addAdminRole.Succeeded)
				{
					throw new InvalidOperationException(
						"Admin role assignment failed: " +
						string.Join("; ", addAdminRole.Errors.Select(error => error.Description)));
				}
			}

			// Демонстрационни потребители
			var defaultUserPassword = "Paladin#12345";
			var demoUsers = new (string FullName, string Email)[]
			{
				("Mila Petkova",      "mila.petkova@paladinhub.test"),
				("Ivan Dimitrov",     "ivan.dimitrov@paladinhub.test"),
				("Georgi Stoyanov",   "georgi.stoyanov@paladinhub.test"),
				("Elena Nikolova",    "elena.nikolova@paladinhub.test"),
				("Petar Ivanov",      "petar.ivanov@paladinhub.test"),
				("Raya Koleva",       "raya.koleva@paladinhub.test"),
				("Dimitar Angelov",   "dimitar.angelov@paladinhub.test"),
				("Vesela Marinova",   "vesela.marinova@paladinhub.test"),
				("Kalin Hristov",     "kalin.hristov@paladinhub.test"),
				("Sofia Georgieva",   "sofia.georgieva@paladinhub.test"),
			};

			foreach (var (name, email) in demoUsers)
			{
				var user = await userManager.FindByEmailAsync(email);
				if (user is null)
				{
					user = new User
					{
						UserName = email,
						Email = email,
						EmailConfirmed = true,
						FullName = name
					};
					var createRes = await userManager.CreateAsync(user, defaultUserPassword);
					if (!createRes.Succeeded)
					{
						var msg = string.Join("; ", createRes.Errors.Select(e => e.Description));
						throw new Exception($"User create failed ({email}): " + msg);
					}
				}

				if (!await userManager.IsInRoleAsync(user, "User"))
				{
					IdentityResult addUserRole = await userManager.AddToRoleAsync(user, "User");
					if (!addUserRole.Succeeded)
					{
						throw new InvalidOperationException(
							$"User role assignment failed ({email}): " +
							string.Join("; ", addUserRole.Errors.Select(error => error.Description)));
					}
				}
			}
		}

		private static async Task EnsureAccessControlFoundationAsync(
			AppDbContext db,
			RoleManager<IdentityRole> roleManager)
		{
			Stream stream = typeof(UsersSeeder).Assembly
				.GetManifestResourceStream("DatabaseUpgrades.RolesPermissions.sql")
				?? throw new InvalidOperationException(
					"Embedded roles/permissions database upgrade was not found.");

			string sql;
			await using (stream)
			using (var reader = new StreamReader(stream))
			{
				sql = await reader.ReadToEndAsync();
			}

			await using var transaction = await db.Database.BeginTransactionAsync();
			await db.Database.ExecuteSqlRawAsync(sql);

			IdentityRole adminRole = await roleManager.FindByNameAsync(SystemRoleCatalog.Administrator)
				?? throw new InvalidOperationException("The protected Admin role was not found after role seeding.");

			foreach (string permissionId in AdminPermissions.AllIds.Order(StringComparer.Ordinal))
			{
				await db.Database.ExecuteSqlInterpolatedAsync($"""
					INSERT INTO "RolePermissions" (
						"RoleId",
						"PermissionId",
						"GrantedBy",
						"GrantedAtUtc")
					VALUES (
						{adminRole.Id},
						{permissionId},
						{'system-bootstrap'},
						CURRENT_TIMESTAMP)
					ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;
					""");
			}

			await transaction.CommitAsync();
		}
	}
}
