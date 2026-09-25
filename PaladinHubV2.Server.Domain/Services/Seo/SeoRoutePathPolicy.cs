namespace PaladinHubV2.Server.Domain.Services.Seo;

public sealed class SeoRoutePathPolicy :
    ISeoRoutePathPolicy
{
    public bool TryNormalizePath(
        string? value,
        bool allowGlobal,
        out string normalized,
        out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "A route is required.";
            return false;
        }

        string candidate = value.Trim();

        if (allowGlobal &&
            candidate == "*")
        {
            normalized = "*";
            return true;
        }

        if (candidate.Length > 2048 ||
            candidate.Any(char.IsControl))
        {
            error =
                "The route is too long or contains control characters.";
            return false;
        }

        if (!candidate.StartsWith('/') ||
            candidate.StartsWith(
                "//",
                StringComparison.Ordinal) ||
            candidate.Contains(
                "//",
                StringComparison.Ordinal) ||
            candidate.Contains('\\') ||
            candidate.Contains('?') ||
            candidate.Contains('#') ||
            candidate.Contains('%'))
        {
            error =
                "The route must be a plain local path without encoding, query, fragment, backslashes or repeated separators.";
            return false;
        }

        candidate =
            candidate.Length > 1
                ? candidate.TrimEnd('/')
                : candidate;

        if (candidate == "/")
        {
            normalized = candidate;
            return true;
        }

        string[] segments =
            candidate.Split(
                '/',
                StringSplitOptions.None);

        if (segments
            .Skip(1)
            .Any(segment =>
                string.IsNullOrEmpty(segment) ||
                segment is "." or ".."))
        {
            error =
                "The route contains an invalid path segment.";
            return false;
        }

        normalized = candidate;
        return true;
    }

    public bool TryBuildDatabasePagePath(
        string section,
        string slug,
        out string path,
        out string? error)
    {
        return TryNormalizePath(
            $"/{section.Trim('/')}/{slug.Trim('/')}",
            allowGlobal: false,
            out path,
            out error);
    }
}
