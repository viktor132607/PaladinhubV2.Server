using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Banners;

namespace PaladinHubV2.Server.Tests;

public sealed class BannerStorePostgresCoverageTests
{
    private static readonly CancellationToken Ct =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task RepositoryAndCompatibilityFacadeCoverPostgresPersistencePaths()
    {
        string? baseConnectionString =
            Environment.GetEnvironmentVariable(
                "SEO_POSTGRES_CONNECTION");

        if (string.IsNullOrWhiteSpace(
                baseConnectionString))
        {
            Assert.Skip(
                "Set SEO_POSTGRES_CONNECTION to run banner PostgreSQL coverage tests.");
            return;
        }

        string schema =
            "banner_refactor_" +
            Guid.NewGuid().ToString("N");

        await using var setup =
            new NpgsqlConnection(
                baseConnectionString);

        await setup.OpenAsync(Ct);

        try
        {
            await ExecuteAsync(
                setup,
                $"""
                CREATE SCHEMA "{schema}";
                SET search_path TO "{schema}";

                CREATE TABLE "SpellIcons"(
                    "Id" uuid PRIMARY KEY,
                    "IsArchived" boolean NOT NULL DEFAULT false,
                    "IsDeleted" boolean NOT NULL DEFAULT false
                );

                CREATE TABLE "SiteBanners"(
                    "Id" uuid PRIMARY KEY,
                    "InternalName" varchar(100) NOT NULL,
                    "Title" varchar(200) NOT NULL,
                    "Text" text NOT NULL,
                    "ImageUrl" varchar(2048),
                    "AltText" varchar(300) NOT NULL DEFAULT '',
                    "ButtonText" varchar(120),
                    "ButtonUrl" varchar(2048),
                    "Kind" varchar(16) NOT NULL,
                    "Position" varchar(32) NOT NULL,
                    "PagesJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
                    "StartAtUtc" timestamptz,
                    "EndAtUtc" timestamptz,
                    "SortOrder" integer NOT NULL DEFAULT 0,
                    "IsDismissible" boolean NOT NULL DEFAULT true,
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "IsArchived" boolean NOT NULL DEFAULT false,
                    "IsDeleted" boolean NOT NULL DEFAULT false,
                    "Version" integer NOT NULL DEFAULT 1,
                    "CreatedAtUtc" timestamptz NOT NULL,
                    "UpdatedAtUtc" timestamptz NOT NULL
                );

                CREATE UNIQUE INDEX "IX_SiteBanners_InternalName"
                    ON "SiteBanners"(lower("InternalName"))
                    WHERE NOT "IsDeleted";

                CREATE TABLE "BannerRevisions"(
                    "Id" uuid PRIMARY KEY,
                    "BannerId" uuid NOT NULL
                        REFERENCES "SiteBanners"("Id")
                        ON DELETE RESTRICT,
                    "Version" integer NOT NULL,
                    "Action" varchar(32) NOT NULL,
                    "Actor" varchar(256) NOT NULL,
                    "CreatedAtUtc" timestamptz NOT NULL,
                    "Snapshot" jsonb NOT NULL,
                    CONSTRAINT "UQ_BannerRevisions_Banner_Version"
                        UNIQUE("BannerId","Version")
                );
                """);

            var builder =
                new NpgsqlConnectionStringBuilder(
                    baseConnectionString)
                {
                    SearchPath = schema
                };

            var options =
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseNpgsql(
                        builder.ConnectionString)
                    .Options;

            await using var db =
                new AppDbContext(options);

            var repository =
                new PostgresBannerRepository(db);

            Assert.Empty(
                await repository.ReadAllAsync(Ct));

            Assert.Null(
                await repository.FindAsync(
                    Guid.NewGuid(),
                    Ct));

            Assert.Null(
                await repository.RevisionSnapshotAsync(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Ct));

            DateTimeOffset now =
                new(
                    2026,
                    9,
                    25,
                    12,
                    0,
                    0,
                    TimeSpan.Zero);

            BannerDto first =
                Banner(
                    "notice",
                    null,
                    now);

            BannerStoreResult created =
                await repository.CreateAsync(
                    first,
                    "creator",
                    Ct);

            Assert.Equal(200, created.Status);

            List<BannerDto> all =
                await repository.ReadAllAsync(Ct);

            BannerDto stored =
                Assert.Single(all);

            Assert.Equal(first.Id, stored.Id);
            Assert.Null(stored.ImageUrl);
            Assert.Null(stored.ButtonText);
            Assert.Null(stored.ButtonUrl);
            Assert.Null(stored.StartAtUtc);
            Assert.Null(stored.EndAtUtc);

            BannerDto? found =
                await repository.FindAsync(
                    first.Id,
                    Ct);

            Assert.NotNull(found);

            List<BannerRevisionDto> history =
                await repository.HistoryAsync(
                    first.Id,
                    Ct);

            BannerRevisionDto createdRevision =
                Assert.Single(history);

            Assert.Equal("created", createdRevision.Action);
            Assert.Equal("creator", createdRevision.Actor);

            BannerDto? snapshot =
                await repository.RevisionSnapshotAsync(
                    first.Id,
                    createdRevision.Id,
                    Ct);

            Assert.NotNull(snapshot);
            Assert.Equal("notice", snapshot!.InternalName);

            BannerStoreResult duplicateCreate =
                await repository.CreateAsync(
                    Banner(
                        "NOTICE",
                        null,
                        now.AddSeconds(1)),
                    "creator",
                    Ct);

            Assert.Equal("banner.duplicate", duplicateCreate.Code);

            BannerDto plainImage =
                Banner(
                    "plain-image",
                    "/assets/banner.png",
                    now.AddSeconds(2));

            Assert.Equal(
                200,
                (await repository.CreateAsync(
                    plainImage,
                    "creator",
                    Ct)).Status);

            Guid missingMedia = Guid.NewGuid();

            BannerStoreResult unavailableCreate =
                await repository.CreateAsync(
                    Banner(
                        "missing-media",
                        "/api/spell-icons/" +
                        missingMedia,
                        now.AddSeconds(3)),
                    "creator",
                    Ct);

            Assert.Equal(
                "banner.mediaUnavailable",
                unavailableCreate.Code);

            Guid mediaId = Guid.NewGuid();

            await ExecuteAsync(
                setup,
                $"""
                SET search_path TO "{schema}";
                INSERT INTO "SpellIcons"(
                    "Id",
                    "IsArchived",
                    "IsDeleted")
                VALUES(
                    '{mediaId}',
                    false,
                    false);
                """);

            BannerDto mediaBanner =
                Banner(
                    "media",
                    "/api/spell-icons/" +
                    mediaId,
                    now.AddSeconds(4)) with
                {
                    AltText = "Media alt",
                    ButtonText = "Open",
                    ButtonUrl = "/details",
                    Pages = ["/holy"],
                    StartAtUtc = now,
                    EndAtUtc = now.AddHours(1)
                };

            Assert.Equal(
                200,
                (await repository.CreateAsync(
                    mediaBanner,
                    "creator",
                    Ct)).Status);

            BannerDto current =
                (await repository.FindAsync(
                    first.Id,
                    Ct))!;

            BannerDto next =
                current with
                {
                    Title = "Updated",
                    Version =
                        current.Version + 1,
                    UpdatedAtUtc =
                        now.AddMinutes(1)
                };

            BannerStoreResult replaced =
                await repository.ReplaceAsync(
                    current,
                    next,
                    "updated",
                    "editor",
                    Ct);

            Assert.Equal(200, replaced.Status);

            BannerStoreResult stale =
                await repository.ReplaceAsync(
                    current,
                    next,
                    "updated",
                    "editor",
                    Ct);

            Assert.Equal(
                "banner.stale",
                stale.Code);

            BannerDto plainCurrent =
                (await repository.FindAsync(
                    plainImage.Id,
                    Ct))!;

            BannerDto duplicateNext =
                plainCurrent with
                {
                    InternalName = "notice",
                    Version =
                        plainCurrent.Version + 1,
                    UpdatedAtUtc =
                        now.AddMinutes(2)
                };

            BannerStoreResult duplicateReplace =
                await repository.ReplaceAsync(
                    plainCurrent,
                    duplicateNext,
                    "updated",
                    "editor",
                    Ct);

            Assert.Equal(
                "banner.duplicate",
                duplicateReplace.Code);

            BannerDto mediaCurrent =
                (await repository.FindAsync(
                    mediaBanner.Id,
                    Ct))!;

            BannerDto missingMediaNext =
                mediaCurrent with
                {
                    ImageUrl =
                        "/api/spell-icons/" +
                        Guid.NewGuid(),
                    Version =
                        mediaCurrent.Version + 1,
                    UpdatedAtUtc =
                        now.AddMinutes(3)
                };

            BannerStoreResult unavailableReplace =
                await repository.ReplaceAsync(
                    mediaCurrent,
                    missingMediaNext,
                    "updated",
                    "editor",
                    Ct);

            Assert.Equal(
                "banner.mediaUnavailable",
                unavailableReplace.Code);

            BannerDto sameMediaNext =
                mediaCurrent with
                {
                    Title = "Same image",
                    Version =
                        mediaCurrent.Version + 1,
                    UpdatedAtUtc =
                        now.AddMinutes(4)
                };

            Assert.Equal(
                200,
                (await repository.ReplaceAsync(
                    mediaCurrent,
                    sameMediaNext,
                    "updated",
                    "editor",
                    Ct)).Status);

            plainCurrent =
                (await repository.FindAsync(
                    plainImage.Id,
                    Ct))!;

            BannerDto deleteNext =
                plainCurrent with
                {
                    IsDeleted = true,
                    IsActive = false,
                    Version =
                        plainCurrent.Version + 1,
                    UpdatedAtUtc =
                        now.AddMinutes(5)
                };

            Assert.Equal(
                200,
                (await repository.ReplaceAsync(
                    plainCurrent,
                    deleteNext,
                    "deleted",
                    "editor",
                    Ct)).Status);

            BannerRequest compatibilityRequest =
                Request("compat");

            BannerStoreResult compatibilityCreated =
                await BannerStore.CreateAsync(
                    db,
                    compatibilityRequest,
                    "compat-creator",
                    Ct);

            Assert.Equal(
                200,
                compatibilityCreated.Status);

            Guid compatibilityId =
                compatibilityCreated.Banner!.Id;

            Assert.NotEmpty(
                await BannerStore.ReadAllAsync(
                    db,
                    Ct));

            Assert.NotEmpty(
                await BannerStore.HistoryAsync(
                    db,
                    compatibilityId,
                    Ct));

            BannerStoreResult compatibilityUpdated =
                await BannerStore.UpdateAsync(
                    db,
                    compatibilityId,
                    compatibilityRequest with
                    {
                        Title = "Updated compatibility",
                        Version = 1
                    },
                    "compat-editor",
                    Ct);

            Assert.Equal(
                200,
                compatibilityUpdated.Status);

            BannerStoreResult compatibilityChanged =
                await BannerStore.ChangeAsync(
                    db,
                    compatibilityId,
                    new BannerActionRequest(
                        2,
                        "archive",
                        null),
                    "compat-editor",
                    Ct);

            Assert.Equal(
                200,
                compatibilityChanged.Status);
            Assert.True(
                compatibilityChanged.Banner!.IsArchived);
        }
        finally
        {
            await using var cleanup =
                new NpgsqlCommand(
                    $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;",
                    setup);

            await cleanup.ExecuteNonQueryAsync(Ct);
        }
    }

    private static BannerDto Banner(
        string name,
        string? imageUrl,
        DateTimeOffset now)
    {
        return new BannerDto(
            Guid.NewGuid(),
            name,
            name + " title",
            name + " text",
            imageUrl,
            "",
            null,
            null,
            "information",
            "above-content",
            [],
            null,
            null,
            0,
            true,
            true,
            false,
            false,
            1,
            now,
            now);
    }

    private static BannerRequest Request(
        string name)
    {
        return new BannerRequest(
            name,
            "Title",
            "Body",
            null,
            null,
            null,
            null,
            "information",
            "above-content",
            [],
            null,
            null,
            0,
            true,
            true,
            1);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql)
    {
        await using var command =
            new NpgsqlCommand(
                sql,
                connection);

        await command.ExecuteNonQueryAsync(Ct);
    }
}
