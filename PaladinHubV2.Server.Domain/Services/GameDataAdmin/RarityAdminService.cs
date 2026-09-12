using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum RarityAdminError
{
	None,
	NotFound,
	Stale,
	Validation,
	InUse,
	RevisionNotFound,
	DeletedRevision
}

public sealed record RarityAdminResult(
	RarityAdminError Error,
	ItemRarity? Rarity = null,
	string? Message = null);

public sealed class RarityAdminService
{
	private readonly AppDbContext _db;
	private readonly GameDataAssignmentService _assignments;

	public RarityAdminService(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_db = db;
		_assignments = assignments;
	}

	public Task<List<RarityListItem>> ListAsync(
		CancellationToken cancellationToken)
	{
		return _db.ItemRarities
			.AsNoTracking()
			.OrderBy(item => item.SortOrder)
			.ThenBy(item => item.Name)
			.Select(item => new RarityListItem(
				item.Id,
				item.Name,
				item.Description,
				item.Color,
				null,
				item.SortOrder,
				item.IsArchived,
				item.IsDeleted,
				item.Version,
				_db.Items.Count(product => product.RarityId == item.Id),
				0))
			.ToListAsync(cancellationToken);
	}

	public Task<List<RarityRevision>> HistoryAsync(
		int id,
		CancellationToken cancellationToken)
	{
		return _db.RarityRevisions
			.AsNoTracking()
			.Where(revision => revision.RarityId == id)
			.OrderByDescending(revision => revision.Version)
			.ToListAsync(cancellationToken);
	}

	public async Task<RarityAdminResult> CreateAsync(
		RarityRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		var rarity = new ItemRarity();
		string? error = await ValidateAsync(
			rarity.Id,
			request,
			cancellationToken);

		if (error != null)
		{
			return new RarityAdminResult(
				RarityAdminError.Validation,
				Message: error);
		}

		Apply(rarity, request);
		_db.ItemRarities.Add(rarity);
		await _db.SaveChangesAsync(cancellationToken);
		Record(rarity, "created", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new RarityAdminResult(
			RarityAdminError.None,
			rarity);
	}

	public async Task<RarityAdminResult> UpdateAsync(
		int id,
		RarityRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		ItemRarity? rarity = await _db.ItemRarities.SingleOrDefaultAsync(
			item => item.Id == id && !item.IsDeleted,
			cancellationToken);

		if (rarity == null)
		{
			return new RarityAdminResult(RarityAdminError.NotFound);
		}

		if (request.Version != rarity.Version)
		{
			return new RarityAdminResult(RarityAdminError.Stale);
		}

		string? error = await ValidateAsync(id, request, cancellationToken);
		if (error != null)
		{
			return new RarityAdminResult(
				RarityAdminError.Validation,
				Message: error);
		}

		string action = rarity.IsArchived == request.IsArchived
			? "updated"
			: request.IsArchived ? "archived" : "unarchived";

		Apply(rarity, request);
		rarity.Version++;
		await SyncQualityAsync(rarity, cancellationToken);
		Record(rarity, action, actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new RarityAdminResult(
			RarityAdminError.None,
			rarity);
	}

	public async Task<RarityAdminResult> DeleteAsync(
		int id,
		int version,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		ItemRarity? rarity = await _db.ItemRarities.SingleOrDefaultAsync(
			item => item.Id == id && !item.IsDeleted,
			cancellationToken);

		if (rarity == null)
		{
			return new RarityAdminResult(RarityAdminError.NotFound);
		}

		if (version != rarity.Version)
		{
			return new RarityAdminResult(RarityAdminError.Stale);
		}

		if (await _db.Items.AnyAsync(
				item => item.RarityId == id,
				cancellationToken))
		{
			return new RarityAdminResult(
				RarityAdminError.InUse,
				Message:
					"Remove this rarity from assigned records before deleting it, or archive it.");
		}

		rarity.IsDeleted = true;
		rarity.Version++;
		Record(rarity, "deleted", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new RarityAdminResult(RarityAdminError.None);
	}

	public async Task<RarityAdminResult> RestoreAsync(
		int id,
		RevisionRestoreRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		ItemRarity? rarity = await _db.ItemRarities.SingleOrDefaultAsync(
			item => item.Id == id,
			cancellationToken);

		if (rarity == null)
		{
			return new RarityAdminResult(RarityAdminError.NotFound);
		}

		if (request.Version != rarity.Version)
		{
			return new RarityAdminResult(RarityAdminError.Stale);
		}

		RarityRevision? revision = await _db.RarityRevisions.SingleOrDefaultAsync(
			item => item.Id == request.RevisionId && item.RarityId == id,
			cancellationToken);

		if (revision == null)
		{
			return new RarityAdminResult(RarityAdminError.RevisionNotFound);
		}

		ItemRarity snapshot =
			JsonSerializer.Deserialize<ItemRarity>(revision.Snapshot)!;

		if (snapshot.IsDeleted)
		{
			return new RarityAdminResult(
				RarityAdminError.DeletedRevision,
				Message: "Select a revision before deletion.");
		}

		var restored = new RarityRequest(
			snapshot.Name,
			snapshot.Description,
			snapshot.Color,
			snapshot.SortOrder,
			snapshot.IsArchived,
			rarity.Version);

		string? error = await ValidateAsync(id, restored, cancellationToken);
		if (error != null)
		{
			return new RarityAdminResult(
				RarityAdminError.Validation,
				Message: error);
		}

		Apply(rarity, restored);
		rarity.IsDeleted = false;
		rarity.Version++;
		await SyncQualityAsync(rarity, cancellationToken);
		Record(rarity, "restored", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new RarityAdminResult(
			RarityAdminError.None,
			rarity);
	}

	private Task<int> SyncQualityAsync(
		ItemRarity rarity,
		CancellationToken cancellationToken)
	{
		return _db.Items
			.Where(item => item.RarityId == rarity.Id)
			.ExecuteUpdateAsync(
				setters => setters.SetProperty(
					item => item.Quality,
					rarity.Name),
				cancellationToken);
	}

	private async Task<string?> ValidateAsync(
		int id,
		RarityRequest request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
		{
			return "Name is required.";
		}

		string name = request.Name.Trim().ToLowerInvariant();
		bool exists = await _db.ItemRarities.AnyAsync(
			item =>
				item.Id != id &&
				!item.IsDeleted &&
				item.Name.ToLower() == name,
			cancellationToken);

		return exists
			? "A rarity with this name already exists."
			: null;
	}

	private static void Apply(
		ItemRarity rarity,
		RarityRequest request)
	{
		rarity.Name = request.Name.Trim();
		rarity.Description = request.Description?.Trim() ?? string.Empty;
		rarity.Color = request.Color;
		rarity.SortOrder = request.SortOrder;
		rarity.IsArchived = request.IsArchived;
	}

	private void Record(
		ItemRarity rarity,
		string action,
		string actor)
	{
		_db.RarityRevisions.Add(new RarityRevision
		{
			RarityId = rarity.Id,
			Version = rarity.Version,
			Action = action,
			Actor = actor,
			Snapshot = JsonSerializer.Serialize(rarity)
		});
	}
}
