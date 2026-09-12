using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed record RecordTypeListItem(string Name, int UsageCount);

public enum RecordTypeAdminError
{
	None,
	Validation,
	Duplicate,
	NotFound,
	SameReplacement,
	ReplacementNotFound,
	InUse
}

public sealed record RecordTypeAdminResult(
	RecordTypeAdminError Error,
	string? Name = null,
	int UsageCount = 0,
	string? Message = null);

public sealed class RecordTypeAdminService
{
	private readonly AppDbContext _db;

	public RecordTypeAdminService(AppDbContext db)
	{
		_db = db;
	}

	public Task<List<RecordTypeListItem>> ListAsync(
		CancellationToken cancellationToken)
	{
		return _db.RecordTypes
			.AsNoTracking()
			.OrderBy(type => type.Name)
			.Select(type => new RecordTypeListItem(
				type.Name,
				_db.Spells.Count(spell => spell.Quality == type.Name)))
			.ToListAsync(cancellationToken);
	}

	public async Task<RecordTypeAdminResult> CreateAsync(
		string? requestedName,
		CancellationToken cancellationToken)
	{
		string name = Normalize(requestedName);
		if (name.Length is 0 or > 50)
		{
			return new RecordTypeAdminResult(
				RecordTypeAdminError.Validation,
				Message: "Type must contain 1–50 characters.");
		}

		_db.RecordTypes.Add(new RecordType { Name = name });

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException exception)
			when (exception.InnerException is PostgresException { SqlState: "23505" })
		{
			return new RecordTypeAdminResult(
				RecordTypeAdminError.Duplicate,
				Message: "A type with this name already exists.");
		}

		return new RecordTypeAdminResult(
			RecordTypeAdminError.None,
			name,
			0);
	}

	public async Task<RecordTypeAdminResult> RenameAsync(
		string currentName,
		string? requestedName,
		CancellationToken cancellationToken)
	{
		string renamed = Normalize(requestedName);
		if (renamed.Length is 0 or > 50)
		{
			return new RecordTypeAdminResult(
				RecordTypeAdminError.Validation,
				Message: "Type must contain 1–50 characters.");
		}

		try
		{
			int count = await _db.Database.ExecuteSqlInterpolatedAsync(
				$"UPDATE \"RecordTypes\" SET \"Name\" = {renamed} WHERE \"Name\" = {currentName}",
				cancellationToken);

			if (count == 0)
			{
				return new RecordTypeAdminResult(
					RecordTypeAdminError.NotFound,
					Message: "Type no longer exists. Refresh the list.");
			}
		}
		catch (PostgresException exception) when (exception.SqlState == "23505")
		{
			return new RecordTypeAdminResult(
				RecordTypeAdminError.Duplicate,
				Message: "A type with this name already exists.");
		}

		return new RecordTypeAdminResult(
			RecordTypeAdminError.None,
			renamed);
	}

	public async Task<RecordTypeAdminResult> DeleteAsync(
		string name,
		string? replacement,
		CancellationToken cancellationToken)
	{
		if (replacement == name)
		{
			return new RecordTypeAdminResult(
				RecordTypeAdminError.SameReplacement,
				Message: "Choose a different replacement type.");
		}

		await using var transaction =
			await _db.Database.BeginTransactionAsync(cancellationToken);

		List<RecordType> locked = await _db.RecordTypes
			.FromSqlInterpolated(
				$"SELECT * FROM \"RecordTypes\" WHERE \"Name\" = {name} OR \"Name\" = {replacement} ORDER BY \"Name\" FOR UPDATE")
			.ToListAsync(cancellationToken);

		RecordType? type = locked.FirstOrDefault(current => current.Name == name);
		if (type == null)
		{
			return new RecordTypeAdminResult(
				RecordTypeAdminError.NotFound,
				Message: "Type no longer exists. Refresh the list.");
		}

		if (replacement != null &&
			!locked.Any(current => current.Name == replacement))
		{
			return new RecordTypeAdminResult(
				RecordTypeAdminError.ReplacementNotFound,
				Message: "Replacement type does not exist.");
		}

		if (replacement == null &&
			await _db.Spells.AnyAsync(
				spell => spell.Quality == name,
				cancellationToken))
		{
			return new RecordTypeAdminResult(
				RecordTypeAdminError.InUse,
				Message: "This type is in use. Choose a replacement before deleting it.");
		}

		if (replacement != null)
		{
			await _db.Spells
				.Where(spell => spell.Quality == name)
				.ExecuteUpdateAsync(
					update => update.SetProperty(
						spell => spell.Quality,
						replacement),
					cancellationToken);
		}

		_db.RecordTypes.Remove(type);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new RecordTypeAdminResult(RecordTypeAdminError.None);
	}

	private static string Normalize(string? value)
	{
		return (value ?? string.Empty).Trim().ToLowerInvariant();
	}
}
