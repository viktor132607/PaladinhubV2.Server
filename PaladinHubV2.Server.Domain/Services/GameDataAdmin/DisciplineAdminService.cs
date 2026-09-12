using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

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

public sealed class DisciplineAdminService
{
	private readonly AppDbContext _db;
	private readonly GameDataAssignmentService _assignments;

	public DisciplineAdminService(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_db = db;
		_assignments = assignments;
	}

	public Task<List<DisciplineListItem>> ListAsync(
		CancellationToken cancellationToken)
	{
		return _db.GameDisciplines
			.AsNoTracking()
			.OrderBy(item => item.SortOrder)
			.ThenBy(item => item.Name)
			.Select(item => new DisciplineListItem(
				item.Id,
				item.Name,
				item.Description,
				item.ParentId,
				item.SortOrder,
				item.IsArchived,
				item.IsDeleted,
				item.Version,
				_db.Spells.Count(spell => spell.DisciplineId == item.Id) +
				_db.Items.Count(product => product.DisciplineId == item.Id),
				_db.GameDisciplines.Count(child =>
					child.ParentId == item.Id && !child.IsDeleted)))
			.ToListAsync(cancellationToken);
	}

	public Task<List<DisciplineRevision>> HistoryAsync(
		int id,
		CancellationToken cancellationToken)
	{
		return _db.DisciplineRevisions
			.AsNoTracking()
			.Where(revision => revision.DisciplineId == id)
			.OrderByDescending(revision => revision.Version)
			.ToListAsync(cancellationToken);
	}

	public async Task<DisciplineAdminResult> CreateAsync(
		DisciplineRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		var discipline = new GameDiscipline();
		string? error = await ValidateAsync(
			discipline.Id,
			request,
			cancellationToken);

		if (error != null)
		{
			return new DisciplineAdminResult(
				DisciplineAdminError.Validation,
				Message: error);
		}

		Apply(discipline, request);
		_db.GameDisciplines.Add(discipline);
		await _db.SaveChangesAsync(cancellationToken);
		Record(discipline, "created", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new DisciplineAdminResult(
			DisciplineAdminError.None,
			discipline);
	}

	public async Task<DisciplineAdminResult> UpdateAsync(
		int id,
		DisciplineRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		GameDiscipline? discipline =
			await _db.GameDisciplines.SingleOrDefaultAsync(
				item => item.Id == id && !item.IsDeleted,
				cancellationToken);

		if (discipline == null)
		{
			return new DisciplineAdminResult(
				DisciplineAdminError.NotFound);
		}

		if (request.Version != discipline.Version)
		{
			return new DisciplineAdminResult(
				DisciplineAdminError.Stale);
		}

		string? error = await ValidateAsync(
			id,
			request,
			cancellationToken);

		if (error != null)
		{
			return new DisciplineAdminResult(
				DisciplineAdminError.Validation,
				Message: error);
		}

		string action =
			discipline.IsArchived == request.IsArchived
				? "updated"
				: request.IsArchived
					? "archived"
					: "unarchived";

		Apply(discipline, request);
		discipline.Version++;
		Record(discipline, action, actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new DisciplineAdminResult(
			DisciplineAdminError.None,
			discipline);
	}

	public async Task<DisciplineAdminResult> DeleteAsync(
		int id,
		int version,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		GameDiscipline? discipline =
			await _db.GameDisciplines.SingleOrDefaultAsync(
				item => item.Id == id && !item.IsDeleted,
				cancellationToken);

		if (discipline == null)
		{
			return new DisciplineAdminResult(
				DisciplineAdminError.NotFound);
		}

		if (version != discipline.Version)
		{
			return new DisciplineAdminResult(
				DisciplineAdminError.Stale);
		}

		bool inUse =
			await _db.GameDisciplines.AnyAsync(
				item => item.ParentId == id && !item.IsDeleted,
				cancellationToken) ||
			await _db.Spells.AnyAsync(
				spell => spell.DisciplineId == id,
				cancellationToken) ||
			await _db.Items.AnyAsync(
				item => item.DisciplineId == id,
				cancellationToken);

		if (inUse)
		{
			return new DisciplineAdminResult(
				DisciplineAdminError.InUse,
				Message:
					"Move specializations and assigned records before deleting this entry, or archive it.");
		}

		discipline.IsDeleted = true;
		discipline.Version++;
		Record(discipline, "deleted", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new DisciplineAdminResult(DisciplineAdminError.None);
	}

	public async Task<DisciplineAdminResult> RestoreAsync(
		int id,
		RevisionRestoreRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		GameDiscipline? discipline =
			await _db.GameDisciplines.SingleOrDefaultAsync(
				item => item.Id == id,
				cancellationToken);

		if (discipline == null)
		{
			return new DisciplineAdminResult(
				DisciplineAdminError.NotFound);
		}

		if (request.Version != discipline.Version)
		{
			return new DisciplineAdminResult(
				DisciplineAdminError.Stale);
		}

		DisciplineRevision? revision =
			await _db.DisciplineRevisions.SingleOrDefaultAsync(
				item =>
					item.Id == request.RevisionId &&
					item.DisciplineId == id,
				cancellationToken);

		if (revision == null)
		{
			return new DisciplineAdminResult(
				DisciplineAdminError.RevisionNotFound);
		}

		GameDiscipline snapshot =
			JsonSerializer.Deserialize<GameDiscipline>(
				revision.Snapshot)!;

		if (snapshot.IsDeleted)
		{
			return new DisciplineAdminResult(
				DisciplineAdminError.DeletedRevision,
				Message: "Select a revision before deletion.");
		}

		var restored = new DisciplineRequest(
			snapshot.Name,
			snapshot.Description,
			snapshot.ParentId,
			snapshot.SortOrder,
			snapshot.IsArchived,
			discipline.Version);

		string? error = await ValidateAsync(
			id,
			restored,
			cancellationToken);

		if (error != null)
		{
			return new DisciplineAdminResult(
				DisciplineAdminError.Validation,
				Message: error);
		}

		Apply(discipline, restored);
		discipline.IsDeleted = false;
		discipline.Version++;
		Record(discipline, "restored", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new DisciplineAdminResult(
			DisciplineAdminError.None,
			discipline);
	}

	private async Task<string?> ValidateAsync(
		int id,
		DisciplineRequest request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
		{
			return "Name is required.";
		}

		List<GameDiscipline> disciplines =
			await _db.GameDisciplines
				.AsNoTracking()
				.ToListAsync(cancellationToken);

		if (disciplines.Any(item =>
				item.Id != id &&
				!item.IsDeleted &&
				item.ParentId == request.ParentId &&
				string.Equals(
					item.Name,
					request.Name.Trim(),
					StringComparison.OrdinalIgnoreCase)))
		{
			return "A class or specialization with this name already exists under this class.";
		}

		if (request.ParentId is not null)
		{
			GameDiscipline? owner = disciplines.Find(item =>
				item.Id == request.ParentId && !item.IsDeleted);

			if (owner is null || owner.ParentId is not null)
			{
				return "A specialization must belong to a top-level class.";
			}

			if (disciplines.Any(item =>
					item.ParentId == id && !item.IsDeleted))
			{
				return "A class with specializations cannot become a specialization.";
			}
		}

		var seen = new HashSet<int> { id };
		int? parentId = request.ParentId;

		while (parentId is not null)
		{
			if (!seen.Add(parentId.Value))
			{
				return "A class cannot belong to itself or its specializations.";
			}

			GameDiscipline? parent = disciplines.Find(item =>
				item.Id == parentId && !item.IsDeleted);

			if (parent is null)
			{
				return "The class does not exist. Restore it first.";
			}

			if (parent.IsArchived && !request.IsArchived)
			{
				return "An active specialization cannot belong to an archived class.";
			}

			parentId = parent.ParentId;
		}

		if (request.IsArchived && disciplines.Any(item =>
				item.ParentId == id &&
				!item.IsDeleted &&
				!item.IsArchived))
		{
			return "Archive or move active specializations first.";
		}

		return null;
	}

	private static void Apply(
		GameDiscipline discipline,
		DisciplineRequest request)
	{
		discipline.Name = request.Name.Trim();
		discipline.Description = request.Description?.Trim() ?? string.Empty;
		discipline.ParentId = request.ParentId;
		discipline.SortOrder = request.SortOrder;
		discipline.IsArchived = request.IsArchived;
	}

	private void Record(
		GameDiscipline discipline,
		string action,
		string actor)
	{
		_db.DisciplineRevisions.Add(new DisciplineRevision
		{
			DisciplineId = discipline.Id,
			Version = discipline.Version,
			Action = action,
			Actor = actor,
			Snapshot = JsonSerializer.Serialize(discipline)
		});
	}
}
