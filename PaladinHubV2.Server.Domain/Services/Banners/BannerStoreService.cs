namespace PaladinHubV2.Server.Domain.Services.Banners;

public sealed class BannerStoreService
{
    private readonly IBannerRepository _repository;
    private readonly IBannerRules _rules;
    private readonly TimeProvider _timeProvider;

    public BannerStoreService(
        IBannerRepository repository,
        IBannerRules rules,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _rules = rules;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<BannerDto>> ListAsync(
        string? search,
        string status,
        CancellationToken cancellationToken)
    {
        IEnumerable<BannerDto> items =
            await _repository.ReadAllAsync(
                cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string text = search.Trim();

            items = items.Where(item =>
                item.InternalName.Contains(
                    text,
                    StringComparison.OrdinalIgnoreCase) ||
                item.Title.Contains(
                    text,
                    StringComparison.OrdinalIgnoreCase) ||
                item.Text.Contains(
                    text,
                    StringComparison.OrdinalIgnoreCase));
        }

        items = (status ?? string.Empty)
            .ToLowerInvariant() switch
        {
            "all" => items,
            "archived" =>
                items.Where(item =>
                    item.IsArchived &&
                    !item.IsDeleted),
            "deleted" =>
                items.Where(item =>
                    item.IsDeleted),
            "inactive" =>
                items.Where(item =>
                    !item.IsDeleted &&
                    !item.IsArchived &&
                    !item.IsActive),
            _ =>
                items.Where(item =>
                    !item.IsDeleted &&
                    !item.IsArchived &&
                    item.IsActive)
        };

        return items
            .OrderBy(item => item.Position)
            .ThenBy(item => item.SortOrder)
            .ThenBy(item => item.InternalName)
            .ToArray();
    }

    public Task<List<BannerRevisionDto>> HistoryAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        _repository.HistoryAsync(
            id,
            cancellationToken);

    public async Task<BannerStoreResult> CreateAsync(
        BannerRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        string? invalid =
            _rules.Validate(
                request,
                true);

        if (invalid is not null)
        {
            return Validation(invalid);
        }

        BannerDto banner =
            _rules.Create(
                request,
                _timeProvider.GetUtcNow());

        return await _repository.CreateAsync(
            banner,
            actor,
            cancellationToken);
    }

    public async Task<BannerStoreResult> UpdateAsync(
        Guid id,
        BannerRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        string? invalid =
            _rules.Validate(
                request,
                false);

        if (invalid is not null)
        {
            return Validation(invalid);
        }

        BannerDto? current =
            await _repository.FindAsync(
                id,
                cancellationToken);

        if (current is null ||
            current.IsDeleted)
        {
            return new BannerStoreResult(
                404,
                "banner.notFound",
                "Banner not found.");
        }

        if (current.IsArchived)
        {
            return new BannerStoreResult(
                409,
                "banner.archived",
                "Unarchive before editing.");
        }

        if (current.Version != request.Version)
        {
            return new BannerStoreResult(
                409,
                "banner.stale",
                "The banner was changed by another user. Reload before saving.");
        }

        BannerDto next =
            _rules.Update(
                current,
                request,
                _timeProvider.GetUtcNow());

        return await _repository.ReplaceAsync(
            current,
            next,
            "updated",
            actor,
            cancellationToken);
    }

    public async Task<BannerStoreResult> ChangeAsync(
        Guid id,
        BannerActionRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        BannerDto? current =
            await _repository.FindAsync(
                id,
                cancellationToken);

        if (current is null)
        {
            return new BannerStoreResult(
                404,
                "banner.notFound",
                "Banner not found.");
        }

        if (current.Version != request.Version)
        {
            return new BannerStoreResult(
                409,
                "banner.stale",
                "The banner was changed by another user. Reload before continuing.");
        }

        string action =
            (request.Action ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

        if (current.IsDeleted &&
            action != "restore")
        {
            return new BannerStoreResult(
                409,
                "banner.deleted",
                "Restore a revision before deletion first.");
        }

        DateTimeOffset now =
            _timeProvider.GetUtcNow();

        BannerDto next;
        string revisionAction;

        switch (action)
        {
            case "archive":
                next = current with
                {
                    IsArchived = true,
                    Version = current.Version + 1,
                    UpdatedAtUtc = now
                };
                revisionAction = "archived";
                break;

            case "unarchive":
                next = current with
                {
                    IsArchived = false,
                    IsDeleted = false,
                    Version = current.Version + 1,
                    UpdatedAtUtc = now
                };
                revisionAction = "unarchived";
                break;

            case "delete":
                next = current with
                {
                    IsDeleted = true,
                    IsActive = false,
                    Version = current.Version + 1,
                    UpdatedAtUtc = now
                };
                revisionAction = "deleted";
                break;

            case "restore":
                if (request.RevisionId is null)
                {
                    return new BannerStoreResult(
                        400,
                        "banner.revisionRequired",
                        "Revision is required.");
                }

                BannerDto? snapshot =
                    await _repository.RevisionSnapshotAsync(
                        id,
                        request.RevisionId.Value,
                        cancellationToken);

                if (snapshot is null)
                {
                    return new BannerStoreResult(
                        404,
                        "banner.revisionNotFound",
                        "Revision not found.");
                }

                if (snapshot.IsDeleted)
                {
                    return new BannerStoreResult(
                        400,
                        "banner.deletedRevision",
                        "Select a revision before deletion.");
                }

                string? invalid =
                    _rules.Validate(
                        _rules.ToRequest(
                            snapshot,
                            current.Version),
                        false);

                if (invalid is not null)
                {
                    return new BannerStoreResult(
                        400,
                        "banner.restoreInvalid",
                        invalid);
                }

                next = snapshot with
                {
                    Version = current.Version + 1,
                    IsDeleted = false,
                    UpdatedAtUtc = now
                };
                revisionAction = "restored";
                break;

            default:
                return new BannerStoreResult(
                    400,
                    "banner.actionInvalid",
                    "Unsupported banner action.");
        }

        return await _repository.ReplaceAsync(
            current,
            next,
            revisionAction,
            actor,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<BannerDto> Items, DateTimeOffset? NextChangeAtUtc)>
        VisibleAsync(
            string path,
            CancellationToken cancellationToken)
    {
        path = _rules.NormalizePath(path);

        DateTimeOffset now =
            _timeProvider.GetUtcNow();

        List<BannerDto> scoped =
            (await _repository.ReadAllAsync(
                cancellationToken))
            .Where(item =>
                !item.IsDeleted &&
                !item.IsArchived &&
                item.IsActive)
            .Where(item =>
                item.Pages.Count == 0 ||
                item.Pages.Any(page =>
                    string.Equals(
                        _rules.NormalizePath(page),
                        path,
                        StringComparison.OrdinalIgnoreCase)))
            .ToList();

        BannerDto[] visible =
            scoped
                .Where(item =>
                    _rules.IsVisible(
                        item,
                        path,
                        now))
                .OrderBy(item => item.Position)
                .ThenBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .ToArray();

        DateTimeOffset? next =
            scoped
                .SelectMany(item =>
                    new[]
                    {
                        item.StartAtUtc,
                        item.EndAtUtc
                    })
                .Where(value =>
                    value.HasValue &&
                    value.Value > now)
                .Select(value =>
                    value!.Value)
                .OrderBy(value => value)
                .Cast<DateTimeOffset?>()
                .FirstOrDefault();

        return (visible, next);
    }

    private static BannerStoreResult Validation(
        string message) =>
        new(
            400,
            "banner.validation",
            message);
}
