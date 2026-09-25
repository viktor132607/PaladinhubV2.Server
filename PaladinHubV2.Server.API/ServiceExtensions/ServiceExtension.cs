using System;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Community.Microsoft.Extensions.Caching.PostgreSql;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
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
using PaladinHubV2.Server.Domain.Services.Checkout;
using PaladinHubV2.Server.Domain.Services.Discussions;
using PaladinHubV2.Server.Domain.Services.ItemsService;
using PaladinHubV2.Server.Domain.Services.PageBuilder;
using PaladinHubV2.Server.Domain.Services.Payments;
using PaladinHubV2.Server.Domain.Services.Presets;
using PaladinHubV2.Server.Domain.Services.Products;
using PaladinHubV2.Server.Domain.Services.Promos;
using PaladinHubV2.Server.Domain.Services.Roles;
using PaladinHubV2.Server.Domain.Services.SectionServices;
using PaladinHubV2.Server.Domain.Services.Seo;
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

			string connectionString = resolvedConnection.ConnectionString;
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<
                PaladinHubV2.Server.API.Services.IExternalProcessExecutor,
                PaladinHubV2.Server.API.Services.SystemExternalProcessExecutor>();
            services.AddSingleton<
                PaladinHubV2.Server.API.Services.IDatabaseBackupFileStore,
                PaladinHubV2.Server.API.Services.PhysicalDatabaseBackupFileStore>();
            services.AddSingleton<
                PaladinHubV2.Server.API.Services.IPgDumpArchiveValidator,
                PaladinHubV2.Server.API.Services.PgDumpArchiveValidator>();
            services.AddSingleton<
                PaladinHubV2.Server.API.Services.IDatabasePoolManager,
                PaladinHubV2.Server.API.Services.NpgsqlDatabasePoolManager>();
            services.AddSingleton<
                PaladinHubV2.Server.API.Services.IPostgresToolRunner>(
                provider => new PaladinHubV2.Server.API.Services.PostgresToolRunner(
                    connectionString,
                    provider.GetRequiredService<
                        PaladinHubV2.Server.API.Services.IExternalProcessExecutor>(),
                    provider.GetRequiredService<
                        ILogger<PaladinHubV2.Server.API.Services.PostgresToolRunner>>()));
            services.AddSingleton<
                PaladinHubV2.Server.API.Services.DatabaseBackupService>();
			bool isDevelopment = environment.IsDevelopment();
			SameSiteMode cookieSameSite =
				isDevelopment ? SameSiteMode.Lax : SameSiteMode.None;
			CookieSecurePolicy cookieSecurePolicy =
				isDevelopment
					? CookieSecurePolicy.SameAsRequest
					: CookieSecurePolicy.Always;

			services.AddControllersWithViews();
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("account-security", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                        }));
            });
			services.AddDbContext<AppDbContext>(
				options => options.UseNpgsql(connectionString));

			services.AddDistributedPostgreSqlCache(
				options =>
				{
					options.ConnectionString = connectionString;
					options.SchemaName = "public";
					options.TableName = "__CacheEntries";
					options.CreateInfrastructure = true;
					options.ExpiredItemsDeletionInterval =
						TimeSpan.FromMinutes(30);
				});

			services.AddSession(
				options =>
				{
					options.Cookie.Name = "PaladinHub.Session";
					options.Cookie.HttpOnly = true;
					options.Cookie.IsEssential = true;
					options.Cookie.SameSite = cookieSameSite;
					options.Cookie.SecurePolicy = cookieSecurePolicy;
					options.IdleTimeout = TimeSpan.FromMinutes(30);
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
						options.Password.RequireNonAlphanumeric = true;
						options.Password.RequiredLength = 8;
						options.Password.RequireUppercase = true;
						options.Password.RequireLowercase = true;
						options.User.RequireUniqueEmail = true;
						options.SignIn.RequireConfirmedAccount = false;
						options.SignIn.RequireConfirmedEmail = false;
						options.SignIn.RequireConfirmedPhoneNumber = false;
					})
				.AddEntityFrameworkStores<AppDbContext>()
				.AddDefaultTokenProviders()
				.AddTokenProvider<SessionEmailTokenProvider>(TokenOptions.DefaultEmailProvider);

			// Identity's temporary and remembered-device cookies must also cross
			// the client/API origin boundary, just like the application cookie.
			foreach (string scheme in new[] { IdentityConstants.TwoFactorUserIdScheme, IdentityConstants.TwoFactorRememberMeScheme })
			{
				services.Configure<CookieAuthenticationOptions>(scheme, options =>
				{
					options.Cookie.HttpOnly = true;
					options.Cookie.IsEssential = true;
					options.Cookie.SameSite = cookieSameSite;
					options.Cookie.SecurePolicy = cookieSecurePolicy;
				});
			}

			services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(30));

			string clientBaseUrl =
				(configuration["ClientApp:BaseUrl"] ?? "http://localhost:3000")
				.TrimEnd('/');

			services.ConfigureApplicationCookie(
				options =>
				{
					options.Cookie.Name = "PaladinHub.Identity";
					options.Cookie.HttpOnly = true;
					options.Cookie.IsEssential = true;
					options.Cookie.SameSite = cookieSameSite;
					options.Cookie.SecurePolicy = cookieSecurePolicy;
					options.ExpireTimeSpan = TimeSpan.FromDays(7);
					options.SlidingExpiration = true;

					options.Events.OnRedirectToLogin =
						context =>
						{
							if (IsApiRequest(context.Request))
							{
								context.Response.StatusCode =
									StatusCodes.Status401Unauthorized;
								return Task.CompletedTask;
							}

							string returnUrl = Uri.EscapeDataString(
								$"{context.Request.PathBase}" +
								$"{context.Request.Path}" +
								$"{context.Request.QueryString}");

							context.Response.Redirect(
								$"{clientBaseUrl}/Account/Login?returnUrl={returnUrl}");
							return Task.CompletedTask;
						};

					options.Events.OnRedirectToAccessDenied =
						context =>
						{
							if (IsApiRequest(context.Request))
							{
								context.Response.StatusCode =
									StatusCodes.Status403Forbidden;
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
					options.HeaderName = "X-CSRF-TOKEN";
					options.Cookie.Name = "PaladinHub.Antiforgery";
					options.Cookie.HttpOnly = true;
					options.Cookie.IsEssential = true;
					options.Cookie.SameSite = cookieSameSite;
					options.Cookie.SecurePolicy = cookieSecurePolicy;
				});

			string[] allowedOrigins = GetAllowedOrigins(configuration);
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

			services.AddScoped<ISpellbookService, SpellbookService>();
			services.AddScoped<SpellAdminService>();
			services.AddScoped<ITalentPageContentReader, TalentPageContentReader>();
			services.AddScoped<ITalentPageTreeSelector, TalentPageTreeSelector>();
			services.AddScoped<ITalentPageModelFactory, TalentPageModelFactory>();
			services.AddScoped<TalentPageService>(provider =>
				new TalentPageService(
					provider.GetRequiredService<ITalentPageModelFactory>(),
					provider.GetRequiredService<ITalentPageTreeSelector>()));

			services.AddScoped<IItemsService, ItemsService>();
			services.AddScoped<ItemAdminService>();

			services.AddScoped<ICartActiveService, CartActiveService>();
			services.AddScoped<ICartArchiveQueryService, CartArchiveQueryService>();
			services.AddScoped<ICartArchiveMutationService, CartArchiveMutationService>();
			services.AddScoped<ICartService, CartService>();
			services.AddScoped<ICartSessionRequestPolicy, CartSessionRequestPolicy>();
			services.AddScoped<IAnonymousCartSessionService, AnonymousCartSessionService>();
			services.AddScoped<IPersistentCartSessionService, PersistentCartSessionService>();
			services.AddScoped<ICartSessionLifecycleService, CartSessionLifecycleService>();
			services.AddScoped<ICartSessionService, CartSessionService>();
			services.AddScoped<CartFlowService>();

			services.AddScoped<IProductCatalogQueryService, ProductCatalogQueryService>();
			services.AddScoped<IProductSearchService, ProductSearchService>();
			services.AddScoped<IProductMutationService, ProductMutationService>();
			services.AddScoped<IProductReviewService, ProductReviewService>();
			services.AddScoped<IProductService, ProductServiceAlias>();
			services.AddScoped<IProductAdminFormService, ProductAdminFormService>();
			services.AddScoped<MerchandiseService>();

			services.AddScoped<IRoleService, RoleService>();
			services.AddScoped(provider =>
			{
				AppDbContext database = provider.GetRequiredService<AppDbContext>();
				string activeConnection = database.Database.GetConnectionString()
					?? throw new InvalidOperationException(
						"Access-control database connection is unavailable.");
				return AccessControlAdminService.ForPostgres(activeConnection);
			});
			services.AddTransient<HolySectionService>();
			services.AddTransient<ProtectionSectionService>();
			services.AddTransient<RetributionSectionService>();
			services.AddScoped<PaladinContentService>();

			services.AddScoped<ITalentTreeAdminService, TalentTreeAdminService>();
			services.AddScoped<IAccountIdentityService, AccountIdentityService>();
			services.AddScoped<IAccountSecurityScorer, AccountSecurityScorer>();
			services.AddScoped<IAccountRegionService, AccountRegionService>();
			services.AddScoped<IAccountProfileService, AccountProfileService>();
			services.AddSingleton<IAccountAvatarStore, PhysicalAccountAvatarStore>();
			services.AddScoped<IAccountAvatarService, AccountAvatarService>();
			services.AddScoped<IAccountOverviewService, AccountOverviewService>();
			services.AddScoped<IAccountUiService, AccountUiService>();
			services.AddScoped<AuthSessionService>();
			services.AddScoped<AuthRegistrationService>();
			services.AddScoped<AuthLoginService>();
			services.AddHttpClient<AccountEmailService>();
			services.AddHttpClient<PaladinHubV2.Server.API.Services.EuroUsdRateService>();
			services.AddScoped<IEuroUsdRateProvider>(provider => provider.GetRequiredService<PaladinHubV2.Server.API.Services.EuroUsdRateService>());
			services.AddScoped<ISecurityService, SecurityService>();
			services.AddScoped<AccountTwoFactorService>();
			services.AddScoped<IAvatarService, AvatarService>();
			services.AddScoped<IPaymentMethodsService, PaymentMethodsService>();
			services.AddScoped<ITransactionsService, TransactionsService>();
			services.AddScoped<IWalletService, WalletService>();
			services.AddScoped<IDiscussionService, DiscussionService>();
			services.AddScoped<ISeoTargetResolver, SeoTargetResolver>();
			services.AddScoped<ISeoPublicSnapshotService, SeoPublicSnapshotService>();
			services.AddScoped<ISeoMutationLock, SeoMutationLock>();
			services.AddScoped<SeoService>();

			services.AddScoped<ICheckoutSessionService, CheckoutSessionService>();
			services.AddScoped<ICheckoutOrderService, CheckoutOrderService>();
			services.AddScoped<ICheckoutCardPaymentService, CheckoutCardPaymentService>();
			services.AddScoped<CheckoutCardFlowService>();

			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.Banners.IBannerRules,
				PaladinHubV2.Server.Domain.Services.Banners.BannerRules>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.Banners.IBannerRepository,
				PaladinHubV2.Server.Domain.Services.Banners.PostgresBannerRepository>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.Banners.BannerStoreService>();

			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameData.GameDataAssignmentService>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.ICategoryAdminQueryService,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.CategoryAdminQueryService>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.ICategoryAdminValidator,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.CategoryAdminValidator>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.ICategoryUsageGuard,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.CategoryUsageGuard>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.ICategoryRevisionJournal,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.CategoryRevisionJournal>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.CategoryAdminService>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IRarityAdminQueryService,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.RarityAdminQueryService>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IRarityAdminValidator,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.RarityAdminValidator>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IRarityUsageGuard,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.RarityUsageGuard>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IRarityRevisionJournal,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.RarityRevisionJournal>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IRarityQualitySynchronizer,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.RarityQualitySynchronizer>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.RarityAdminService>(provider =>
					new PaladinHubV2.Server.Domain.Services.GameDataAdmin.RarityAdminService(
						provider.GetRequiredService<PaladinHubV2.Server.Data.AppDbContext>(),
						provider.GetRequiredService<PaladinHubV2.Server.Domain.Services.GameData.GameDataAssignmentService>(),
						provider.GetRequiredService<PaladinHubV2.Server.Domain.Services.GameDataAdmin.IRarityAdminQueryService>(),
						provider.GetRequiredService<PaladinHubV2.Server.Domain.Services.GameDataAdmin.IRarityAdminValidator>(),
						provider.GetRequiredService<PaladinHubV2.Server.Domain.Services.GameDataAdmin.IRarityUsageGuard>(),
						provider.GetRequiredService<PaladinHubV2.Server.Domain.Services.GameDataAdmin.IRarityRevisionJournal>(),
						provider.GetRequiredService<PaladinHubV2.Server.Domain.Services.GameDataAdmin.IRarityQualitySynchronizer>()));
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IMediaBannerUsageLookup,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.MediaBannerUsageLookup>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IMediaUsageCounter,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.MediaUsageCounter>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IMediaAdminQueryService,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.MediaAdminQueryService>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IMediaAdminValidator,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.MediaAdminValidator>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IMediaRevisionJournal,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.MediaRevisionJournal>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.MediaAdminService>(provider =>
					new PaladinHubV2.Server.Domain.Services.GameDataAdmin.MediaAdminService(
						provider.GetRequiredService<PaladinHubV2.Server.Data.AppDbContext>(),
						provider.GetRequiredService<PaladinHubV2.Server.Domain.Services.GameData.GameDataAssignmentService>(),
						provider.GetRequiredService<PaladinHubV2.Server.Domain.Services.GameDataAdmin.IMediaAdminQueryService>(),
						provider.GetRequiredService<PaladinHubV2.Server.Domain.Services.GameDataAdmin.IMediaAdminValidator>(),
						provider.GetRequiredService<PaladinHubV2.Server.Domain.Services.GameDataAdmin.IMediaUsageCounter>(),
						provider.GetRequiredService<PaladinHubV2.Server.Domain.Services.GameDataAdmin.IMediaRevisionJournal>()));
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IDisciplineAdminQueryService,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.DisciplineAdminQueryService>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IDisciplineAdminValidator,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.DisciplineAdminValidator>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IDisciplineUsageGuard,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.DisciplineUsageGuard>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.IDisciplineRevisionJournal,
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.DisciplineRevisionJournal>();
			services.AddScoped<
				PaladinHubV2.Server.Domain.Services.GameDataAdmin.DisciplineAdminService>();

			services.AddScoped<ICartStore, MemoryCartStore>();
			services.AddScoped<IBlockRenderer, BlockRenderer>();
			services.AddScoped<ISpecializationTreeBuilder, HolySpecTreeBuilder>();
			services.AddScoped<ISpecializationTreeBuilder, ProtectionSpecTreeBuilder>();
			services.AddScoped<ISpecializationTreeBuilder, RetributionSpecTreeBuilder>();
			services.AddScoped<IClassTreeBuilder, PaladinClassTreeBuilder>();
			services.AddScoped<IHeroTalentTreesService, HeroTalentTreesService>();
			services.AddScoped<ITalentTreeService, TalentTreeService>();

			services.AddScoped<IPageService, PageService>();
			services.AddScoped<PageBuilderAdminService>();
			services.AddScoped<IPageManagementQueryService, PageManagementQueryService>();
			services.AddScoped<IPageManagementRequestPolicy, PageManagementRequestPolicy>();
			services.AddScoped<IPageManagementMutationService, PageManagementMutationService>();
			services.AddScoped<PageManagementService>(provider =>
				new PageManagementService(
					provider.GetRequiredService<IPageManagementQueryService>(),
					provider.GetRequiredService<IPageManagementRequestPolicy>(),
					provider.GetRequiredService<IPageManagementMutationService>()));
			services.AddScoped<TalentPageAdminService>();
			services.AddScoped<IJsonLayoutValidator, JsonLayoutValidator>();
			services.AddScoped<IDataPresetService, DataPresetService>();
			services.AddScoped<IPromoCodeService, PromoCodeService>();
			services.AddScoped<PromoCodeAdminService>();

			return services;
		}

		private static string[] GetAllowedOrigins(IConfiguration configuration)
		{
			List<string> origins = configuration
				.GetSection("Cors:AllowedOrigins")
				.GetChildren()
				.Select(item => item.Value)
				.Where(value => !string.IsNullOrWhiteSpace(value))
				.Cast<string>()
				.ToList();

			string? environmentOrigins = configuration["CORS_ALLOWED_ORIGINS"];
			if (!string.IsNullOrWhiteSpace(environmentOrigins))
			{
				origins.AddRange(
					environmentOrigins.Split(
						[',', ';'],
						StringSplitOptions.RemoveEmptyEntries |
						StringSplitOptions.TrimEntries));
			}

			string[] normalizedOrigins = origins
				.Select(origin => origin.Trim().TrimEnd('/'))
				.Where(origin => !string.IsNullOrWhiteSpace(origin))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();

			return normalizedOrigins.Length > 0
				? normalizedOrigins
				: ["http://localhost:3000", "http://127.0.0.1:3000"];
		}

		private static void ConfigureStripe(IConfiguration configuration)
		{
			string stripeMode =
				configuration["STRIPE_MODE"] ??
				configuration["Stripe:Mode"] ??
				"Test";

			bool useLiveStripe = stripeMode.Equals(
				"Live",
				StringComparison.OrdinalIgnoreCase);

			string? stripeSecretKey = useLiveStripe
				? configuration["STRIPE__SECRETKEY_LIVE"] ??
				  configuration["Stripe__SecretKey_Live"] ??
				  configuration["Stripe:SecretKey_Live"]
				: configuration["STRIPE__SECRETKEY_TEST"] ??
				  configuration["Stripe__SecretKey_Test"] ??
				  configuration["Stripe:SecretKey_Test"];

			string? stripePublishableKey = useLiveStripe
				? configuration["STRIPE__PUBLISHABLEKEY_LIVE"] ??
				  configuration["Stripe__PublishableKey_Live"] ??
				  configuration["Stripe:PublishableKey_Live"]
				: configuration["STRIPE__PUBLISHABLEKEY_TEST"] ??
				  configuration["Stripe__PublishableKey_Test"] ??
				  configuration["Stripe:PublishableKey_Test"];

			if (!string.IsNullOrWhiteSpace(stripeSecretKey))
			{
				StripeConfiguration.ApiKey = stripeSecretKey;
			}

			if (configuration is ConfigurationManager manager)
			{
				manager["Stripe:SecretKey"] = stripeSecretKey ?? string.Empty;
				manager["Stripe:PublishableKey"] = stripePublishableKey ?? string.Empty;
			}
		}

		private static bool IsApiRequest(HttpRequest request)
		{
			if (request.Path.StartsWithSegments(
					"/api",
					StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			bool acceptsJson = request.Headers["Accept"]
				.Any(value =>
					value?.Contains(
						"application/json",
						StringComparison.OrdinalIgnoreCase) == true);

			if (acceptsJson)
			{
				return true;
			}

			return string.Equals(
				request.Headers["X-Requested-With"].ToString(),
				"XMLHttpRequest",
				StringComparison.OrdinalIgnoreCase);
		}
	}
}
