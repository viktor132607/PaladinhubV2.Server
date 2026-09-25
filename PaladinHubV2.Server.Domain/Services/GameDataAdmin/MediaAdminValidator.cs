using PaladinHubV2.Server.Common.Models.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed class MediaAdminValidator :
    IMediaAdminValidator
{
    public string? Validate(
        MediaRequest request)
    {
        return string.IsNullOrWhiteSpace(request.Name)
            ? "Name is required."
            : null;
    }
}
