using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetEnv;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.API.ServiceExtensions;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Seed;
using PaladinHubV2.Server.Data.Seed.Contracts;

LoadEnvironmentFile();

WebApplicationBuilder builder =
	WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(
	options =>
	{
		options.ForwardedHeaders =
			ForwardedHeaders.XForwardedFor |
			ForwardedHeaders.XForwardedProto;

		options.KnownNetworks.Clear();
		options.KnownProxies.Clear();
	});

builder.Services.AddPaladinHubApp(
	builder.Configuration,
	builder.Environment);

ConfigureHttpPort(builder);

WebApplication app = builder.Build();

app.UseForwardedHeaders();

app.UseExceptionHandler(
	errorApplication =>
	{
		errorApplication.Run(
			async context =>
			{
				IExceptionHandlerFeature? errorFeature =
					context.Features
						.Get<IExceptionHandlerFeature>();

				Exception? exception =
					errorFeature?.Error;

				ILogger logger =
					context.RequestServices
						.GetRequiredService<ILoggerFactory>()
						.CreateLogger(
							"GlobalExceptionHandler");

				logger.LogError(
					exception,
					"Unhandled exception while processing {Method} {Path}.",
					context.Request.Method,
					context.Request.Path);

				context.Response.Clear();

				await Results.Problem(
						statusCode:
							StatusCodes
								.Status500InternalServerError,
						title:
							"Internal server error",
						detail:
							app.Environment.IsDevelopment()
								? exception?.Message
								: "An unexpected server error occurred.",
						instance:
							context.Request.Path)
					.ExecuteAsync(context);
			});
	});

app.UseStatusCodePages(
	async statusContext =>
	{
		HttpContext context =
			statusContext.HttpContext;

		if (context.Response.HasStarted)
		{
			return;
		}

		int statusCode =
			context.Response.StatusCode;

		await Results.Problem(
				statusCode: statusCode,
				title:
					GetStatusTitle(statusCode),
				instance:
					context.Request.Path)
			.ExecuteAsync(context);
	});

app.UseStaticFiles();

app.UseRouting();

app.UseCors("PaladinHubClient");

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) => {
    if (context.User.Identity?.IsAuthenticated == true && context.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
        context.RequestServices.GetRequiredService<AppDbContext>().AuditActor = context.User.Identity.Name ?? "admin";
    try { await next(); }
    catch (PaladinHubV2.Server.Data.Entities.PageInUseException error) {
        context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { message = error.Message });
    }
});


app.MapControllers();

app.MapControllerRoute(
	name: "areas",
	pattern:
		"{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
	name: "default",
	pattern:
		"{controller=Home}/{action=Index}/{id?}");

app.MapGet(
	"/health",
	async (
		AppDbContext database,
		CancellationToken cancellationToken) =>
	{
		try
		{
			bool canConnect =
				await database.Database
					.CanConnectAsync(
						cancellationToken);

			if (!canConnect)
			{
				return Results.Problem(
					title:
						"Database connection failed",
					statusCode:
						StatusCodes
							.Status503ServiceUnavailable);
			}

			return Results.Ok(
				new
				{
					status = "ok",
					service =
						"PaladinHubV2.Server.API",
					database = "connected",
					utc = DateTime.UtcNow
				});
		}
		catch (Exception exception)
		{
			ILogger logger =
				app.Services
					.GetRequiredService<ILoggerFactory>()
					.CreateLogger(
						"HealthCheck");

			logger.LogWarning(
				exception,
				"Database health check failed.");

			return Results.Problem(
				title:
					"Database connection failed",
				statusCode:
					StatusCodes
						.Status503ServiceUnavailable);
		}
	})
	.AllowAnonymous();

app.MapGet(
	"/",
	() =>
		Results.Ok(
			new
			{
				service =
					"PaladinHubV2.Server.API",
				status = "running",
				health = "/health"
			}))
	.AllowAnonymous();

bool initializeDatabase =
	string.Equals(
		app.Configuration[
			"APPLY_MIGRATIONS_ON_STARTUP"],
		"true",
		StringComparison.OrdinalIgnoreCase);

if (initializeDatabase)
{
	await InitializeDatabaseAsync(app);
}

await app.RunAsync();

static void LoadEnvironmentFile()
{
	string currentDirectory =
		Directory.GetCurrentDirectory();

	string[] environmentFileCandidates =
	[
		Path.Combine(
			currentDirectory,
			".env"),

		Path.GetFullPath(
			Path.Combine(
				currentDirectory,
				"..",
				".env"))
	];

	foreach (
		string environmentFile in
		environmentFileCandidates.Distinct(
			StringComparer.OrdinalIgnoreCase))
	{
		if (!File.Exists(environmentFile))
		{
			continue;
		}

		Env.Load(environmentFile);
		break;
	}
}

static void ConfigureHttpPort(
	WebApplicationBuilder builder)
{
	string? configuredPort =
		builder.Configuration["PORT"];

	if (string.IsNullOrWhiteSpace(
			configuredPort))
	{
		return;
	}

	if (!int.TryParse(
			configuredPort,
			out int httpPort) ||
		httpPort is < 1 or > 65535)
	{
		throw new InvalidOperationException(
			"PORT must be a valid integer " +
			"between 1 and 65535. " +
			$"Current value: '{configuredPort}'.");
	}

	string? aspNetCoreUrls =
		builder.Configuration[
			"ASPNETCORE_URLS"];

	string? dotnetUrls =
		builder.Configuration[
			"DOTNET_URLS"];

	if (
		!string.IsNullOrWhiteSpace(
			aspNetCoreUrls) ||
		!string.IsNullOrWhiteSpace(
			dotnetUrls))
	{
		return;
	}

	builder.WebHost.UseUrls(
		$"http://0.0.0.0:{httpPort}");
}

static async Task InitializeDatabaseAsync(
	WebApplication application)
{
	ILogger logger =
		application.Services
			.GetRequiredService<ILoggerFactory>()
			.CreateLogger(
				"DatabaseInitialization");

	await using AsyncServiceScope scope =
		application.Services
			.CreateAsyncScope();

	AppDbContext database =
		scope.ServiceProvider
			.GetRequiredService<AppDbContext>();

	logger.LogInformation(
		"Initializing application database.");

	await database.Database
		.EnsureCreatedAsync();

	// EnsureCreated does not update an existing database. Keep this upgrade idempotent.
    await database.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "SpellIcons" (
            "Id" uuid PRIMARY KEY,
            "Name" character varying(255) NOT NULL,
            "ContentType" character varying(32) NOT NULL,
            "Content" bytea NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL
        );
        ALTER TABLE "Spells" ALTER COLUMN "Icon" TYPE character varying(2048);
        """);

    await database.Database.ExecuteSqlRawAsync("""
        DO $$
        BEGIN
            IF to_regclass('"RecordTypes"') IS NULL THEN
                CREATE TABLE "RecordTypes" ("Name" character varying(50) PRIMARY KEY);
                INSERT INTO "RecordTypes" ("Name") VALUES ('item'), ('spell'), ('talent');
                UPDATE "Spells" SET "Quality" = COALESCE(NULLIF(lower(trim("Quality")), ''), 'spell');
                INSERT INTO "RecordTypes" ("Name") SELECT DISTINCT "Quality" FROM "Spells" ON CONFLICT DO NOTHING;
            END IF;
            IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Spells_RecordTypes_Quality' AND conrelid = '"Spells"'::regclass AND confupdtype = 'c') THEN
                ALTER TABLE "Spells" DROP CONSTRAINT IF EXISTS "FK_Spells_RecordTypes_Quality";
                ALTER TABLE "Spells" ADD CONSTRAINT "FK_Spells_RecordTypes_Quality"
                    FOREIGN KEY ("Quality") REFERENCES "RecordTypes" ("Name") ON UPDATE CASCADE ON DELETE RESTRICT;
            END IF;
        END $$;
        """);

    using (var sql = new StreamReader(typeof(Program).Assembly.GetManifestResourceStream("DatabaseUpgrades.Categories.sql")!))
        await database.Database.ExecuteSqlRawAsync(await sql.ReadToEndAsync());
    using (var sql = new StreamReader(typeof(Program).Assembly.GetManifestResourceStream("DatabaseUpgrades.Classes.sql")!))
        await database.Database.ExecuteSqlRawAsync(await sql.ReadToEndAsync());
    using (var sql = new StreamReader(typeof(Program).Assembly.GetManifestResourceStream("DatabaseUpgrades.Tags.sql")!))
        await database.Database.ExecuteSqlRawAsync(await sql.ReadToEndAsync());
    using (var sql = new StreamReader(typeof(Program).Assembly.GetManifestResourceStream("DatabaseUpgrades.Patches.sql")!))
        await database.Database.ExecuteSqlRawAsync(await sql.ReadToEndAsync());
    using (var sql = new StreamReader(typeof(Program).Assembly.GetManifestResourceStream("DatabaseUpgrades.Rarities.sql")!))
        await database.Database.ExecuteSqlRawAsync(await sql.ReadToEndAsync());
    using (var sql = new StreamReader(typeof(Program).Assembly.GetManifestResourceStream("DatabaseUpgrades.Media.sql")!))
        await database.Database.ExecuteSqlRawAsync(await sql.ReadToEndAsync());
    using (var sql = new StreamReader(typeof(Program).Assembly.GetManifestResourceStream("DatabaseUpgrades.Navigation.sql")!))
        await database.Database.ExecuteSqlRawAsync(await sql.ReadToEndAsync());
    using (var sql = new StreamReader(typeof(Program).Assembly.GetManifestResourceStream("DatabaseUpgrades.Pages.sql")!))
        await database.Database.ExecuteSqlRawAsync(await sql.ReadToEndAsync());
	IEnumerable<ISeeder> seeders =
		scope.ServiceProvider
			.GetServices<ISeeder>()
			.OrderBy(GetSeederOrder);

	foreach (ISeeder seeder in seeders)
	{
		logger.LogInformation(
			"Running database seeder {SeederType}.",
			seeder.GetType().Name);

		await seeder.SeedAsync();
	}

	logger.LogInformation(
		"Application database initialization completed.");
}

static int GetSeederOrder(
	ISeeder seeder)
{
	return seeder switch
	{
		UsersSeeder => 0,
		ProductsSeeder => 1,
		SpellbookSeeder => 2,
		ItemsSeeder => 3,
		DiscussionsSeeder => 4,
		_ => 99
	};
}

static string GetStatusTitle(
	int statusCode)
{
	return statusCode switch
	{
		StatusCodes.Status400BadRequest =>
			"Bad request",

		StatusCodes.Status401Unauthorized =>
			"Authentication required",

		StatusCodes.Status403Forbidden =>
			"Access denied",

		StatusCodes.Status404NotFound =>
			"Resource not found",

		StatusCodes.Status405MethodNotAllowed =>
			"Method not allowed",

		StatusCodes.Status409Conflict =>
			"Request conflict",

		StatusCodes.Status415UnsupportedMediaType =>
			"Unsupported media type",

		StatusCodes.Status429TooManyRequests =>
			"Too many requests",

		StatusCodes.Status500InternalServerError =>
			"Internal server error",

		StatusCodes.Status503ServiceUnavailable =>
			"Service unavailable",

		_ =>
			"Request failed"
	};
}
