using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum TagAdminError
{
	None,
	NotFound,
	Stale,
	Validation,
	InUse,
	RevisionNotFound,
	DeletedRevision
}

public sealed record TagAdminResult(
	TagAdminError Error,
	GameTag? Tag = null,
	string? Message = null);

public sealed class TagAdminService
{
	private readonly AppDbContext _db;
	private readonly GameDataAssignmentService _assignments;

	public TagAdminService(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_db = db;
		_assignments = assignments;
	}

	public Task<List<TagListItem>> ListAsync(
		CancellationToken cancellationToken)
	{
		return _db.GameTags
			.AsNoTracking()
			.OrderBy(item => item.SortOrder)
			.ThenBy(item => item.Name)
			.Select(item => new TagListItem(
				item.Id,
				item.Name,
				item.Description,
				null,
				item.SortOrder,
				item.IsArchived,
				item.IsDeleted,
				item.Version,
				_db.Spells.Count(spell => spell.TagIds.Contains(item.Id)) +
				_db.Items.Count(product => product.TagIds.Contains(item.Id)),
				0))
			.ToListAsync(cancellationToken);
	}

	public Task<List<TagRevision>> HistoryAsync(
		int id,
		CancellationToken cancellationToken)
	{
		return _db.TagRevisions
			.AsNoTracking()
			.Where(revision => revision.TagId == id)
			.OrderByDescending(revision => revision.Version)
			.ToListAsync(cancellationToken);
	}

	public async Task<TagAdminResult> CreateAsync(
		TagRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction = await _assignments.BeginAsync(cancellationToken);

		var tag = new GameTag();
		string? error = await ValidateAsync(tag.Id, request, cancellationToken);
		if (error != null)
		{
			return new TagAdminResult(
				TagAdminError.Validation,
				Message: error);
		}

		Apply(tag, request);
		_db.GameTags.Add(tag);
		await _db.SaveChangesAsync(cancellationToken);
		Record(tag, "created", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new TagAdminResult(TagAdminError.None, tag);
	}

	public async Task<TagAdminResult> UpdateAsync(
		int id,
		TagRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction = await _assignments.BeginAsync(cancellationToken);

		GameTag? tag = await _db.GameTags.SingleOrDefaultAsync(
			item => item.Id == id && !item.IsDeleted,
			cancellationToken);

		if (tag == null)
		{
			return new TagAdminResult(TagAdminError.NotFound);
		}

		if (request.Version != tag.Version)
		{
			return new TagAdminResult(TagAdminError.Stale);
		}

		string? error = await ValidateAsync(id, request, cancellationToken);
		if (error != null)
		{
			return new TagAdminResult(
				TagAdminError.Validation,
				Message: error);
		}

		string action = tag.IsArchived == request.IsArchived
			? "updated"
			: request.IsArchived ? "archived" : "unarchived";

		Apply(tag, request);
		tag.Version++;
		Record(tag, action, actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new TagAdminResult(TagAdminError.None, tag);
	}

	public async Task<TagAdminResult> DeleteAsync(
		int id,
		int version,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction = await _assignments.BeginAsync(cancellationToken);

		GameTag? tag = await _db.GameTags.SingleOrDefaultAsync(
			item => item.Id == id && !item.IsDeleted,
			cancellationToken);

		if (tag == null)
		{
			return new TagAdminResult(TagAdminError.NotFound);
		}

		if (version != tag.Version)
		{
			return new TagAdminResult(TagAdminError.Stale);
		}

		bool inUse =
			await _db.Spells.AnyAsync(spell => spell.TagIds.Contains(id), cancellationToken) ||
			await _db.Items.AnyAsync(item => item.TagIds.Contains(id), cancellationToken);

		if (inUse)
		{
			return new TagAdminResult(
				TagAdminError.InUse,
				Message:
					"Remove this tag from assigned records before deleting it, or archive it.");
		}

		tag.IsDeleted = true;
		tag.Version++;
		Record(tag, "deleted", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new TagAdminResult(TagAdminError.None);
	}

	public async Task<TagAdminResult> RestoreAsync(
		int id,
		RevisionRestoreRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction = await _assignments.BeginAsync(cancellationToken);

		GameTag? tag = await _db.GameTags.SingleOrDefaultAsync(
			item => item.Id == id,
			cancellationToken);

		if (tag == null)
		{
			return new TagAdminResult(TagAdminError.NotFound);
		}

		if (request.Version != tag.Version)
		{
			return new TagAdminResult(TagAdminError.Stale);
		}

		TagRevision? revision = await _db.TagRevisions.SingleOrDefaultAsync(
			item => item.Id == request.RevisionId && item.TagId == id,
			cancellationToken);

		if (revision == null)
		{
			return new TagAdminResult(TagAdminError.RevisionNotFound);
		}

		GameTag snapshot = JsonSerializer.Deserialize<GameTag>(revision.Snapshot)!;
		if (snapshot.IsDeleted)
		{
			return new TagAdminResult(
				TagAdminError.DeletedRevision,
				Message: "Select a revision before deletion.");
		}

		var restored = new TagRequest(
			snapshot.Name,
			snapshot.Description,
			snapshot.SortOrder,
			snapshot.IsArchived,
			tag.Version);

		string? error = await ValidateAsync(id, restored, cancellationToken);
		if (error != null)
		{
			return new TagAdminResult(
				TagAdminError.Validation,
				Message: error);
		}

		Apply(tag, restored);
		tag.IsDeleted = false;
		tag.Version++;
		Record(tag, "restored", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new TagAdminResult(TagAdminError.None, tag);
	}

	private async Task<string?> ValidateAsync(
		int id,
		TagRequest request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
		{
			return "Name is required.";
		}

		string name = request.Name.Trim().ToLowerInvariant();
		bool exists = await _db.GameTags.AnyAsync(
			item =>
				item.Id != id &&
				!item.IsDeleted &&
				item.Name.ToLower() == name,
			cancellationToken);

		return exists
			? "A tag with this name already exists."
			: null;
	}

	private static void Apply(GameTag tag, TagRequest request)
	{
		tag.Name = request.Name.Trim();
		tag.Description = request.Description?.Trim() ?? string.Empty;
		tag.SortOrder = request.SortOrder;
		tag.IsArchived = request.IsArchived;
	}

	private void Record(GameTag tag, string action, string actor)
	{
		_db.TagRevisions.Add(new TagRevision
		{
			TagId = tag.Id,
			Version = tag.Version,
			Action = action,
			Actor = actor,
			Snapshot = JsonSerializer.Serialize(tag)
		});
	}
}
