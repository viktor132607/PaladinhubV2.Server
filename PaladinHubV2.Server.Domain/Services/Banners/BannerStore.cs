using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.Banners;

public static class BannerStore
{
    private static readonly BannerRules Rules =
        new();

    public static string NormalizePath(
        string? path) =>
        Rules.NormalizePath(path);

    public static string? Validate(
        BannerRequest request,
        bool creating) =>
        Rules.Validate(
            request,
            creating);

    public static bool IsVisible(
        BannerDto banner,
        string path,
        DateTimeOffset now) =>
        Rules.IsVisible(
            banner,
            path,
            now);

    public static Task<List<BannerDto>> ReadAllAsync(
        AppDbContext db,
        CancellationToken cancellationToken) =>
        new PostgresBannerRepository(db)
            .ReadAllAsync(
                cancellationToken);

    public static Task<List<BannerRevisionDto>> HistoryAsync(
        AppDbContext db,
        Guid id,
        CancellationToken cancellationToken) =>
        new PostgresBannerRepository(db)
            .HistoryAsync(
                id,
                cancellationToken);

    public static Task<BannerStoreResult> CreateAsync(
        AppDbContext db,
        BannerRequest request,
        string actor,
        CancellationToken cancellationToken) =>
        CreateService(db).CreateAsync(
            request,
            actor,
            cancellationToken);

    public static Task<BannerStoreResult> UpdateAsync(
        AppDbContext db,
        Guid id,
        BannerRequest request,
        string actor,
        CancellationToken cancellationToken) =>
        CreateService(db).UpdateAsync(
            id,
            request,
            actor,
            cancellationToken);

    public static Task<BannerStoreResult> ChangeAsync(
        AppDbContext db,
        Guid id,
        BannerActionRequest request,
        string actor,
        CancellationToken cancellationToken) =>
        CreateService(db).ChangeAsync(
            id,
            request,
            actor,
            cancellationToken);

    private static BannerStoreService CreateService(
        AppDbContext db) =>
        new(
            new PostgresBannerRepository(db),
            Rules,
            TimeProvider.System);
}
