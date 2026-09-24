using PaladinHub.Models.Talents;

namespace PaladinHubV2.Server.Domain.Services.TalentTrees;

public sealed class TalentPageTreeSelector :
    ITalentPageTreeSelector
{
    public string? NormalizeSection(string? section)
    {
        string normalized =
            (section ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

        return normalized switch
        {
            "holy" or "holly" => "holy",
            "prot" or "protection" => "protection",
            "ret" or "retri" or "retribution" =>
                "retribution",
            _ => null
        };
    }

    public string? ResolveTreeSection(
        string key,
        string? section)
    {
        string? sectionSource =
            !string.IsNullOrWhiteSpace(section)
                ? section
                : key.Split(
                        '-',
                        StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();

        return NormalizeSection(sectionSource);
    }

    public string[] BuildSectionKeys(
        IReadOnlyDictionary<string, TalentTreeViewModel> trees,
        string section)
    {
        string? classKey =
            FindExact(trees, "paladin");

        string? heroKey =
            FindHeroTree(trees, section, "herald") ??
            FindHeroTree(trees, section, "lightsmith") ??
            FindHeroTree(trees, section, "templar");

        string? specializationKey =
            FindSpecializationTree(
                trees,
                section);

        return NormalizeKeys(
            trees,
            classKey,
            heroKey,
            specializationKey);
    }

    public string[] BuildTreeKeys(
        IReadOnlyDictionary<string, TalentTreeViewModel> trees,
        string section,
        string resolvedKey)
    {
        string? classKey =
            FindExact(trees, "paladin");

        string? specializationKey =
            FindSpecializationTree(
                trees,
                section);

        return NormalizeKeys(
            trees,
            classKey,
            resolvedKey,
            specializationKey);
    }

    public string? ResolveTreeKey(
        string rawKey,
        IEnumerable<string> candidates,
        string section)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return null;
        }

        string requestedKey =
            rawKey.Trim().ToLowerInvariant();

        List<string> keys =
            candidates.ToList();

        string? result = keys.FirstOrDefault(key =>
            key.Equals(
                requestedKey,
                StringComparison.OrdinalIgnoreCase));

        if (result is not null)
        {
            return result;
        }

        if (IsHeroName(requestedKey))
        {
            result = keys.FirstOrDefault(key =>
                key.Contains(
                    section,
                    StringComparison.OrdinalIgnoreCase) &&
                key.EndsWith(
                    $"-{requestedKey}",
                    StringComparison.OrdinalIgnoreCase));

            if (result is not null)
            {
                return result;
            }

            result = keys.FirstOrDefault(key =>
                key.EndsWith(
                    $"-{requestedKey}",
                    StringComparison.OrdinalIgnoreCase));

            if (result is not null)
            {
                return result;
            }
        }

        result = keys.FirstOrDefault(key =>
            key.EndsWith(
                requestedKey,
                StringComparison.OrdinalIgnoreCase));

        if (result is not null)
        {
            return result;
        }

        string[] requestedTokens =
            requestedKey.Split(
                '-',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        if (requestedTokens.Length > 0)
        {
            result = keys.FirstOrDefault(key =>
            {
                string[] candidateTokens =
                    key.Split(
                        '-',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries);

                return requestedTokens.All(token =>
                    candidateTokens.Contains(
                        token,
                        StringComparer.OrdinalIgnoreCase));
            });

            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private static string? FindExact(
        IReadOnlyDictionary<string, TalentTreeViewModel> trees,
        string key) =>
        trees.Keys.FirstOrDefault(candidate =>
            candidate.Equals(
                key,
                StringComparison.OrdinalIgnoreCase));

    private static string? FindHeroTree(
        IReadOnlyDictionary<string, TalentTreeViewModel> trees,
        string section,
        string hero)
    {
        return trees.Keys.FirstOrDefault(key =>
                   key.Contains(
                       section,
                       StringComparison.OrdinalIgnoreCase) &&
                   key.EndsWith(
                       $"-{hero}",
                       StringComparison.OrdinalIgnoreCase))
               ?? trees.Keys.FirstOrDefault(key =>
                   key.EndsWith(
                       $"-{hero}",
                       StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindSpecializationTree(
        IReadOnlyDictionary<string, TalentTreeViewModel> trees,
        string section)
    {
        return FindExact(trees, section)
            ?? trees.Keys.FirstOrDefault(key =>
                key.Contains(
                    section,
                    StringComparison.OrdinalIgnoreCase) &&
                !IsHeroKey(key));
    }

    private static string[] NormalizeKeys(
        IReadOnlyDictionary<string, TalentTreeViewModel> trees,
        params string?[] keys)
    {
        return keys
            .Where(key =>
                !string.IsNullOrWhiteSpace(key) &&
                trees.ContainsKey(key))
            .Select(key => key!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsHeroName(string key) =>
        key is "herald" or "lightsmith" or "templar";

    private static bool IsHeroKey(string key) =>
        key.EndsWith(
            "-herald",
            StringComparison.OrdinalIgnoreCase) ||
        key.EndsWith(
            "-lightsmith",
            StringComparison.OrdinalIgnoreCase) ||
        key.EndsWith(
            "-templar",
            StringComparison.OrdinalIgnoreCase);
}
