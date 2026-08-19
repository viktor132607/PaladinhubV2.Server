using Microsoft.AspNetCore.Http;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.Common;

namespace PaladinHubV2.Server.Domain.Services.Avatars
{
    public interface IAvatarService
    {
        Task<OperationResult> SetDefaultAvatar(User user, string file);
        Task<OperationResult> UploadAvatar(User user, IFormFile file);
        Task<OperationResult> SetUploadedAvatar(User user, string path);
        Task<OperationResult> DeleteUpload(User user, string path);
    }
}
