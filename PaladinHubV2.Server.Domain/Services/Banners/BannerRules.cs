namespace PaladinHubV2.Server.Domain.Services.Banners;

public sealed class BannerRules : IBannerRules
{
    private static readonly HashSet<string> AllowedKinds =
        new(StringComparer.Ordinal)
        {
            "information",
            "success",
            "warning"
        };

    private static readonly HashSet<string> AllowedPositions =
        new(StringComparer.Ordinal)
        {
            "above-navbar",
            "below-navbar",
            "above-content"
        };

    private static readonly string[] PrivateScopePrefixes =
    [
        "admin",
        "api",
        "account",
        "checkout",
        "cart",
        "login",
        "register"
    ];

    public string NormalizePath(string? path)
    {
        string value = string.IsNullOrWhiteSpace(path)
            ? "/"
            : path.Trim();

        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        if (value.Length > 1)
        {
            value = value.TrimEnd('/');
        }

        return value;
    }

    public string? Validate(
        BannerRequest request,
        bool creating)
    {
        if (string.IsNullOrWhiteSpace(request.InternalName) ||
            request.InternalName.Trim().Length > 100)
        {
            return "Internal name is required and limited to 100 characters.";
        }

        if (string.IsNullOrWhiteSpace(request.Title) ||
            request.Title.Trim().Length > 200)
        {
            return "Title is required and limited to 200 characters.";
        }

        if (string.IsNullOrWhiteSpace(request.Text) ||
            request.Text.Length > 10000)
        {
            return "Text is required and limited to 10000 characters.";
        }

        if ((request.AltText?.Length ?? 0) > 300 ||
            (request.ButtonText?.Length ?? 0) > 120 ||
            (request.ImageUrl?.Length ?? 0) > 2048 ||
            (request.ButtonUrl?.Length ?? 0) > 2048)
        {
            return "One or more values exceed their maximum length.";
        }

        string kind =
            request.Kind?.Trim().ToLowerInvariant() ??
            string.Empty;

        if (!AllowedKinds.Contains(kind))
        {
            return "Kind must be information, success or warning.";
        }

        string position =
            request.Position?.Trim().ToLowerInvariant() ??
            string.Empty;

        if (!AllowedPositions.Contains(position))
        {
            return "Position is invalid.";
        }

        if (request.StartAtUtc.HasValue &&
            request.EndAtUtc.HasValue &&
            request.EndAtUtc.Value.ToUniversalTime() <
            request.StartAtUtc.Value.ToUniversalTime())
        {
            return "End date cannot be before start date.";
        }

        if (string.IsNullOrWhiteSpace(request.ButtonUrl) !=
            string.IsNullOrWhiteSpace(request.ButtonText))
        {
            return "Button text and URL must be provided together.";
        }

        if (!string.IsNullOrWhiteSpace(request.ButtonUrl) &&
            !SafeUrl(request.ButtonUrl))
        {
            return "Button URL must be an internal path or an HTTP/HTTPS URL.";
        }

        if (!string.IsNullOrWhiteSpace(request.ImageUrl) &&
            !SafeUrl(request.ImageUrl))
        {
            return "Image URL must be an internal path or an HTTP/HTTPS URL.";
        }

        if (request.Pages is { Count: > 200 })
        {
            return "A banner can target at most 200 pages.";
        }

        if (request.Pages is not null &&
            request.Pages.Any(IsInvalidScope))
        {
            return "Page scopes must be public paths beginning with a single slash.";
        }

        if (!creating && request.Version < 1)
        {
            return "Version is required.";
        }

        return null;
    }

    public bool IsVisible(
        BannerDto banner,
        string path,
        DateTimeOffset now)
    {
        return
            !banner.IsDeleted &&
            !banner.IsArchived &&
            banner.IsActive &&
            (banner.Pages.Count == 0 ||
             banner.Pages.Any(page =>
                 string.Equals(
                     NormalizePath(page),
                     NormalizePath(path),
                     StringComparison.OrdinalIgnoreCase))) &&
            (banner.StartAtUtc is null ||
             banner.StartAtUtc <= now) &&
            (banner.EndAtUtc is null ||
             banner.EndAtUtc > now);
    }

    public BannerDto Create(
        BannerRequest request,
        DateTimeOffset now)
    {
        return new BannerDto(
            Guid.NewGuid(),
            request.InternalName.Trim(),
            request.Title.Trim(),
            request.Text.Trim(),
            Null(request.ImageUrl),
            request.AltText?.Trim() ?? string.Empty,
            Null(request.ButtonText),
            Null(request.ButtonUrl),
            request.Kind.Trim().ToLowerInvariant(),
            request.Position.Trim().ToLowerInvariant(),
            NormalizePages(request.Pages),
            request.StartAtUtc?.ToUniversalTime(),
            request.EndAtUtc?.ToUniversalTime(),
            request.SortOrder,
            request.IsDismissible,
            request.IsActive,
            false,
            false,
            1,
            now,
            now);
    }

    public BannerDto Update(
        BannerDto current,
        BannerRequest request,
        DateTimeOffset now)
    {
        return current with
        {
            InternalName =
                request.InternalName.Trim(),
            Title = request.Title.Trim(),
            Text = request.Text.Trim(),
            ImageUrl = Null(request.ImageUrl),
            AltText =
                request.AltText?.Trim() ??
                string.Empty,
            ButtonText =
                Null(request.ButtonText),
            ButtonUrl =
                Null(request.ButtonUrl),
            Kind =
                request.Kind.Trim()
                    .ToLowerInvariant(),
            Position =
                request.Position.Trim()
                    .ToLowerInvariant(),
            Pages =
                NormalizePages(request.Pages),
            StartAtUtc =
                request.StartAtUtc?
                    .ToUniversalTime(),
            EndAtUtc =
                request.EndAtUtc?
                    .ToUniversalTime(),
            SortOrder = request.SortOrder,
            IsDismissible =
                request.IsDismissible,
            IsActive = request.IsActive,
            Version = current.Version + 1,
            UpdatedAtUtc = now
        };
    }

    public BannerRequest ToRequest(
        BannerDto banner,
        int version)
    {
        return new BannerRequest(
            banner.InternalName,
            banner.Title,
            banner.Text,
            banner.ImageUrl,
            banner.AltText,
            banner.ButtonText,
            banner.ButtonUrl,
            banner.Kind,
            banner.Position,
            banner.Pages,
            banner.StartAtUtc,
            banner.EndAtUtc,
            banner.SortOrder,
            banner.IsDismissible,
            banner.IsActive,
            version);
    }

    private bool IsInvalidScope(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        string trimmed = path.Trim();

        if (!PublicPath(trimmed) ||
            trimmed.Contains('?') ||
            trimmed.Contains('#'))
        {
            return true;
        }

        string firstSegment =
            trimmed.TrimStart('/')
                .Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ??
            string.Empty;

        return PrivateScopePrefixes.Contains(
            firstSegment,
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool PublicPath(string path)
    {
        return
            path.Length <= 2048 &&
            path.StartsWith('/') &&
            !path.StartsWith("//") &&
            !path.Contains('\\') &&
            !path.Any(char.IsControl);
    }

    private static bool SafeUrl(string value)
    {
        if (value.Any(char.IsControl) ||
            value.Contains('\\'))
        {
            return false;
        }

        value = value.Trim();

        if (PublicPath(value))
        {
            return true;
        }

        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out Uri? uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https";
    }

    private IReadOnlyList<string> NormalizePages(
        IReadOnlyList<string>? pages)
    {
        return pages?
            .Where(page =>
                !string.IsNullOrWhiteSpace(page))
            .Select(NormalizePath)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(page => page)
            .ToArray() ??
            [];
    }

    private static string? Null(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
