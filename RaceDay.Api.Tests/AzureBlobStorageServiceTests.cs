
using Microsoft.AspNetCore.Http;
using RaceDay.Api.Services;
using Xunit;

namespace RaceDay.Api.Tests;

public class AzureBlobStorageServiceTests
{
    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public void ValidateImage_WithValidSignature_DoesNotThrow(
        string contentType)
    {
        var file = CreateFormFile(
            GetValidHeader(contentType),
            contentType);

        var exception = Record.Exception(
            () => AzureBlobStorageService.ValidateImage(file));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public void ValidateImage_WithMismatchedBytes_ThrowsArgumentException(
        string contentType)
    {
        using var stream = new MemoryStream(
            new byte[] { 1, 2, 3, 4 });

        var file = CreateFormFile(
            stream,
            "test-image",
            contentType);

        Assert.Throws<ArgumentException>(
            () => AzureBlobStorageService.ValidateImage(file));
    }

    [Fact]
    public void ValidateImage_WithUnsupportedContentType_ThrowsArgumentException()
    {
        using var stream = new MemoryStream(
            new byte[] { 1, 2, 3, 4 });

        var file = CreateFormFile(
            stream,
            "test.txt",
            "text/plain");

        Assert.Throws<ArgumentException>(
            () => AzureBlobStorageService.ValidateImage(file));
    }

    [Fact]
    public void ValidateImage_WithEmptyFile_ThrowsArgumentException()
    {
        using var stream = new MemoryStream();

        var file = CreateFormFile(
            stream,
            "empty.png",
            "image/png");

        Assert.Throws<ArgumentException>(
            () => AzureBlobStorageService.ValidateImage(file));
    }

    [Fact]
    public void ValidateImage_WithFileOverFiveMB_ThrowsArgumentException()
    {
        using var stream = new MemoryStream(
            new byte[5 * 1024 * 1024 + 1]);

        var file = CreateFormFile(
            stream,
            "large.png",
            "image/png");

        Assert.Throws<ArgumentException>(
            () => AzureBlobStorageService.ValidateImage(file));
    }

    private static byte[] GetValidHeader(string contentType)
    {
        return contentType switch
        {
            "image/png" =>
            [
                0x89, 0x50, 0x4E, 0x47,
                0x0D, 0x0A, 0x1A, 0x0A
            ],

            "image/jpeg" =>
            [
                0xFF, 0xD8, 0xFF, 0xD9
            ],

            "image/webp" =>
            [
                (byte)'R', (byte)'I', (byte)'F', (byte)'F',
                0x00, 0x00, 0x00, 0x00,
                (byte)'W', (byte)'E', (byte)'B', (byte)'P'
            ],

            _ => throw new ArgumentException(
                "Unsupported test content type.",
                nameof(contentType))
        };
    }

    private static FormFile CreateFormFile(
        byte[] bytes,
        string contentType)
    {
        var stream = new MemoryStream(bytes);

        return CreateFormFile(
            stream,
            "test-image",
            contentType);
    }

    private static FormFile CreateFormFile(
        Stream stream,
        string fileName,
        string contentType)
    {
        return new FormFile(
            stream,
            0,
            stream.Length,
            "File",
            fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
