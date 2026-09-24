using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PaladinHubV2.Server.Domain.Services.Seo;

internal static class SeoUrlPolicy
{
    internal static bool IsSafeAbsoluteHttpUrl(string value)
    {
        return value.Length <= 2048 &&
               !value.Any(char.IsControl) &&
               !value.Contains('\\') &&
               Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
               uri.Scheme is "http" or "https" &&
               !string.IsNullOrWhiteSpace(uri.Host) &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               string.IsNullOrEmpty(uri.Fragment);
    }

    internal static string NormalizeOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return string.Empty;
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    internal static string CombineOriginAndPath(string origin, string path)
    {
        return string.IsNullOrWhiteSpace(origin)
            ? string.Empty
            : origin.TrimEnd('/') + (path.StartsWith('/') ? path : "/" + path);
    }

    internal static string BuildSnapshotVersion(
        IEnumerable<SeoPublicEntry> entries,
        IEnumerable<SeoPublicPage> pages,
        IEnumerable<string> staticRoutes,
        string siteUrl,
        string apiOrigin)
    {
        string payload = JsonSerializer.Serialize(new
        {
            SiteUrl = siteUrl,
            ApiOrigin = apiOrigin,
            Registry = SeoRouteRegistry.Version,
            Entries = entries.OrderBy(entry => entry.Id),
            Pages = pages.OrderBy(page => page.Id),
            Routes = staticRoutes.OrderBy(route => route, StringComparer.Ordinal)
        });

        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }

    internal static Guid ResourceId(string path)
    {
        return new Guid(
            SHA256.HashData(Encoding.UTF8.GetBytes("resource:" + path))[..16]);
    }
}
