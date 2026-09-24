using PaladinHub.Models;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees;

public sealed record TalentSectionData(
    string Section,
    string[] Keys,
    CombinedViewModel Model);

public sealed record TalentTreeData(
    string Section,
    string RequestedKey,
    string ResolvedKey,
    string[] Keys,
    CombinedViewModel Model,
    IReadOnlyDictionary<string, TalentTreeViewModel> SelectedTrees);

public sealed record TalentPageContent(
    List<Spell> Spells,
    List<Item> Items);

public interface ITalentPageContentReader
{
    Task<TalentPageContent> LoadAsync();
}

public interface ITalentPageModelFactory
{
    Task<CombinedViewModel> BuildAsync(string section);
}

public interface ITalentPageTreeSelector
{
    string? NormalizeSection(string? section);

    string? ResolveTreeSection(
        string key,
        string? section);

    string[] BuildSectionKeys(
        IReadOnlyDictionary<string, TalentTreeViewModel> trees,
        string section);

    string[] BuildTreeKeys(
        IReadOnlyDictionary<string, TalentTreeViewModel> trees,
        string section,
        string resolvedKey);

    string? ResolveTreeKey(
        string rawKey,
        IEnumerable<string> candidates,
        string section);
}
