using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.ItemsService;

public enum ItemMutationError
{
	None = 0,
	NotFound,
	InvalidCategory,
	InvalidDiscipline,
	InvalidPatch,
	InvalidRarity,
	InvalidMedia,
	InvalidTags
}

public sealed record ItemMutationResult(
	ItemMutationError Error,
	Item? Item = null);

public sealed class ItemAdminService
{
	private readonly AppDbContext _db;
	private readonly GameDataAssignmentService _assignments;

	public ItemAdminService(AppDbContext db)
	{
		_db = db;
		_assignments = new GameDataAssignmentService(db);
	}

	public Task<Item?> GetAsync(
		int id,
		CancellationToken cancellationToken)
	{
		return _db.Items
			.AsNoTracking()
			.FirstOrDefaultAsync(
				item => item.Id == id,
				cancellationToken);
	}

	public async Task<ItemMutationResult> CreateAsync(
		Item item,
		CancellationToken cancellationToken)
	{
		item.Id = 0;

		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		ItemMutationError validation =
			await ValidateAssignmentsAsync(
				item,
				previous: null,
				cancellationToken);

		if (validation != ItemMutationError.None)
		{
			return new ItemMutationResult(validation);
		}

		Normalize(item);

		_db.Items.Add(item);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new ItemMutationResult(
			ItemMutationError.None,
			item);
	}

	public async Task<ItemMutationResult> UpdateAsync(
		int id,
		Item item,
		CancellationToken cancellationToken)
	{
		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		Item? existing = await _db.Items
			.FirstOrDefaultAsync(
				current => current.Id == id,
				cancellationToken);

		if (existing == null)
		{
			return new ItemMutationResult(
				ItemMutationError.NotFound);
		}

		ItemMutationError validation =
			await ValidateAssignmentsAsync(
				item,
				existing,
				cancellationToken);

		if (validation != ItemMutationError.None)
		{
			return new ItemMutationResult(validation);
		}

		existing.Name = item.Name.Trim();
		existing.CategoryId = item.CategoryId;
		existing.DisciplineId = item.DisciplineId;
		existing.PatchId = item.PatchId;
		existing.RarityId = item.RarityId;
		existing.TagIds = item.TagIds;
		existing.Icon = NormalizeOptional(item.Icon);
		existing.SecondIcon = NormalizeOptional(item.SecondIcon);
		existing.Description = NormalizeOptional(item.Description);
		existing.Url = NormalizeOptional(item.Url);
		existing.ItemLevel = item.ItemLevel;
		existing.RequiredLevel = item.RequiredLevel;
		existing.Quality = NormalizeOptional(item.Quality);

		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new ItemMutationResult(
			ItemMutationError.None,
			existing);
	}

	public async Task<bool> DeleteAsync(
		int id,
		CancellationToken cancellationToken)
	{
		Item? item = await _db.Items
			.FirstOrDefaultAsync(
				current => current.Id == id,
				cancellationToken);

		if (item == null)
		{
			return false;
		}

		_db.Items.Remove(item);
		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	private async Task<ItemMutationError> ValidateAssignmentsAsync(
		Item item,
		Item? previous,
		CancellationToken cancellationToken)
	{
		if (!await _assignments.CanAssignCategoryAsync(
				item.CategoryId,
				previous?.CategoryId,
				cancellationToken))
		{
			return ItemMutationError.InvalidCategory;
		}

		if (!await _assignments.CanAssignDisciplineAsync(
				item.DisciplineId,
				previous?.DisciplineId,
				cancellationToken))
		{
			return ItemMutationError.InvalidDiscipline;
		}

		if (!await _assignments.CanAssignPatchAsync(
				item.PatchId,
				previous?.PatchId,
				cancellationToken))
		{
			return ItemMutationError.InvalidPatch;
		}

		ItemRarity? rarity = item.RarityId is null
			? null
			: await _db.ItemRarities.SingleOrDefaultAsync(
				rarity => rarity.Id == item.RarityId,
				cancellationToken);

		if (item.RarityId is not null &&
			(rarity is null ||
			 rarity.IsDeleted ||
			 (rarity.IsArchived &&
			  item.RarityId != previous?.RarityId)))
		{
			return ItemMutationError.InvalidRarity;
		}

		item.Quality = rarity?.Name;

		if (!await _assignments.CanAssignMediaAsync(
				item.Icon,
				previous?.Icon,
				cancellationToken) ||
			!await _assignments.CanAssignMediaAsync(
				item.SecondIcon,
				previous?.SecondIcon,
				cancellationToken))
		{
			return ItemMutationError.InvalidMedia;
		}

		item.TagIds = (item.TagIds ?? [])
			.Distinct()
			.ToArray();

		if (!await _assignments.CanAssignTagsAsync(
				item.TagIds,
				previous?.TagIds ?? [],
				cancellationToken))
		{
			return ItemMutationError.InvalidTags;
		}

		return ItemMutationError.None;
	}

	private static void Normalize(Item item)
	{
		item.Name = item.Name.Trim();
		item.Icon = NormalizeOptional(item.Icon);
		item.SecondIcon = NormalizeOptional(item.SecondIcon);
		item.Description = NormalizeOptional(item.Description);
		item.Url = NormalizeOptional(item.Url);
		item.Quality = NormalizeOptional(item.Quality);
	}

	private static string? NormalizeOptional(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: value.Trim();
	}
}
