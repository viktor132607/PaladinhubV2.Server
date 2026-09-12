using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum PatchAdminError
{
	None,
	NotFound,
	Stale,
	Validation,
	InUse,
	RevisionNotFound,
	DeletedRevision
}

public sealed record PatchAdminResult(
	PatchAdminError Error,
	GamePatch? Patch = null,
	string? Message = null);

public sealed class PatchAdminService
{
	private readonly AppDbContext _db;
	private readonly GameDataAssignmentService _assignments;

	public PatchAdminService(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_db = db;
		_assignments = assignments;
	}

	public Task<List<PatchListItem>> ListAsync(
		CancellationToken cancellationToken)
	{
		return _db.GamePatches
			.AsNoTracking()
			.OrderBy(item => item.SortOrder)
			.ThenBy(item => item.Name)
			.Select(item => new PatchListItem(
				item.Id,
				item.Name,
				item.Description,
				null,
				item.SortOrder,
				item.IsArchived,
				item.IsDeleted,
				item.Version,
				_db.Spells.Count(spell => spell.PatchId == item.Id) +
				_db.Items.Count(product => product.PatchId == item.Id),
				0))
			.ToListAsync(cancellationToken);
	}

	public Task<List<PatchRevision>> HistoryAsync(
		int id,
		CancellationToken cancellationToken)
	{
		return _db.PatchRevisions
			.AsNoTracking()
			.Where(revision => revision.PatchId == id)
			.OrderByDescending(revision => revision.Version)
			.ToListAsync(cancellationToken);
	}

	public async Task<PatchAdminResult> CreateAsync(
		PatchRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction = await _assignments.BeginAsync(cancellationToken);

		var patch = new GamePatch();
		string? error = await ValidateAsync(patch.Id, request, cancellationToken);
		if (error != null)
		{
			return new PatchAdminResult(
				PatchAdminError.Validation,
				Message: error);
		}

		Apply(patch, request);
		_db.GamePatches.Add(patch);
		await _db.SaveChangesAsync(cancellationToken);
		Record(patch, "created", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new PatchAdminResult(PatchAdminError.None, patch);
	}

	public async Task<PatchAdminResult> UpdateAsync(
		int id,
		PatchRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction = await _assignments.BeginAsync(cancellationToken);

		GamePatch? patch = await _db.GamePatches.SingleOrDefaultAsync(
			item => item.Id == id && !item.IsDeleted,
			cancellationToken);

		if (patch == null)
		{
			return new PatchAdminResult(PatchAdminError.NotFound);
		}

		if (request.Version != patch.Version)
		{
			return new PatchAdminResult(PatchAdminError.Stale);
		}

		string? error = await ValidateAsync(id, request, cancellationToken);
		if (error != null)
		{
			return new PatchAdminResult(
				PatchAdminError.Validation,
				Message: error);
		}

		string action = patch.IsArchived == request.IsArchived
			? "updated"
			: request.IsArchived ? "archived" : "unarchived";

		Apply(patch, request);
		patch.Version++;
		Record(patch, action, actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new PatchAdminResult(PatchAdminError.None, patch);
	}

	public async Task<PatchAdminResult> DeleteAsync(
		int id,
		int version,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction = await _assignments.BeginAsync(cancellationToken);

		GamePatch? patch = await _db.GamePatches.SingleOrDefaultAsync(
			item => item.Id == id && !item.IsDeleted,
			cancellationToken);

		if (patch == null)
		{
			return new PatchAdminResult(PatchAdminError.NotFound);
		}

		if (version != patch.Version)
		{
			return new PatchAdminResult(PatchAdminError.Stale);
		}

		bool inUse =
			await _db.Spells.AnyAsync(spell => spell.PatchId == id, cancellationToken) ||
			await _db.Items.AnyAsync(item => item.PatchId == id, cancellationToken);

		if (inUse)
		{
			return new PatchAdminResult(
				PatchAdminError.InUse,
				Message:
					"Remove this patch from assigned records before deleting it, or archive it.");
		}

		patch.IsDeleted = true;
		patch.Version++;
		Record(patch, "deleted", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new PatchAdminResult(PatchAdminError.None);
	}

	public async Task<PatchAdminResult> RestoreAsync(
		int id,
		RevisionRestoreRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction = await _assignments.BeginAsync(cancellationToken);

		GamePatch? patch = await _db.GamePatches.SingleOrDefaultAsync(
			item => item.Id == id,
			cancellationToken);

		if (patch == null)
		{
			return new PatchAdminResult(PatchAdminError.NotFound);
		}

		if (request.Version != patch.Version)
		{
			return new PatchAdminResult(PatchAdminError.Stale);
		}

		PatchRevision? revision = await _db.PatchRevisions.SingleOrDefaultAsync(
			item => item.Id == request.RevisionId && item.PatchId == id,
			cancellationToken);

		if (revision == null)
		{
			return new PatchAdminResult(PatchAdminError.RevisionNotFound);
		}

		GamePatch snapshot = JsonSerializer.Deserialize<GamePatch>(revision.Snapshot)!;
		if (snapshot.IsDeleted)
		{
			return new PatchAdminResult(
				PatchAdminError.DeletedRevision,
				Message: "Select a revision before deletion.");
		}

		var restored = new PatchRequest(
			snapshot.Name,
			snapshot.Description,
			snapshot.SortOrder,
			snapshot.IsArchived,
			patch.Version);

		string? error = await ValidateAsync(id, restored, cancellationToken);
		if (error != null)
		{
			return new PatchAdminResult(
				PatchAdminError.Validation,
				Message: error);
		}

		Apply(patch, restored);
		patch.IsDeleted = false;
		patch.Version++;
		Record(patch, "restored", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new PatchAdminResult(PatchAdminError.None, patch);
	}

	private async Task<string?> ValidateAsync(
		int id,
		PatchRequest request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
		{
			return "Name is required.";
		}

		string name = request.Name.Trim().ToLowerInvariant();
		bool exists = await _db.GamePatches.AnyAsync(
			item =>
				item.Id != id &&
				!item.IsDeleted &&
				item.Name.ToLower() == name,
			cancellationToken);

		return exists
			? "A patch with this name already exists."
			: null;
	}

	private static void Apply(GamePatch patch, PatchRequest request)
	{
		patch.Name = request.Name.Trim();
		patch.Description = request.Description?.Trim() ?? string.Empty;
		patch.SortOrder = request.SortOrder;
		patch.IsArchived = request.IsArchived;
	}

	private void Record(GamePatch patch, string action, string actor)
	{
		_db.PatchRevisions.Add(new PatchRevision
		{
			PatchId = patch.Id,
			Version = patch.Version,
			Action = action,
			Actor = actor,
			Snapshot = JsonSerializer.Serialize(patch)
		});
	}
}
