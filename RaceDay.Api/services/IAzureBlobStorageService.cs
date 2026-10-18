
using Microsoft.AspNetCore.Http;

namespace RaceDay.Api.Services;

public interface IAzureBlobStorageService
{
    Task<string> UploadImageAsync(
        IFormFile file,
        string containerName,
        CancellationToken cancellationToken = default);
}
