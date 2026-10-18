
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using Xunit;

namespace RaceDay.Api.Tests;

public class EnrolmentTests : IDisposable
{
    private readonly RaceDayApiFactory _factory =
        new RaceDayApiFactory();

    private const string SecondParticipantEmail =
        "test.participant2@example.com";

    private const string SecondOrganiserEmail =
        "test.organiser2@example.com";

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email,
                password = RaceDayApiFactory.TestPassword
            });

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"Login failed for {email}. " +
            $"Status: {(int)response.StatusCode}. Response: {body}");

        return client;
    }

    private async Task<int> CreateEnrolmentAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/events/1/enrolments",
            new { categoryId = 1 });

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created, received " +
            $"{(int)response.StatusCode}. Response: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement
            .GetProperty("enrolmentId")
            .GetInt32();
    }

    private async Task AddSecondParticipantAsync()
    {
        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        var hasher = new PasswordHasher<User>();

        var participant = new User
        {
            UserId = 3,
            RoleId = 2,
            FirstName = "Second",
            LastName = "Participant",
            Email = SecondParticipantEmail,
            CreatedAt = DateTime.UtcNow
        };

        participant.PasswordHash =
            hasher.HashPassword(
                participant,
                RaceDayApiFactory.TestPassword);

        db.Users.Add(participant);
        await db.SaveChangesAsync();
    }

    private async Task AddSecondOrganiserAsync()
    {
        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        var hasher = new PasswordHasher<User>();

        var organiser = new User
        {
            UserId = 3,
            RoleId = 1,
            FirstName = "Second",
            LastName = "Organiser",
            Email = SecondOrganiserEmail,
            CreatedAt = DateTime.UtcNow
        };

        organiser.PasswordHash =
            hasher.HashPassword(
                organiser,
                RaceDayApiFactory.TestPassword);

        db.Users.Add(organiser);
        await db.SaveChangesAsync();
    }

    private async Task AddPastEventAsync()
    {
        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        db.Events.Add(new Event
        {
            EventId = 2,
            OrganiserId = 1,
            EventName = "Past Test Event",
            Description = "Event used to test past-event enrolment",
            EventDate = DateTime.Today.AddDays(-1),
            Location = "Johannesburg",
            DistanceKm = 5,
            EventType = "Run",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateEventEnrolment_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/events/1/enrolments",
            new { categoryId = 1 });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateEventEnrolment_AsOrganiser_ReturnsForbidden()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.PostAsJsonAsync(
            "/api/events/1/enrolments",
            new { categoryId = 1 });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateEventEnrolment_ForMissingEvent_ReturnsNotFound()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await client.PostAsJsonAsync(
            "/api/events/99999/enrolments",
            new { categoryId = 1 });

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateEventEnrolment_WithValidData_ReturnsCreated()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        await CreateEnrolmentAsync(client);
    }

    [Fact]
    public async Task CreateEventEnrolment_ForPastEvent_ReturnsBadRequest()
    {
        await AddPastEventAsync();

        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await client.PostAsJsonAsync(
            "/api/events/2/enrolments",
            new { categoryId = 1 });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateEnrolment_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/enrolments",
            new
            {
                eventId = 1,
                categoryId = 1
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateEnrolment_AsOrganiser_ReturnsForbidden()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.PostAsJsonAsync(
            "/api/enrolments",
            new
            {
                eventId = 1,
                categoryId = 1
            });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateEnrolment_ForMissingEvent_ReturnsNotFound()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await client.PostAsJsonAsync(
            "/api/enrolments",
            new
            {
                eventId = 99999,
                categoryId = 1
            });

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateEnrolment_WithCategoryFromAnotherEvent_ReturnsBadRequest()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await client.PostAsJsonAsync(
            "/api/enrolments",
            new
            {
                eventId = 1,
                categoryId = 99999
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateEnrolment_WithValidData_ReturnsCreated()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        await CreateEnrolmentAsync(client);
    }

    [Fact]
    public async Task CreateEnrolment_TwiceForSameEvent_ReturnsConflict()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var request = new
        {
            eventId = 1,
            categoryId = 1
        };

        var firstResponse = await client.PostAsJsonAsync(
            "/api/enrolments",
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync(
            "/api/enrolments",
            request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondResponse.StatusCode);
    }

    [Fact]
    public async Task GetMyEnrolments_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/api/enrolments/mine");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetMyEnrolments_AsOrganiser_ReturnsForbidden()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.GetAsync(
            "/api/enrolments/mine");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task GetMyEnrolments_AfterEnrolling_ReturnsOwnEnrolment()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        await CreateEnrolmentAsync(client);

        var response = await client.GetAsync(
            "/api/enrolments/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Test RaceDay Event", body);
        Assert.Contains("Test Age Category", body);
    }

    [Fact]
    public async Task GetEnrolment_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/api/enrolments/1");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetEnrolment_ForMissingId_ReturnsNotFound()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await client.GetAsync(
            "/api/enrolments/99999");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetEnrolment_AsOwner_ReturnsOk()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var enrolmentId = await CreateEnrolmentAsync(client);

        var response = await client.GetAsync(
            $"/api/enrolments/{enrolmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEnrolment_AsAnotherParticipant_ReturnsForbidden()
    {
        await AddSecondParticipantAsync();

        using var ownerClient = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var enrolmentId =
            await CreateEnrolmentAsync(ownerClient);

        using var otherClient = await LoginAsync(
            SecondParticipantEmail);

        var response = await otherClient.GetAsync(
            $"/api/enrolments/{enrolmentId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task GetEnrolment_AsOrganiserWhoDoesNotOwnEvent_ReturnsForbidden()
    {
        await AddSecondOrganiserAsync();

        using var participantClient = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var enrolmentId =
            await CreateEnrolmentAsync(participantClient);

        using var otherOrganiserClient = await LoginAsync(
            SecondOrganiserEmail);

        var response = await otherOrganiserClient.GetAsync(
            $"/api/enrolments/{enrolmentId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task DeleteEnrolment_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.DeleteAsync(
            "/api/enrolments/1");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task DeleteEnrolment_AsOrganiser_ReturnsForbidden()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.DeleteAsync(
            "/api/enrolments/1");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task DeleteEnrolment_ForMissingId_ReturnsNotFound()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await client.DeleteAsync(
            "/api/enrolments/99999");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task DeleteEnrolment_AsOwner_ReturnsNoContent()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var enrolmentId =
            await CreateEnrolmentAsync(client);

        var response = await client.DeleteAsync(
            $"/api/enrolments/{enrolmentId}");

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        var getResponse = await client.GetAsync(
            $"/api/enrolments/{enrolmentId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteEnrolment_AsAnotherParticipant_ReturnsForbidden()
    {
        await AddSecondParticipantAsync();

        using var ownerClient = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var enrolmentId =
            await CreateEnrolmentAsync(ownerClient);

        using var otherClient = await LoginAsync(
            SecondParticipantEmail);

        var response = await otherClient.DeleteAsync(
            $"/api/enrolments/{enrolmentId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task DeleteEnrolment_WithRecordedResult_ReturnsConflict()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var enrolmentId =
            await CreateEnrolmentAsync(client);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RaceDayDbContext>();

            db.Results.Add(new Result
            {
                EnrolmentId = enrolmentId,
                FinishPosition = 1,
                FinishTime = new TimeSpan(0, 35, 42),
                RecordedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        var response = await client.DeleteAsync(
            $"/api/enrolments/{enrolmentId}");

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
