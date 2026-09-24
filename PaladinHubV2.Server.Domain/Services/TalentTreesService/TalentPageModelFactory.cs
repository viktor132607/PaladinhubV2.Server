using PaladinHub.Models;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees;

public sealed class TalentPageModelFactory :
    ITalentPageModelFactory
{
    private readonly ITalentPageContentReader _content;
    private readonly ITalentTreeService _talentTrees;

    public TalentPageModelFactory(
        ITalentPageContentReader content,
        ITalentTreeService talentTrees)
    {
        _content = content ??
            throw new ArgumentNullException(nameof(content));
        _talentTrees = talentTrees ??
            throw new ArgumentNullException(nameof(talentTrees));
    }

    public async Task<CombinedViewModel> BuildAsync(
        string section)
    {
        TalentPageContent content =
            await _content.LoadAsync();

        var trees =
            await _talentTrees.GetTalentTrees(
                section,
                content.Spells);

        string titleSection = TitleCase(section);

        return new CombinedViewModel
        {
            Section = titleSection,
            PageTitle =
                $"{titleSection} Paladin – Talents",
            Spells = content.Spells,
            Items = content.Items,
            TalentTrees = trees
        };
    }

    private static string TitleCase(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? value
            : char.ToUpperInvariant(value[0]) +
              value[1..];
}
