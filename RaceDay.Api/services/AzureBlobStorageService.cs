
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

public class AzureBlobStorageService : IAzureBlobStorageService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

    private readonly BlobServiceClient? _blobServiceClient;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString =
            configuration["AzureBlobStorage:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(connectionString) &&
            !connectionString.Contains(
                "PLACEHOLDER",
                StringComparison.OrdinalIgnoreCase))
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
        }
    }

    public async Task<string> UploadImageAsync(
        IFormFile file,
        string containerName,
        CancellationToken cancellationToken = default)
    {
        ValidateImage(file);

        if (_blobServiceClient is null)
        {
            throw new InvalidOperationException(
                "Azure Blob Storage is not configured. Set a valid " +
                "AzureBlobStorage:ConnectionString before uploading images.");
        }

        var container =
            _blobServiceClient.GetBlobContainerClient(containerName);

        await container.CreateIfNotExistsAsync(
            PublicAccessType.None,
            cancellationToken: cancellationToken);

        var extension = GetExtension(file.ContentType);
        var blobName = $"{Guid.NewGuid():N}{extension}";
        var blob = container.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();

        await blob.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = file.ContentType
                }
            },
            cancellationToken);

        return blob.Uri.ToString();
    }

    public static void ValidateImage(IFormFile? file)
    {
        if (file is null || file.Length <= 0)
        {
            throw new ArgumentException("An image file is required.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException(
                "The image must not exceed 5 MB.");
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            throw new ArgumentException(
                "Only JPEG, PNG, and WebP images are allowed.");
        }

        using var stream = file.OpenReadStream();

        Span<byte> header = stackalloc byte[12];
        var bytesRead = 0;

        while (bytesRead < header.Length)
        {
            var read = stream.Read(header[bytesRead..]);

            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        var isValidImage = file.ContentType.ToLowerInvariant() switch
        {
            "image/png" => IsPng(header, bytesRead),
            "image/jpeg" => IsJpeg(header, bytesRead),
            "image/webp" => IsWebP(header, bytesRead),
            _ => false
        };

        if (!isValidImage)
        {
            throw new ArgumentException(
                "The file contents do not match the declared image type.");
        }
    }

    private static bool IsPng(ReadOnlySpan<byte> header, int bytesRead)
    {
        return bytesRead >= 8 &&
               header[0] == 0x89 &&
               header[1] == 0x50 &&
               header[2] == 0x4E &&
               header[3] == 0x47 &&
               header[4] == 0x0D &&
               header[5] == 0x0A &&
               header[6] == 0x1A &&
               header[7] == 0x0A;
    }

    private static bool IsJpeg(ReadOnlySpan<byte> header, int bytesRead)
    {
        return bytesRead >= 3 &&
               header[0] == 0xFF &&
               header[1] == 0xD8 &&
               header[2] == 0xFF;
    }

    private static bool IsWebP(ReadOnlySpan<byte> header, int bytesRead)
    {
        return bytesRead >= 12 &&
               header[0] == (byte)'R' &&
               header[1] == (byte)'I' &&
               header[2] == (byte)'F' &&
               header[3] == (byte)'F' &&
               header[8] == (byte)'W' &&
               header[9] == (byte)'E' &&
               header[10] == (byte)'B' &&
               header[11] == (byte)'P';
    }

    private static string GetExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new ArgumentException(
                "Unsupported image content type.")
        };
    }
}
