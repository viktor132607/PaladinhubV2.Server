using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public enum MediaAdminError
{
	None,
	Validation,
	NotFound,
	Stale,
	InUse,
	RevisionNotFound,
	DeletedRevision
}

public sealed record MediaAdminResult(
	MediaAdminError Error,
	Guid? Id = null,
	string? Message = null);

public sealed class MediaAdminService
{
	private sealed record MediaSnapshot(
		string Name,
		string AltText,
		string Description,
		bool IsArchived,
		bool IsDeleted);

	private readonly AppDbContext _db;
	private readonly GameDataAssignmentService _assignments;

	public MediaAdminService(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_db = db;
		_assignments = assignments;
	}

	public async Task<MediaPageResult> ListAsync(
		string? search,
		string status,
		int page,
		int pageSize,
		CancellationToken cancellationToken)
	{
		IQueryable<SpellIcon> query =
			_db.SpellIcons.AsNoTracking().AsQueryable();

		query = status switch
		{
			"all" => query,
			"deleted" => query.Where(item => item.IsDeleted),
			"archived" => query.Where(item =>
				!item.IsDeleted && item.IsArchived),
			_ => query.Where(item =>
				!item.IsDeleted && !item.IsArchived)
		};

		if (!string.IsNullOrWhiteSpace(search))
		{
			string text = search.Trim().ToLower();
			query = query.Where(item =>
				item.Name.ToLower().Contains(text) ||
				item.AltText.ToLower().Contains(text) ||
				item.Description.ToLower().Contains(text));
		}

		pageSize = Math.Clamp(pageSize, 1, 100);
		int total = await query.CountAsync(cancellationToken);
		int pages = Math.Max(
			1,
			(int)Math.Ceiling(total / (double)pageSize));
		page = Math.Clamp(page, 1, pages);

		List<MediaListItem> media = await query
			.OrderByDescending(item => item.CreatedAtUtc)
			.ThenBy(item => item.Id)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(item => new MediaListItem(
				item.Id,
				item.Name,
				item.AltText,
				item.Description,
				item.IsArchived,
				item.IsDeleted,
				item.Version,
				item.CreatedAtUtc,
				item.ContentType,
				item.Content.Length,
				"/api/spell-icons/" + item.Id,
				_db.Spells.Count(spell =>
					spell.Icon != null &&
					spell.Icon.ToLower().Contains(item.Id.ToString())) +
				_db.Items.Count(catalogItem =>
					(catalogItem.Icon != null &&
					 catalogItem.Icon.ToLower().Contains(item.Id.ToString())) ||
					(catalogItem.SecondIcon != null &&
					 catalogItem.SecondIcon.ToLower().Contains(item.Id.ToString()))) +
				_db.ProductImages.Count(image =>
					image.Url.ToLower().Contains(item.Id.ToString())) +
				_db.ContentPages.Count(contentPage =>
					contentPage.JsonLayout.ToLower().Contains(item.Id.ToString())) +
				_db.DiscussionPosts.Count(post =>
					post.Content.ToLower().Contains(item.Id.ToString())) +
				_db.DiscussionComments.Count(comment =>
					comment.Content.ToLower().Contains(item.Id.ToString()))))
			.ToListAsync(cancellationToken);

		return new MediaPageResult(media, page, pages, total);
	}

	public Task<List<MediaRevision>> HistoryAsync(
		Guid id,
		CancellationToken cancellationToken)
	{
		return _db.MediaRevisions
			.AsNoTracking()
			.Where(revision => revision.MediaId == id)
			.OrderByDescending(revision => revision.Version)
			.ToListAsync(cancellationToken);
	}

	public async Task<MediaAdminResult> UpdateAsync(
		Guid id,
		MediaRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
		{
			return new MediaAdminResult(
				MediaAdminError.Validation,
				Message: "Name is required.");
		}

		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		SpellIcon? media = await _db.SpellIcons.SingleOrDefaultAsync(
			item => item.Id == id && !item.IsDeleted,
			cancellationToken);

		if (media == null)
		{
			return new MediaAdminResult(MediaAdminError.NotFound);
		}

		if (media.Version != request.Version)
		{
			return new MediaAdminResult(MediaAdminError.Stale);
		}

		string action =
			media.IsArchived == request.IsArchived
				? "updated"
				: request.IsArchived
					? "archived"
					: "unarchived";

		media.Name = request.Name.Trim();
		media.AltText = request.AltText?.Trim() ?? string.Empty;
		media.Description = request.Description?.Trim() ?? string.Empty;
		media.IsArchived = request.IsArchived;
		media.Version++;

		Record(media, action, actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new MediaAdminResult(MediaAdminError.None, media.Id);
	}

	public async Task<MediaAdminResult> DeleteAsync(
		Guid id,
		int version,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		SpellIcon? media = await _db.SpellIcons.SingleOrDefaultAsync(
			item => item.Id == id && !item.IsDeleted,
			cancellationToken);

		if (media == null)
		{
			return new MediaAdminResult(MediaAdminError.NotFound);
		}

		if (media.Version != version)
		{
			return new MediaAdminResult(MediaAdminError.Stale);
		}

		if (await UsageAsync(id, cancellationToken) > 0)
		{
			return new MediaAdminResult(
				MediaAdminError.InUse,
				Message:
					"This image is in use. Remove its references or archive it.");
		}

		media.IsDeleted = true;
		media.Version++;
		Record(media, "deleted", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new MediaAdminResult(MediaAdminError.None);
	}

	public async Task<MediaAdminResult> RestoreAsync(
		Guid id,
		RevisionRestoreRequest request,
		string actor,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		SpellIcon? media = await _db.SpellIcons.SingleOrDefaultAsync(
			item => item.Id == id,
			cancellationToken);

		if (media == null)
		{
			return new MediaAdminResult(MediaAdminError.NotFound);
		}

		if (media.Version != request.Version)
		{
			return new MediaAdminResult(MediaAdminError.Stale);
		}

		MediaRevision? revision = await _db.MediaRevisions
			.SingleOrDefaultAsync(
				item =>
					item.MediaId == id &&
					item.Id == request.RevisionId,
				cancellationToken);

		if (revision == null)
		{
			return new MediaAdminResult(
				MediaAdminError.RevisionNotFound);
		}

		MediaSnapshot snapshot =
			JsonSerializer.Deserialize<MediaSnapshot>(
				revision.Snapshot)!;

		if (snapshot.IsDeleted)
		{
			return new MediaAdminResult(
				MediaAdminError.DeletedRevision,
				Message: "Select a revision before deletion.");
		}

		media.Name = snapshot.Name;
		media.AltText = snapshot.AltText;
		media.Description = snapshot.Description;
		media.IsArchived = snapshot.IsArchived;
		media.IsDeleted = false;
		media.Version++;

		Record(media, "restored", actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new MediaAdminResult(MediaAdminError.None, media.Id);
	}

	private async Task<int> UsageAsync(
		Guid id,
		CancellationToken cancellationToken)
	{
		string key = id.ToString();

		return
			await _db.Spells.CountAsync(item =>
				item.Icon != null &&
				item.Icon.ToLower().Contains(key), cancellationToken) +
			await _db.Items.CountAsync(item =>
				(item.Icon != null &&
				 item.Icon.ToLower().Contains(key)) ||
				(item.SecondIcon != null &&
				 item.SecondIcon.ToLower().Contains(key)), cancellationToken) +
			await _db.ProductImages.CountAsync(item =>
				item.Url.ToLower().Contains(key), cancellationToken) +
			await _db.ContentPages.CountAsync(page =>
				page.JsonLayout.ToLower().Contains(key), cancellationToken) +
			await _db.DiscussionPosts.CountAsync(post =>
				post.Content.ToLower().Contains(key), cancellationToken) +
			await _db.DiscussionComments.CountAsync(comment =>
				comment.Content.ToLower().Contains(key), cancellationToken);
	}

	private void Record(
		SpellIcon media,
		string action,
		string actor)
	{
		_db.MediaRevisions.Add(new MediaRevision
		{
			MediaId = media.Id,
			Version = media.Version,
			Action = action,
			Actor = actor,
			Snapshot = JsonSerializer.Serialize(
				new MediaSnapshot(
					media.Name,
					media.AltText,
					media.Description,
					media.IsArchived,
					media.IsDeleted))
		});
	}
}
