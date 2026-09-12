using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.SpellbookService;

public enum SpellMutationError
{
	None = 0,
	NotFound,
	InvalidCategory,
	InvalidDiscipline,
	InvalidPatch,
	InvalidMedia,
	InvalidTags,
	InvalidRecordType,
	RecordTypeChanged
}

public sealed record SpellMutationResult(
	SpellMutationError Error,
	Spell? Spell = null);

public sealed class SpellAdminService
{
	private readonly AppDbContext _db;
	private readonly GameDataAssignmentService _assignments;

	public SpellAdminService(AppDbContext db)
	{
		_db = db;
		_assignments = new GameDataAssignmentService(db);
	}

	public Task<Spell?> GetAsync(
		int id,
		CancellationToken cancellationToken)
	{
		return _db.Spells
			.AsNoTracking()
			.FirstOrDefaultAsync(
				spell => spell.Id == id,
				cancellationToken);
	}

	public async Task<SpellMutationResult> CreateAsync(
		Spell spell,
		CancellationToken cancellationToken)
	{
		spell.Id = 0;

		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		SpellMutationError validation =
			await ValidateAssignmentsAsync(
				spell,
				previous: null,
				cancellationToken);

		if (validation != SpellMutationError.None)
		{
			return new SpellMutationResult(validation);
		}

		NormalizeSpell(spell);

		if (!await RecordTypeExistsAsync(
				spell.Quality,
				cancellationToken))
		{
			return new SpellMutationResult(
				SpellMutationError.InvalidRecordType);
		}

		_db.Spells.Add(spell);

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException exception)
			when (IsForeignKeyViolation(exception))
		{
			return new SpellMutationResult(
				SpellMutationError.RecordTypeChanged);
		}

		await transaction.CommitAsync(cancellationToken);

		return new SpellMutationResult(
			SpellMutationError.None,
			spell);
	}

	public async Task<SpellMutationResult> UpdateAsync(
		int id,
		Spell spell,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		Spell? existing = await _db.Spells
			.FirstOrDefaultAsync(
				current => current.Id == id,
				cancellationToken);

		if (existing == null)
		{
			return new SpellMutationResult(
				SpellMutationError.NotFound);
		}

		SpellMutationError validation =
			await ValidateAssignmentsAsync(
				spell,
				existing,
				cancellationToken);

		if (validation != SpellMutationError.None)
		{
			return new SpellMutationResult(validation);
		}

		NormalizeSpell(spell);

		if (!await RecordTypeExistsAsync(
				spell.Quality,
				cancellationToken))
		{
			return new SpellMutationResult(
				SpellMutationError.InvalidRecordType);
		}

		existing.CategoryId = spell.CategoryId;
		existing.DisciplineId = spell.DisciplineId;
		existing.PatchId = spell.PatchId;
		existing.TagIds = spell.TagIds;
		existing.Name = spell.Name;
		existing.Icon = spell.Icon;
		existing.Description = spell.Description;
		existing.Url = spell.Url;
		existing.Quality = spell.Quality;

		NormalizeSpell(existing);

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException exception)
			when (IsForeignKeyViolation(exception))
		{
			return new SpellMutationResult(
				SpellMutationError.RecordTypeChanged);
		}

		await transaction.CommitAsync(cancellationToken);

		return new SpellMutationResult(
			SpellMutationError.None,
			existing);
	}

	public async Task<bool> DeleteAsync(
		int id,
		CancellationToken cancellationToken)
	{
		Spell? spell = await _db.Spells
			.FirstOrDefaultAsync(
				current => current.Id == id,
				cancellationToken);

		if (spell == null)
		{
			return false;
		}

		_db.Spells.Remove(spell);
		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	private async Task<SpellMutationError> ValidateAssignmentsAsync(
		Spell spell,
		Spell? previous,
		CancellationToken cancellationToken)
	{
		if (!await _assignments.CanAssignCategoryAsync(
				spell.CategoryId,
				previous?.CategoryId,
				cancellationToken))
		{
			return SpellMutationError.InvalidCategory;
		}

		if (!await _assignments.CanAssignDisciplineAsync(
				spell.DisciplineId,
				previous?.DisciplineId,
				cancellationToken))
		{
			return SpellMutationError.InvalidDiscipline;
		}

		if (!await _assignments.CanAssignPatchAsync(
				spell.PatchId,
				previous?.PatchId,
				cancellationToken))
		{
			return SpellMutationError.InvalidPatch;
		}

		if (!await _assignments.CanAssignMediaAsync(
				spell.Icon,
				previous?.Icon,
				cancellationToken))
		{
			return SpellMutationError.InvalidMedia;
		}

		spell.TagIds = (spell.TagIds ?? [])
			.Distinct()
			.ToArray();

		if (!await _assignments.CanAssignTagsAsync(
				spell.TagIds,
				previous?.TagIds ?? [],
				cancellationToken))
		{
			return SpellMutationError.InvalidTags;
		}

		return SpellMutationError.None;
	}

	private Task<bool> RecordTypeExistsAsync(
		string quality,
		CancellationToken cancellationToken)
	{
		return _db.RecordTypes.AnyAsync(
			type => type.Name == quality,
			cancellationToken);
	}

	private static void NormalizeSpell(Spell spell)
	{
		spell.Name = spell.Name.Trim();
		spell.Icon = NormalizeOptional(spell.Icon);
		spell.Description = NormalizeOptional(spell.Description);
		spell.Url = NormalizeOptional(spell.Url);
		spell.Quality = string.IsNullOrWhiteSpace(spell.Quality)
			? "spell"
			: spell.Quality.Trim().ToLowerInvariant();
	}

	private static string? NormalizeOptional(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: value.Trim();
	}

	private static bool IsForeignKeyViolation(
		DbUpdateException exception)
	{
		return exception.InnerException is
			Npgsql.PostgresException { SqlState: "23503" };
	}
}
