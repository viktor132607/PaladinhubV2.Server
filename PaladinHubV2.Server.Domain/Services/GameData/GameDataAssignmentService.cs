using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.GameData
{
	public sealed class GameDataAssignmentService
	{
		private readonly AppDbContext _db;

		public GameDataAssignmentService(AppDbContext db)
		{
			_db = db;
		}

		public async Task<IDbContextTransaction> BeginAsync(
			CancellationToken cancellationToken)
		{
			IDbContextTransaction transaction =
				await _db.Database.BeginTransactionAsync(
					cancellationToken);

			try
			{
				await _db.Database.ExecuteSqlRawAsync(
					"SELECT pg_advisory_xact_lock(8820411)",
					cancellationToken);
			}
			catch
			{
				await transaction.DisposeAsync();
				throw;
			}

			return transaction;
		}

		public async Task<bool> CanAssignCategoryAsync(
			int? id,
			int? previousId,
			CancellationToken cancellationToken)
		{
			if (id is null)
			{
				return true;
			}

			return await _db.Categories.AnyAsync(
				category =>
					category.Id == id &&
					!category.IsDeleted &&
					(!category.IsArchived || id == previousId),
				cancellationToken);
		}

		public async Task<bool> CanAssignDisciplineAsync(
			int? id,
			int? previousId,
			CancellationToken cancellationToken)
		{
			if (id is null)
			{
				return true;
			}

			var value = await _db.GameDisciplines
				.AsNoTracking()
				.SingleOrDefaultAsync(
					candidate => candidate.Id == id,
					cancellationToken);

			if (value is null ||
				value.IsDeleted ||
				(value.IsArchived && id != previousId))
			{
				return false;
			}

			if (value.ParentId is null)
			{
				return true;
			}

			return await _db.GameDisciplines.AnyAsync(
				candidate =>
					candidate.Id == value.ParentId &&
					!candidate.IsDeleted &&
					candidate.ParentId == null &&
					(!candidate.IsArchived || id == previousId),
				cancellationToken);
		}

		public async Task<bool> CanAssignTagsAsync(
			int[] ids,
			int[] previousIds,
			CancellationToken cancellationToken)
		{
			if (ids.Length > 100)
			{
				return false;
			}

			int existingCount = await _db.GameTags.CountAsync(
				tag =>
					ids.Contains(tag.Id) &&
					!tag.IsDeleted &&
					(!tag.IsArchived || previousIds.Contains(tag.Id)),
				cancellationToken);

			return existingCount == ids.Distinct().Count();
		}

		public async Task<bool> CanAssignPatchAsync(
			int? id,
			int? previousId,
			CancellationToken cancellationToken)
		{
			if (id is null)
			{
				return true;
			}

			return await _db.GamePatches.AnyAsync(
				patch =>
					patch.Id == id &&
					!patch.IsDeleted &&
					(!patch.IsArchived || id == previousId),
				cancellationToken);
		}

		public async Task<bool> CanAssignMediaAsync(
			string? value,
			string? previous,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(value) ||
				value == previous)
			{
				return true;
			}

			const string marker = "/api/spell-icons/";

			int index = value.IndexOf(
				marker,
				StringComparison.OrdinalIgnoreCase);

			if (index < 0)
			{
				return true;
			}

			string tail = value[(index + marker.Length)..]
				.Split('?', '#')[0];

			return Guid.TryParse(tail, out Guid id) &&
				await _db.SpellIcons.AnyAsync(
					media =>
						media.Id == id &&
						!media.IsDeleted &&
						!media.IsArchived,
					cancellationToken);
		}
	}
}
