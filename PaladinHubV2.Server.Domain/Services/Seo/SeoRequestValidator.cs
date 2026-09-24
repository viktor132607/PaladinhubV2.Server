namespace PaladinHubV2.Server.Domain.Services.Seo;

internal static class SeoRequestValidator
{
    internal static string? Validate(SeoRequest request)
    {
        string title = request.Title ?? string.Empty;
        string description = request.Description ?? string.Empty;
        string socialTitle = request.SocialTitle ?? string.Empty;
        string socialDescription = request.SocialDescription ?? string.Empty;
        string canonical = request.CanonicalUrl ?? string.Empty;
        string imageUrl = request.ImageUrl ?? string.Empty;

        if (title.Length > 200 || socialTitle.Length > 200)
        {
            return "SEO and social titles allow at most 200 characters.";
        }

        if (description.Length > 500 || socialDescription.Length > 500)
        {
            return "SEO and social descriptions allow at most 500 characters.";
        }

        if (request.SocialImageMediaId.HasValue &&
            !string.IsNullOrWhiteSpace(imageUrl))
        {
            return "Choose either a media-library image or an external social image URL, not both.";
        }

        if (!string.IsNullOrWhiteSpace(canonical) &&
            !SeoUrlPolicy.IsSafeAbsoluteHttpUrl(canonical))
        {
            return "Canonical URL must be an absolute HTTP/HTTPS URL without userinfo, fragments, backslashes or control characters.";
        }

        if (!string.IsNullOrWhiteSpace(imageUrl) &&
            !SeoUrlPolicy.IsSafeAbsoluteHttpUrl(imageUrl))
        {
            return "External social image must be an absolute HTTP/HTTPS URL without userinfo, fragments, backslashes or control characters.";
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? imageUri) &&
            imageUri.AbsolutePath.Contains(
                "/api/spell-icons/",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Use the media library for /api/spell-icons/ images so their dependency can be protected.";
        }

        if (!request.PageId.HasValue)
        {
            if (!SeoRouteRegistry.TryResolveStaticTarget(
                    request.Path,
                    out SeoRouteDefinition? route,
                    out string? routeError))
            {
                return routeError;
            }

            if (route?.Route == "*" && !string.IsNullOrWhiteSpace(canonical))
            {
                return "Global SEO defaults cannot define a canonical URL. Canonical is resolved per page.";
            }
        }

        return null;
    }
}
