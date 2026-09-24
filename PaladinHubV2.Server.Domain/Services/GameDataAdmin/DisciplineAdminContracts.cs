using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum DisciplineAdminError
{
    None,
    NotFound,
    Stale,
    Validation,
    InUse,
    RevisionNotFound,
    DeletedRevision
}

public sealed record DisciplineAdminResult(
    DisciplineAdminError Error,
    GameDiscipline? Discipline = null,
    string? Message = null);

public interface IDisciplineAdminQueryService
{
    Task<List<DisciplineListItem>> ListAsync(
        CancellationToken cancellationToken);

    Task<List<DisciplineRevision>> HistoryAsync(
        int id,
        CancellationToken cancellationToken);
}

public interface IDisciplineAdminValidator
{
    Task<string?> ValidateAsync(
        int id,
        DisciplineRequest request,
        CancellationToken cancellationToken);
}

public interface IDisciplineUsageGuard
{
    Task<bool> IsInUseAsync(
        int id,
        CancellationToken cancellationToken);
}

public interface IDisciplineRevisionJournal
{
    void Record(
        GameDiscipline discipline,
        string action,
        string actor);

    GameDiscipline ReadSnapshot(
        DisciplineRevision revision);
}
