
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using Xunit;

namespace RaceDay.Api.Tests;

public class EventBannerUploadTests
    : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public EventBannerUploadTests(RaceDayApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadBanner_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        using var content = CreateImageContent("image/png");

        var response = await client.PostAsync(
            "/api/events/1/banner",
            content);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_AsParticipant_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();

        await Login(client, RaceDayApiFactory.ParticipantEmail);

        using var content = CreateImageContent("image/png");

        var response = await client.PostAsync(
            "/api/events/1/banner",
            content);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WithInvalidContentType_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        await Login(client, RaceDayApiFactory.OrganiserEmail);

        using var content = CreateImageContent("text/plain");

        var response = await client.PostAsync(
            "/api/events/1/banner",
            content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_AsOwner_SavesBannerUrl()
    {
        using var client = _factory.CreateClient();

        await Login(client, RaceDayApiFactory.OrganiserEmail);

        using var content = CreateImageContent("image/png");

        var response = await client.PostAsync(
            "/api/events/1/banner",
            content);

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK but received HTTP {(int)response.StatusCode}. Response: {responseBody}");

        using var json = JsonDocument.Parse(responseBody);

        var returnedUrl = json.RootElement
            .GetProperty("bannerImageUrl")
            .GetString();

        const string expectedUrl =
            "https://test.example.com/event-banners/test-image.png";

        Assert.Equal(expectedUrl, returnedUrl);

        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        var eventItem = await db.Events
            .AsNoTracking()
            .SingleAsync(e => e.EventId == 1);

        Assert.Equal(expectedUrl, eventItem.BannerImageUrl);
    }

    [Fact]
    public async Task UploadBanner_WhenStorageIsUnconfigured_ReturnsServiceUnavailable()
    {
        var storage = _factory.Services
            .GetRequiredService<FakeAzureBlobStorageService>();

        storage.UploadException = new InvalidOperationException(
            "Azure Blob Storage is not configured.");

        try
        {
            using var client = _factory.CreateClient();

            await Login(client, RaceDayApiFactory.OrganiserEmail);

            using var content = CreateImageContent("image/png");

            var response = await client.PostAsync(
                "/api/events/1/banner",
                content);

            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.ServiceUnavailable,
                $"Expected HTTP 503 but received HTTP {(int)response.StatusCode}. Response: {responseBody}");

            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<RaceDayDbContext>();

            var eventItem = await db.Events
                .AsNoTracking()
                .SingleAsync(e => e.EventId == 1);

            Assert.Null(eventItem.BannerImageUrl);
        }
        finally
        {
            storage.UploadException = null;
        }
    }

    [Fact]
    public async Task UploadBanner_WhenAzureStorageFails_ReturnsServiceUnavailable()
    {
        var storage = _factory.Services
            .GetRequiredService<FakeAzureBlobStorageService>();

        string? originalBannerUrl;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RaceDayDbContext>();

            var eventItem = await db.Events
                .AsNoTracking()
                .SingleAsync(e => e.EventId == 1);

            originalBannerUrl = eventItem.BannerImageUrl;
        }

        storage.UploadException = new RequestFailedException(
            503,
            "Simulated Azure Storage failure.");

        try
        {
            using var client = _factory.CreateClient();

            await Login(client, RaceDayApiFactory.OrganiserEmail);

            using var content = CreateImageContent("image/png");

            var response = await client.PostAsync(
                "/api/events/1/banner",
                content);

            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.ServiceUnavailable,
                $"Expected HTTP 503 but received HTTP {(int)response.StatusCode}. Response: {responseBody}");

            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<RaceDayDbContext>();

            var eventItem = await db.Events
                .AsNoTracking()
                .SingleAsync(e => e.EventId == 1);

            Assert.Equal(
                originalBannerUrl,
                eventItem.BannerImageUrl);
        }
        finally
        {
            storage.UploadException = null;
        }
    }

    [Fact]
    public async Task UploadBanner_ForMissingEvent_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        await Login(client, RaceDayApiFactory.OrganiserEmail);

        using var content = CreateImageContent("image/png");

        var response = await client.PostAsync(
            "/api/events/99999/banner",
            content);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_AsAnotherOrganiser_ReturnsForbidden()
    {
        const string secondOrganiserEmail =
            "second.organiser.banner@example.com";

        int secondOrganiserId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RaceDayDbContext>();

            var existingUser = await db.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == secondOrganiserEmail);

            if (existingUser == null)
            {
                var organiserRole = await db.Roles
                    .FirstAsync(r => r.RoleName == "Organiser");

                var user = new User
                {
                    RoleId = organiserRole.RoleId,
                    FirstName = "Second",
                    LastName = "Organiser",
                    Email = secondOrganiserEmail,
                    CreatedAt = DateTime.UtcNow
                };

                user.PasswordHash = new PasswordHasher<User>()
                    .HashPassword(
                        user,
                        RaceDayApiFactory.TestPassword);

                db.Users.Add(user);
                await db.SaveChangesAsync();

                secondOrganiserId = user.UserId;
            }
            else
            {
                secondOrganiserId = existingUser.UserId;
            }

            // The second organiser has a separate event.
            var secondEventExists = await db.Events.AnyAsync(e =>
                e.OrganiserId == secondOrganiserId);

            if (!secondEventExists)
            {
                db.Events.Add(new Event
                {
                    OrganiserId = secondOrganiserId,
                    EventName = $"Second Organiser Event {Guid.NewGuid():N}",
                    Description = "Event for banner ownership testing",
                    EventDate = new DateTime(2027, 12, 10),
                    Location = "Johannesburg",
                    DistanceKm = 5,
                    EventType = "Run",
                    CreatedAt = DateTime.UtcNow
                });

                await db.SaveChangesAsync();
            }
        }

        using var client = _factory.CreateClient();

        await Login(client, secondOrganiserEmail);

        using var content = CreateImageContent("image/png");

        var response = await client.PostAsync(
            "/api/events/1/banner",
            content);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    private static async Task Login(
        HttpClient client,
        string email)
    {
        using var loginContent = new StringContent(
            JsonSerializer.Serialize(new
            {
                email,
                password = RaceDayApiFactory.TestPassword
            }));

        loginContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/json");

        var response = await client.PostAsync(
            "/api/auth/login",
            loginContent);

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Login failed with HTTP {(int)response.StatusCode}. Response: {body}");
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

        multipart.Add(
            fileContent,
            "File",
            "test-banner.png");

        return multipart;
    }
}
