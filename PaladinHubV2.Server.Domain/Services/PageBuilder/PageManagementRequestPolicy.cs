using PaladinHub.Areas.Admin.Models;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder;

public sealed class PageManagementRequestPolicy :
    IPageManagementRequestPolicy
{
    private static readonly HashSet<string> ReservedRoutes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "holy/overview",
            "holy/gear",
            "holy/talents",
            "holy/consumables",
            "holy/rotation",
            "holy/stats",
            "protection/overview",
            "protection/gear",
            "protection/talents",
            "protection/consumables",
            "protection/rotation",
            "protection/stats",
            "retribution/overview",
            "retribution/gear",
            "retribution/talents",
            "retribution/consumables",
            "retribution/rotation",
            "retribution/stats"
        };

    public string? ValidateRequest(
        SavePageRequest? request)
    {
        if (request is null)
        {
            return "Request body is required.";
        }

        if (!TryNormalizeSection(
                request.Section,
                out _))
        {
            return "Section must be Holy, Protection, or Retribution.";
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Page title is required.";
        }

        if (request.Title.Trim().Length > 200)
        {
            return "Page title cannot exceed 200 characters.";
        }

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            return "Slug is required.";
        }

        string slug = Slugify(request.Slug);

        if (slug.Length == 0)
        {
            return "Slug must contain letters or numbers.";
        }

        if (slug.Length > 100)
        {
            return "Slug cannot exceed 100 characters.";
        }

        return null;
    }

    public string NormalizeSection(
        string value)
    {
        TryNormalizeSection(
            value,
            out string section);

        return section;
    }

    public string Slugify(
        string value)
    {
        var output = new List<char>();
        bool pendingDash = false;

        foreach (char character in
                 value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingDash && output.Count > 0)
                {
                    output.Add('-');
                }

                output.Add(character);
                pendingDash = false;
            }
            else if (character == '-' ||
                     char.IsWhiteSpace(character))
            {
                pendingDash = output.Count > 0;
            }
        }

        return new string(output.ToArray())
            .Trim('-');
    }

    public bool IsReserved(
        string section,
        string slug)
    {
        return ReservedRoutes.Contains(
            $"{section}/{slug}");
    }

    internal static bool TryNormalizeSection(
        string? value,
        out string section)
    {
        section = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant() switch
        {
            "holy" => "holy",
            "protection" or "prot" => "protection",
            "retribution" or "retri" or "ret" =>
                "retribution",
            _ => string.Empty
        };

        return section.Length > 0;
    }
}
