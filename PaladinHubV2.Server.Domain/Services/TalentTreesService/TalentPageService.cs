using PaladinHub.Models;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.Data;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees;

public sealed class TalentPageService
{
    private readonly ITalentPageModelFactory _models;
    private readonly ITalentPageTreeSelector _selector;

    public TalentPageService(
        AppDbContext db,
        ITalentTreeService talentTrees)
        : this(
            new TalentPageModelFactory(
                new TalentPageContentReader(db),
                talentTrees),
            new TalentPageTreeSelector())
    {
    }

    public TalentPageService(
        ITalentPageModelFactory models,
        ITalentPageTreeSelector selector)
    {
        _models = models ??
            throw new ArgumentNullException(nameof(models));
        _selector = selector ??
            throw new ArgumentNullException(nameof(selector));
    }

    public string? NormalizeSection(string? section) =>
        _selector.NormalizeSection(section);

    public string? ResolveTreeSection(
        string key,
        string? section) =>
        _selector.ResolveTreeSection(
            key,
            section);

    public async Task<TalentSectionData> BuildSectionAsync(
        string normalizedSection)
    {
        CombinedViewModel model =
            await _models.BuildAsync(
                normalizedSection);

        string[] keys =
            _selector.BuildSectionKeys(
                model.TalentTrees,
                normalizedSection);

        return new TalentSectionData(
            normalizedSection,
            keys,
            model);
    }

    public async Task<TalentTreeData?> BuildTreeAsync(
        string normalizedSection,
        string requestedKey)
    {
        CombinedViewModel model =
            await _models.BuildAsync(
                normalizedSection);

        string? resolvedKey =
            _selector.ResolveTreeKey(
                requestedKey,
                model.TalentTrees.Keys,
                normalizedSection);

        if (resolvedKey is null)
        {
            return null;
        }

        string[] keys =
            _selector.BuildTreeKeys(
                model.TalentTrees,
                normalizedSection,
                resolvedKey);

        Dictionary<string, TalentTreeViewModel>
            selectedTrees = keys.ToDictionary(
                treeKey => treeKey,
                treeKey =>
                    model.TalentTrees[treeKey],
                StringComparer.OrdinalIgnoreCase);

        return new TalentTreeData(
            normalizedSection,
            requestedKey,
            resolvedKey,
            keys,
            model,
            selectedTrees);
    }
}
