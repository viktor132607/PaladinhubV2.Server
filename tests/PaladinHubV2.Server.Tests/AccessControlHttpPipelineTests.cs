using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using PaladinHub.Models.Auth;
using PaladinHubV2.Server.API.Controllers.Accounts;
using PaladinHubV2.Server.Core.Security;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Roles;

namespace PaladinHubV2.Server.Tests;

public sealed class AccessControlHttpPipelineTests
{
    private const string Password = "Pipeline#12345";
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task AuthorizationMatrixAndLiveRevocationWorkThroughRealHttpPipeline()
    {
        string? rootConnectionString = Environment.GetEnvironmentVariable("SEO_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(rootConnectionString))
        {
            return;
        }

        string databaseName = "access_http_" + Guid.NewGuid().ToString("N");
        string testConnectionString = await CreateDatabaseAsync(rootConnectionString, databaseName);

        try
        {
            var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
            using var factory = new HttpPipelineFactory(testConnectionString, clock);

            TestActors actors = await InitializeAccessControlAsync(factory.Services, testConnectionString);

            using HttpClient anonymous = CreateClient(factory);
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await anonymous.GetAsync("/Admin/api/access-control/permissions", Ct)).StatusCode);
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await anonymous.PostAsJsonAsync("/api/blocks/render", new { }, Ct)).StatusCode);

            using HttpClient ordinary = CreateClient(factory);
            await LoginAsync(ordinary, actors.OrdinaryEmail);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await ordinary.GetAsync("/Admin/api/access-control/permissions", Ct)).StatusCode);

            using HttpClient readOnly = CreateClient(factory);
            await LoginAsync(readOnly, actors.ReadOnlyEmail);
            Assert.Equal(
                HttpStatusCode.OK,
                (await readOnly.GetAsync("/Admin/api/access-control/permissions", Ct)).StatusCode);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await readOnly.GetAsync("/Admin/api/access-control/users", Ct)).StatusCode);

            using HttpClient editor = CreateClient(factory);
            await LoginAsync(editor, actors.EditorEmail);
            Assert.Equal(
                HttpStatusCode.OK,
                (await editor.GetAsync("/Admin/api/access-control/permissions", Ct)).StatusCode);
            Assert.Equal(
                HttpStatusCode.OK,
                (await editor.GetAsync("/Admin/api/access-control/users", Ct)).StatusCode);

            using HttpClient administrator = CreateClient(factory);
            await LoginAsync(administrator, actors.AdminEmail);
            Assert.Equal(
                HttpStatusCode.OK,
                (await administrator.GetAsync("/Admin/api/access-control/permissions", Ct)).StatusCode);
            Assert.Equal(
                HttpStatusCode.OK,
                (await administrator.GetAsync("/Admin/api/access-control/users", Ct)).StatusCode);

            await AssertSystemAdministratorUsesEffectivePermissionSetAsync(
                testConnectionString,
                actors.AdminRoleId);

            var accessControl = AccessControlAdminService.ForPostgres(testConnectionString);
            RoleResponse readOnlyRole = await accessControl.GetRoleAsync(actors.ReadOnlyRoleId, Ct);
            await accessControl.ReplacePermissionsAsync(
                actors.ReadOnlyRoleId,
                new ReplaceRolePermissionsRequest(Array.Empty<string>(), readOnlyRole.Version),
                TestActor(),
                Ct);

            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await readOnly.GetAsync("/Admin/api/access-control/permissions", Ct)).StatusCode);
            Assert.True(await IsAuthenticatedAsync(readOnly));

            string editorStampBefore = await GetSecurityStampAsync(factory.Services, actors.EditorUserId);
            await accessControl.RevokeUserAsync(
                actors.EditorRoleId,
                actors.EditorUserId,
                TestActor(),
                Ct);
            string editorStampAfter = await GetSecurityStampAsync(factory.Services, actors.EditorUserId);

            Assert.NotEqual(editorStampBefore, editorStampAfter);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await editor.GetAsync("/Admin/api/access-control/users", Ct)).StatusCode);
            Assert.True(await IsAuthenticatedAsync(editor));

            clock.Advance(TimeSpan.FromMinutes(31));
            Assert.False(await IsAuthenticatedAsync(editor));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabaseAsync(rootConnectionString, databaseName);
        }
    }

    private static async Task<TestActors> InitializeAccessControlAsync(
        IServiceProvider services,
        string connectionString)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AppDbContext database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await database.Database.EnsureCreatedAsync(Ct);

        RoleManager<IdentityRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        IdentityResult adminRoleResult = await roleManager.CreateAsync(
            new IdentityRole(SystemRoleCatalog.Administrator));
        Assert.True(
            adminRoleResult.Succeeded,
            string.Join("; ", adminRoleResult.Errors.Select(error => error.Description)));

        await ExecuteRolesUpgradeAsync(database);

        var accessControl = AccessControlAdminService.ForPostgres(connectionString);
        RoleResponse readOnlyRole = await accessControl.CreateRoleAsync(
            new CreateRoleRequest("HTTP Read Only", [AdminPermissions.RolePermissions.Read]),
            TestActor(),
            Ct);
        RoleResponse editorRole = await accessControl.CreateRoleAsync(
            new CreateRoleRequest(
                "HTTP Editor",
                [AdminPermissions.RolePermissions.Read, AdminPermissions.Users.Read]),
            TestActor(),
            Ct);

        IdentityRole adminRole = await roleManager.FindByNameAsync(SystemRoleCatalog.Administrator)
            ?? throw new InvalidOperationException("Admin role was not created.");

        UserManager<User> userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        User ordinary = await CreateUserAsync(userManager, "ordinary");
        User readOnly = await CreateUserAsync(userManager, "readonly");
        User editor = await CreateUserAsync(userManager, "editor");
        User admin = await CreateUserAsync(userManager, "admin");

        await accessControl.AssignUserAsync(readOnlyRole.Id, readOnly.Id, TestActor(), Ct);
        await accessControl.AssignUserAsync(editorRole.Id, editor.Id, TestActor(), Ct);
        await accessControl.AssignUserAsync(adminRole.Id, admin.Id, TestActor(), Ct);

        return new TestActors(
            ordinary.Email!,
            readOnly.Email!,
            editor.Email!,
            admin.Email!,
            readOnlyRole.Id,
            editorRole.Id,
            adminRole.Id,
            editor.Id);
    }

    private static async Task<User> CreateUserAsync(UserManager<User> userManager, string name)
    {
        string email = $"http-{name}-{Guid.NewGuid():N}@paladinhub.test";
        var user = new User
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = $"HTTP {name}"
        };

        IdentityResult result = await userManager.CreateAsync(user, Password);
        Assert.True(
            result.Succeeded,
            string.Join("; ", result.Errors.Select(error => error.Description)));
        return user;
    }

    private static HttpClient CreateClient(WebApplicationFactory<AuthApiController> factory)
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static async Task LoginAsync(HttpClient client, string identifier)
    {
        using HttpResponseMessage csrfResponse = await client.GetAsync("/api/auth/csrf", Ct);
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        using JsonDocument csrf = JsonDocument.Parse(await csrfResponse.Content.ReadAsStringAsync(Ct));
        string token = csrf.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("CSRF response did not contain a token.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest
            {
                Identifier = identifier,
                Password = Password,
                RememberMe = false
            })
        };
        request.Headers.Add("X-CSRF-TOKEN", token);

        using HttpResponseMessage response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await IsAuthenticatedAsync(client));
    }

    private static async Task<bool> IsAuthenticatedAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/auth/me", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("isAuthenticated").GetBoolean();
    }

    private static async Task AssertSystemAdministratorUsesEffectivePermissionSetAsync(
        string connectionString,
        string adminRoleId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            SELECT COUNT(*)
            FROM "RolePermissions"
            WHERE "RoleId" = @roleId;
            """, connection);
        command.Parameters.AddWithValue("roleId", adminRoleId);
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync(Ct))!);
    }

    private static async Task<string> GetSecurityStampAsync(IServiceProvider services, string userId)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        UserManager<User> userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        User user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("HTTP test user was not found.");
        return user.SecurityStamp
            ?? throw new InvalidOperationException("HTTP test user has no security stamp.");
    }

    private static async Task ExecuteRolesUpgradeAsync(AppDbContext database)
    {
        Stream stream = typeof(AuthApiController).Assembly
            .GetManifestResourceStream("DatabaseUpgrades.RolesPermissions.sql")
            ?? throw new InvalidOperationException("Embedded roles/permissions upgrade was not found.");

        await using (stream)
        using (var reader = new StreamReader(stream))
        {
            string sql = await reader.ReadToEndAsync(Ct);
            await database.Database.ExecuteSqlRawAsync(sql, Ct);
        }
    }

    private static async Task<string> CreateDatabaseAsync(string rootConnectionString, string databaseName)
    {
        var test = new NpgsqlConnectionStringBuilder(rootConnectionString)
        {
            Database = databaseName
        };

        await using var connection = new NpgsqlConnection(rootConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            $"CREATE DATABASE \"{databaseName}\";",
            connection);
        await command.ExecuteNonQueryAsync(Ct);
        return test.ConnectionString;
    }

    private static async Task DropDatabaseAsync(string rootConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(rootConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);",
            connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static AccessControlActor TestActor() =>
        new("http-test-operator", "HTTP Test Operator");

    private sealed record TestActors(
        string OrdinaryEmail,
        string ReadOnlyEmail,
        string EditorEmail,
        string AdminEmail,
        string ReadOnlyRoleId,
        string EditorRoleId,
        string AdminRoleId,
        string EditorUserId);

    private sealed class HttpPipelineFactory(
        string connectionString,
        MutableTimeProvider clock)
        : WebApplicationFactory<AuthApiController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DB_CONNECTION"] = connectionString,
                    ["APPLY_MIGRATIONS_ON_STARTUP"] = "false",
                    ["ClientApp:BaseUrl"] = "http://localhost:3000"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IDistributedCache>();
                services.AddDistributedMemoryCache();

                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(
                    options => options.UseNpgsql(connectionString));

                services.PostConfigure<CookieAuthenticationOptions>(
                    IdentityConstants.ApplicationScheme,
                    options => options.TimeProvider = clock);
            });
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
