
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Services;
using Xunit;

namespace RaceDay.Api.Tests;

public class ProfilePictureUploadTests
    : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public ProfilePictureUploadTests(RaceDayApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadProfilePicture_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        using var content = CreateImageContent("image/png");

        var response = await client.PostAsync(
            "/api/users/me/profile-picture",
            content);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task UploadProfilePicture_WithInvalidContentType_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        await LoginAsParticipant(client);

        using var content = CreateImageContent("text/plain");

        var response = await client.PostAsync(
            "/api/users/me/profile-picture",
            content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task UploadProfilePicture_WithValidImage_SavesPictureUrl()
    {
        using var client = _factory.CreateClient();

        await LoginAsParticipant(client);

        using var content = CreateImageContent("image/png");

        var response = await client.PostAsync(
            "/api/users/me/profile-picture",
            content);

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK, received {(int)response.StatusCode}. {responseBody}");

        using var json = JsonDocument.Parse(responseBody);

        var returnedUrl = json.RootElement
            .GetProperty("profilePictureUrl")
            .GetString();

        Assert.Equal(
            RaceDayApiFactory.FakeProfilePictureUrl,
            returnedUrl);

        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        var participant = await db.Users
            .AsNoTracking()
            .SingleAsync(
                u => u.Email == RaceDayApiFactory.ParticipantEmail);

        Assert.Equal(
            RaceDayApiFactory.FakeProfilePictureUrl,
            participant.ProfilePictureUrl);
    }

    [Fact]
    public async Task UploadProfilePicture_WhenStorageIsUnconfigured_ReturnsServiceUnavailable()
    {
        var storage = _factory.Services
            .GetRequiredService<FakeAzureBlobStorageService>();

        storage.UploadException = new InvalidOperationException(
            "Azure Blob Storage is not configured.");

        try
        {
            using var client = _factory.CreateClient();

            await LoginAsParticipant(client);

            using var content = CreateImageContent("image/png");

            var response = await client.PostAsync(
                "/api/users/me/profile-picture",
                content);

            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.ServiceUnavailable,
                $"Expected HTTP 503 but received HTTP {(int)response.StatusCode}. Response: {responseBody}");
        }
        finally
        {
            storage.UploadException = null;
        }
    }

    [Fact]
    public async Task UploadProfilePicture_WhenAzureStorageFails_ReturnsServiceUnavailableAndPreservesExistingUrl()
    {
        var storage = _factory.Services
            .GetRequiredService<FakeAzureBlobStorageService>();

        string? originalPictureUrl;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RaceDayDbContext>();

            var participant = await db.Users
                .AsNoTracking()
                .SingleAsync(
                    u => u.Email == RaceDayApiFactory.ParticipantEmail);

            originalPictureUrl = participant.ProfilePictureUrl;
        }

        storage.UploadException = new RequestFailedException(
            503,
            "Simulated Azure Storage failure.");

        try
        {
            using var client = _factory.CreateClient();

            await LoginAsParticipant(client);

            using var content = CreateImageContent("image/png");

            var response = await client.PostAsync(
                "/api/users/me/profile-picture",
                content);

            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.ServiceUnavailable,
                $"Expected HTTP 503 but received HTTP {(int)response.StatusCode}. Response: {responseBody}");

            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<RaceDayDbContext>();

            var participant = await db.Users
                .AsNoTracking()
                .SingleAsync(
                    u => u.Email == RaceDayApiFactory.ParticipantEmail);

            Assert.Equal(
                originalPictureUrl,
                participant.ProfilePictureUrl);
        }
        finally
        {
            storage.UploadException = null;
        }
    }

    private static async Task LoginAsParticipant(HttpClient client)
    {
        using var loginContent = new StringContent(
            JsonSerializer.Serialize(new
            {
                email = RaceDayApiFactory.ParticipantEmail,
                password = RaceDayApiFactory.TestPassword
            }));

        loginContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/json");

        var loginResponse = await client.PostAsync(
            "/api/auth/login",
            loginContent);

        var body = await loginResponse.Content.ReadAsStringAsync();

        Assert.True(
            loginResponse.StatusCode == HttpStatusCode.OK,
            $"Login failed with HTTP {(int)loginResponse.StatusCode}. Response: {body}");
    }

    private static MultipartFormDataContent CreateImageContent(
        string contentType)
    {
        var multipart = new MultipartFormDataContent();

        byte[] imageBytes = contentType == "image/png"
            ? new byte[]
            {
                0x89, 0x50, 0x4E, 0x47,
                0x0D, 0x0A, 0x1A, 0x0A
            }
            : new byte[] { 1, 2, 3, 4 };

        var fileContent = new ByteArrayContent(imageBytes);

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue(contentType);

        multipart.Add(fileContent, "File", "test-image.png");

        return multipart;
    }
}
