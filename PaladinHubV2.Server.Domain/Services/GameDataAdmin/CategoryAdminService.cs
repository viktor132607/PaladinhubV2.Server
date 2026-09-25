using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class CategoryAdminService
{
    private readonly AppDbContext _db;
    private readonly GameDataAssignmentService _assignments;
    private readonly ICategoryAdminQueryService _queries;
    private readonly ICategoryAdminValidator _validator;
    private readonly ICategoryUsageGuard _usage;
    private readonly ICategoryRevisionJournal _journal;

    public CategoryAdminService(
        AppDbContext db,
        GameDataAssignmentService assignments)
        : this(
            db,
            assignments,
            new CategoryAdminQueryService(db),
            new CategoryAdminValidator(db),
            new CategoryUsageGuard(db),
            new CategoryRevisionJournal(db))
    {
    }

    public CategoryAdminService(
        AppDbContext db,
        GameDataAssignmentService assignments,
        ICategoryAdminQueryService queries,
        ICategoryAdminValidator validator,
        ICategoryUsageGuard usage,
        ICategoryRevisionJournal journal)
    {
        _db = db;
        _assignments = assignments;
        _queries = queries;
        _validator = validator;
        _usage = usage;
        _journal = journal;
    }

    public Task<List<CategoryListItem>> ListAsync(
        CancellationToken cancellationToken) =>
        _queries.ListAsync(cancellationToken);

    public Task<List<CategoryRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken) =>
        _queries.HistoryAsync(id, cancellationToken);

    public async Task<CategoryAdminResult> CreateAsync(
        CategoryRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(cancellationToken);

        var category = new Category();

        string? error = await _validator.ValidateAsync(
            category.Id,
            request,
            cancellationToken);

        if (error is not null)
        {
            return Validation(error);
        }

        Apply(category, request);

        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        _journal.Record(category, "created", actor);
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Success(category);
    }

    public async Task<CategoryAdminResult> UpdateAsync(
        int id,
        CategoryRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(cancellationToken);

        Category? category =
            await _db.Categories.SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    !item.IsDeleted,
                cancellationToken);

        if (category is null)
        {
            return Error(CategoryAdminError.NotFound);
        }

        if (request.Version != category.Version)
        {
            return Error(CategoryAdminError.Stale);
        }

        string? error = await _validator.ValidateAsync(
            id,
            request,
            cancellationToken);

        if (error is not null)
        {
            return Validation(error);
        }

        string action = ResolveUpdateAction(
            category.IsArchived,
            request.IsArchived);

        Apply(category, request);
        category.Version++;

        _journal.Record(category, action, actor);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(category);
    }

    public async Task<CategoryAdminResult> DeleteAsync(
        int id,
        int version,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(cancellationToken);

        Category? category =
            await _db.Categories.SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    !item.IsDeleted,
                cancellationToken);

        if (category is null)
        {
            return Error(CategoryAdminError.NotFound);
        }

        if (version != category.Version)
        {
            return Error(CategoryAdminError.Stale);
        }

        if (await _usage.IsInUseAsync(
                id,
                cancellationToken))
        {
            return new CategoryAdminResult(
                CategoryAdminError.InUse,
                Message:
                    "Move the subcategories and assigned records before deleting this category, or archive it.");
        }

        category.IsDeleted = true;
        category.Version++;

        _journal.Record(category, "deleted", actor);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Error(CategoryAdminError.None);
    }

    public async Task<CategoryAdminResult> RestoreAsync(
        int id,
        RevisionRestoreRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _assignments.BeginAsync(cancellationToken);

        Category? category =
            await _db.Categories.SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (category is null)
        {
            return Error(CategoryAdminError.NotFound);
        }

        if (request.Version != category.Version)
        {
            return Error(CategoryAdminError.Stale);
        }

        CategoryRevision? revision =
            await _db.CategoryRevisions.SingleOrDefaultAsync(
                item =>
                    item.Id == request.RevisionId &&
                    item.CategoryId == id,
                cancellationToken);

        if (revision is null)
        {
            return Error(
                CategoryAdminError.RevisionNotFound);
        }

        Category snapshot =
            _journal.ReadSnapshot(revision);

        if (snapshot.IsDeleted)
        {
            return new CategoryAdminResult(
                CategoryAdminError.DeletedRevision,
                Message:
                    "Select a revision before deletion.");
        }

        CategoryRequest restored =
            BuildRestoreRequest(
                snapshot,
                category.Version);

        string? error = await _validator.ValidateAsync(
            id,
            restored,
            cancellationToken);

        if (error is not null)
        {
            return Validation(error);
        }

        Apply(category, restored);
        category.IsDeleted = false;
        category.Version++;

        _journal.Record(category, "restored", actor);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(category);
    }

    internal static string ResolveUpdateAction(
        bool wasArchived,
        bool isArchived)
    {
        if (wasArchived == isArchived)
        {
            return "updated";
        }

        return isArchived
            ? "archived"
            : "unarchived";
    }

    internal static CategoryRequest BuildRestoreRequest(
        Category snapshot,
        int currentVersion)
    {
        return new CategoryRequest(
            snapshot.Name,
            snapshot.Description,
            snapshot.ParentId,
            snapshot.SortOrder,
            snapshot.IsArchived,
            currentVersion);
    }

    internal static void Apply(
        Category category,
        CategoryRequest request)
    {
        category.Name = request.Name.Trim();
        category.Description =
            request.Description?.Trim() ??
            string.Empty;
        category.ParentId = request.ParentId;
        category.SortOrder = request.SortOrder;
        category.IsArchived = request.IsArchived;
    }

    private static CategoryAdminResult Success(
        Category category) =>
        new(
            CategoryAdminError.None,
            category);

    private static CategoryAdminResult Validation(
        string message) =>
        new(
            CategoryAdminError.Validation,
            Message: message);

    private static CategoryAdminResult Error(
        CategoryAdminError error) =>
        new(error);
}
