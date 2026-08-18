using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Community.Microsoft.Extensions.Caching.PostgreSql;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.Configuration;
using PaladinHubV2.Server.API.Controllers.Content.Talents;
using PaladinHubV2.Server.API.Infrastructure.Routing;
using PaladinHubV2.Server.API.Services.Background;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Data.Seed;
using PaladinHubV2.Server.Data.Seed.Contracts;
using PaladinHubV2.Server.Domain.Services;
using PaladinHubV2.Server.Domain.Services.Accounts;
using PaladinHubV2.Server.Domain.Services.Avatars;
using PaladinHubV2.Server.Domain.Services.Carts;
using PaladinHubV2.Server.Domain.Services.Discussions;
using PaladinHubV2.Server.Domain.Services.ItemsService;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Domain.Services.Payments;
using PaladinHubV2.Server.Domain.Services.Presets;
using PaladinHubV2.Server.Domain.Services.Products;
using PaladinHubV2.Server.Domain.Services.Promos;
using PaladinHubV2.Server.Domain.Services.Roles;
using PaladinHubV2.Server.Domain.Services.SectionServices;
using PaladinHubV2.Server.Domain.Services.SpellbookService;
using PaladinHubV2.Server.Domain.Services.TalentTrees;
using PaladinHubV2.Server.Domain.Services.Transactions;
using PaladinHubV2.Server.Domain.Services.Wallet;
using Stripe;
using ProductServiceAlias =
	PaladinHubV2.Server.Domain.Services.Products.ProductService;

namespace PaladinHubV2.Server.API.ServiceExtensions
{
	public static class ServiceExtension
	{
		public static IServiceCollection AddPaladinHubApp(
			this IServiceCollection services,
			IConfiguration configuration,
			IWebHostEnvironment environment)
		{
			ResolvedDatabaseConnection resolvedConnection =
				DatabaseConnectionStringResolver.Resolve(
					configuration,
					environment);

			string connectionString =
				resolvedConnection.ConnectionString;

			bool isDevelopment =
				environment.IsDevelopment();

			SameSiteMode cookieSameSite =
				isDevelopment
					? SameSiteMode.Lax
					: SameSiteMode.None;

			CookieSecurePolicy cookieSecurePolicy =
				isDevelopment
					? CookieSecurePolicy.SameAsRequest
					: CookieSecurePolicy.Always;

			services.AddControllersWithViews();

			services.AddDbContext<AppDbContext>(
				options =>
					options.UseNpgsql(connectionString));

			services.AddDistributedPostgreSqlCache(
				options =>
				{
					options.ConnectionString =
						connectionString;

					options.SchemaName = "public";
					options.TableName = "__CacheEntries";
					options.CreateInfrastructure = true;

					options.ExpiredItemsDeletionInterval =
						TimeSpan.FromMinutes(30);
				});

			services.AddSession(
				options =>
				{
					options.Cookie.Name =
						"PaladinHub.Session";

					options.Cookie.HttpOnly = true;
					options.Cookie.IsEssential = true;
					options.Cookie.SameSite = cookieSameSite;

					options.Cookie.SecurePolicy =
						cookieSecurePolicy;

					options.IdleTimeout =
						TimeSpan.FromMinutes(30);
				});

			services.AddMemoryCache();

			services.Configure<RouteOptions>(
				options =>
				{
					options.ConstraintMap["palsec"] =
						typeof(AllowedSectionConstraint);
				});

			services
				.AddIdentity<User, IdentityRole>(
					options =>
					{
						options.Password
							.RequireNonAlphanumeric = true;

						options.Password.RequiredLength = 8;
						options.Password.RequireUppercase = true;
						options.Password.RequireLowercase = true;

						options.User.RequireUniqueEmail = true;

						options.SignIn
							.RequireConfirmedAccount = false;

						options.SignIn
							.RequireConfirmedEmail = false;

						options.SignIn
							.RequireConfirmedPhoneNumber = false;
					})
				.AddEntityFrameworkStores<AppDbContext>()
				.AddDefaultTokenProviders();

			string clientBaseUrl =
				(
					configuration["ClientApp:BaseUrl"] ??
					"http://localhost:3000"
				)
				.TrimEnd('/');

			services.ConfigureApplicationCookie(
				options =>
				{
					options.Cookie.Name =
						"PaladinHub.Identity";

					options.Cookie.HttpOnly = true;
					options.Cookie.IsEssential = true;
					options.Cookie.SameSite = cookieSameSite;

					options.Cookie.SecurePolicy =
						cookieSecurePolicy;

					options.ExpireTimeSpan =
						TimeSpan.FromDays(7);

					options.SlidingExpiration = true;

					options.Events.OnRedirectToLogin =
						context =>
						{
							if (IsApiRequest(
									context.Request))
							{
								context.Response.StatusCode =
									StatusCodes
										.Status401Unauthorized;

								return Task.CompletedTask;
							}

							string returnUrl =
								Uri.EscapeDataString(
									$"{context.Request.PathBase}" +
									$"{context.Request.Path}" +
									$"{context.Request.QueryString}");

							context.Response.Redirect(
								$"{clientBaseUrl}" +
								$"/Account/Login" +
								$"?returnUrl={returnUrl}");

							return Task.CompletedTask;
						};

					options.Events.OnRedirectToAccessDenied =
						context =>
						{
							if (IsApiRequest(
									context.Request))
							{
								context.Response.StatusCode =
									StatusCodes
										.Status403Forbidden;

								return Task.CompletedTask;
							}

							context.Response.Redirect(
								$"{clientBaseUrl}/Error/403");

							return Task.CompletedTask;
						};
				});

			services.AddAntiforgery(
				options =>
				{
					options.HeaderName =
						"X-CSRF-TOKEN";

					options.Cookie.Name =
						"PaladinHub.Antiforgery";

					options.Cookie.HttpOnly = true;
					options.Cookie.IsEssential = true;
					options.Cookie.SameSite = cookieSameSite;

					options.Cookie.SecurePolicy =
						cookieSecurePolicy;
				});

			string[] allowedOrigins =
				GetAllowedOrigins(configuration);

			services.AddCors(
				options =>
				{
					options.AddPolicy(
						"PaladinHubClient",
						policy =>
						{
							policy
								.WithOrigins(allowedOrigins)
								.AllowAnyHeader()
								.AllowAnyMethod()
								.AllowCredentials();
						});
				});

			ConfigureStripe(configuration);

			services.AddHttpContextAccessor();

			services.AddTransient<TalentsController>();

			services.AddHostedService<CleanupCartService>();

			services.AddScoped<ISeeder, UsersSeeder>();
			services.AddScoped<ISeeder, ProductsSeeder>();
			services.AddScoped<ISeeder, SpellbookSeeder>();
			services.AddScoped<ISeeder, ItemsSeeder>();
			services.AddScoped<ISeeder, DiscussionsSeeder>();

			services.AddScoped<
				ISpellbookService,
				SpellbookService>();

			services.AddScoped<
				IItemsService,
				ItemsService>();

			services.AddScoped<
				ICartService,
				CartService>();

			services.AddScoped<
				IProductService,
				ProductServiceAlias>();

			services.AddScoped<
				IRoleService,
				RoleService>();

			services.AddTransient<HolySectionService>();

			services.AddTransient<
				ProtectionSectionService>();

			services.AddTransient<
				RetributionSectionService>();

			services.AddScoped<
				ITalentTreeAdminService,
				TalentTreeAdminService>();

			services.AddScoped<
				IAccountUiService,
				AccountUiService>();

			services.AddScoped<
				ISecurityService,
				SecurityService>();

			services.AddScoped<
				IAvatarService,
				AvatarService>();

			services.AddScoped<
				IPaymentMethodsService,
				PaymentMethodsService>();

			services.AddScoped<
				ITransactionsService,
				TransactionsService>();

			services.AddScoped<
				IWalletService,
				WalletService>();

			services.AddScoped<
				IDiscussionService,
				DiscussionService>();

			services.AddScoped<
				ICartSessionService,
				CartSessionService>();

			services.AddScoped<
				ICartStore,
				MemoryCartStore>();

			services.AddScoped<
				IBlockRenderer,
				BlockRenderer>();

			services.AddScoped<
				ISpecializationTreeBuilder,
				HolySpecTreeBuilder>();

			services.AddScoped<
				ISpecializationTreeBuilder,
				ProtectionSpecTreeBuilder>();

			services.AddScoped<
				ISpecializationTreeBuilder,
				RetributionSpecTreeBuilder>();

			services.AddScoped<
				IClassTreeBuilder,
				PaladinClassTreeBuilder>();

			services.AddScoped<
				IHeroTalentTreesService,
				HeroTalentTreesService>();

			services.AddScoped<
				ITalentTreeService,
				TalentTreeService>();

			services.AddScoped<
				IPageService,
				PageService>();

			services.AddScoped<
				IJsonLayoutValidator,
				JsonLayoutValidator>();

			services.AddScoped<
				IDataPresetService,
				DataPresetService>();

			services.AddScoped<
				IPromoCodeService,
				PromoCodeService>();

			return services;
		}

		private static string[] GetAllowedOrigins(
			IConfiguration configuration)
		{
			List<string> origins = configuration
				.GetSection("Cors:AllowedOrigins")
				.GetChildren()
				.Select(item => item.Value)
				.Where(value =>
					!string.IsNullOrWhiteSpace(value))
				.Cast<string>()
				.ToList();

			string? environmentOrigins =
				configuration["CORS_ALLOWED_ORIGINS"];

			if (!string.IsNullOrWhiteSpace(
					environmentOrigins))
			{
				origins.AddRange(
					environmentOrigins.Split(
						[',', ';'],
						StringSplitOptions
							.RemoveEmptyEntries |
						StringSplitOptions
							.TrimEntries));
			}

			string[] normalizedOrigins = origins
				.Select(origin =>
					origin.Trim().TrimEnd('/'))
				.Where(origin =>
					!string.IsNullOrWhiteSpace(origin))
				.Distinct(
					StringComparer.OrdinalIgnoreCase)
				.ToArray();

			if (normalizedOrigins.Length > 0)
			{
				return normalizedOrigins;
			}

			return
			[
				"http://localhost:3000",
				"http://127.0.0.1:3000"
			];
		}

		private static void ConfigureStripe(
			IConfiguration configuration)
		{
			string stripeMode =
				configuration["STRIPE_MODE"] ??
				configuration["Stripe:Mode"] ??
				"Test";

			bool useLiveStripe =
				stripeMode.Equals(
					"Live",
					StringComparison.OrdinalIgnoreCase);

			string? stripeSecretKey =
				useLiveStripe
					? configuration[
						"STRIPE__SECRETKEY_LIVE"] ??
					  configuration[
						"Stripe__SecretKey_Live"] ??
					  configuration[
						"Stripe:SecretKey_Live"]
					: configuration[
						"STRIPE__SECRETKEY_TEST"] ??
					  configuration[
						"Stripe__SecretKey_Test"] ??
					  configuration[
						"Stripe:SecretKey_Test"];

			string? stripePublishableKey =
				useLiveStripe
					? configuration[
						"STRIPE__PUBLISHABLEKEY_LIVE"] ??
					  configuration[
						"Stripe__PublishableKey_Live"] ??
					  configuration[
						"Stripe:PublishableKey_Live"]
					: configuration[
						"STRIPE__PUBLISHABLEKEY_TEST"] ??
					  configuration[
						"Stripe__PublishableKey_Test"] ??
					  configuration[
						"Stripe:PublishableKey_Test"];

			if (!string.IsNullOrWhiteSpace(
					stripeSecretKey))
			{
				StripeConfiguration.ApiKey =
					stripeSecretKey;
			}

			if (configuration is
				ConfigurationManager manager)
			{
				manager["Stripe:SecretKey"] =
					stripeSecretKey ??
					string.Empty;

				manager["Stripe:PublishableKey"] =
					stripePublishableKey ??
					string.Empty;
			}
		}

		private static bool IsApiRequest(
			HttpRequest request)
		{
			if (request.Path.StartsWithSegments(
					"/api",
					StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			bool acceptsJson =
				request.Headers["Accept"]
					.Any(value =>
						value?.Contains(
							"application/json",
							StringComparison
								.OrdinalIgnoreCase) ==
						true);

			if (acceptsJson)
			{
				return true;
			}

			return string.Equals(
				request.Headers[
					"X-Requested-With"].ToString(),
				"XMLHttpRequest",
				StringComparison.OrdinalIgnoreCase);
		}
	}
}
