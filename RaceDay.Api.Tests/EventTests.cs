
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using Xunit;

namespace RaceDay.Api.Tests;

public class EventTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    private const string SecondOrganiserEmail =
        "second.event.organiser@raceday.test";

    public EventTests(RaceDayApiFactory factory)
    {
        _factory = factory;
    }

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
            $"Login failed: {(int)response.StatusCode}. {body}");

        return client;
    }

    private Task<HttpClient> LoginAsOrganiserAsync() =>
        LoginAsync(RaceDayApiFactory.OrganiserEmail);

    private Task<HttpClient> LoginAsParticipantAsync() =>
        LoginAsync(RaceDayApiFactory.ParticipantEmail);

    private static object EventRequest(
        string eventName,
        string location = "Event Test Location",
        string eventType = "Run",
        string eventDate = "2027-12-10",
        decimal distanceKm = 10)
    {
        return new
        {
            eventName,
            description = "Event created during integration testing",
            eventDate,
            location,
            distanceKm,
            eventType
        };
    }

    private async Task<int> CreateEventAsync(
        HttpClient client,
        string eventName,
        string location = "Event Test Location",
        string eventType = "Run",
        string eventDate = "2027-12-10",
        decimal distanceKm = 10)
    {
        var response = await client.PostAsJsonAsync(
            "/api/events",
            EventRequest(
                eventName,
                location,
                eventType,
                eventDate,
                distanceKm));

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Event creation failed: {(int)response.StatusCode}. {body}");

        using var json = JsonDocument.Parse(body);

        return json.RootElement
            .GetProperty("eventId")
            .GetInt32();
    }

    private async Task AddSecondOrganiserAsync()
    {
        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        if (await db.Users.AnyAsync(
                user => user.Email == SecondOrganiserEmail))
        {
            return;
        }

        var nextUserId = await db.Users
            .Select(user => (int?)user.UserId)
            .MaxAsync() ?? 0;

        var organiser = new User
        {
            UserId = nextUserId + 1,
            RoleId = 1,
            FirstName = "Second",
            LastName = "Organiser",
            Email = SecondOrganiserEmail,
            CreatedAt = DateTime.UtcNow
        };

        var hasher = new PasswordHasher<User>();

        organiser.PasswordHash = hasher.HashPassword(
            organiser,
            RaceDayApiFactory.TestPassword);

        db.Users.Add(organiser);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Organiser_CanCreateEvent()
    {
        using var client = await LoginAsOrganiserAsync();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            EventRequest("Automated Test Run"));

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created, received {(int)response.StatusCode}. {body}");
    }

    [Fact]
    public async Task Organiser_CannotCreateEventWithPastDate()
    {
        using var client = await LoginAsOrganiserAsync();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            EventRequest(
                "Past Event Test",
                eventDate: "2020-01-01"));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanReadCreatedEvent()
    {
        using var client = await LoginAsOrganiserAsync();

        var eventId = await CreateEventAsync(
            client,
            "Event Readback Test");

        var response = await client.GetAsync(
            $"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var retrievedEvent = await response.Content
            .ReadFromJsonAsync<EventResponse>();

        Assert.NotNull(retrievedEvent);
        Assert.Equal(eventId, retrievedEvent.EventId);
        Assert.Equal("Event Readback Test", retrievedEvent.EventName);
    }

    [Fact]
    public async Task GetEvents_WithoutLogin_ReturnsUpcomingEvents()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);

        Assert.Equal(
            JsonValueKind.Array,
            json.RootElement.ValueKind);
    }

    [Fact]
    public async Task GetEvents_CanFilterByEventType()
    {
        using var client = await LoginAsOrganiserAsync();

        const string eventName = "Filter Type Cycle Event";

        await CreateEventAsync(
            client,
            eventName,
            location: "Cycle Filter Location",
            eventType: "Cycle");

        using var publicClient = _factory.CreateClient();

        var response = await publicClient.GetAsync(
            "/api/events?eventType=Cycle");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);

        Assert.Contains(
            json.RootElement.EnumerateArray(),
            item => item.GetProperty("eventName").GetString() == eventName);
    }

    [Fact]
    public async Task GetEvents_CanFilterByDate()
    {
        using var client = await LoginAsOrganiserAsync();

        const string eventName = "Filter Date Event";

        await CreateEventAsync(
            client,
            eventName,
            location: "Date Filter Location",
            eventDate: "2027-11-15");

        using var publicClient = _factory.CreateClient();

        var response = await publicClient.GetAsync(
            "/api/events?eventDate=2027-11-15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);

        Assert.Contains(
            json.RootElement.EnumerateArray(),
            item => item.GetProperty("eventName").GetString() == eventName);
    }

    [Fact]
    public async Task GetEvents_CanFilterByLocation()
    {
        using var client = await LoginAsOrganiserAsync();

        const string eventName = "Filter Location Event";

        await CreateEventAsync(
            client,
            eventName,
            location: "UniqueLocationForEventTest");

        using var publicClient = _factory.CreateClient();

        var response = await publicClient.GetAsync(
            "/api/events?location=UniqueLocationForEventTest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);

        Assert.Contains(
            json.RootElement.EnumerateArray(),
            item => item.GetProperty("eventName").GetString() == eventName);
    }

    [Fact]
    public async Task GetEvent_ForMissingEvent_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/events/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            EventRequest("Unauthorised Event Test"));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent()
    {
        using var client = await LoginAsParticipantAsync();

        var response = await client.PostAsJsonAsync(
            "/api/events",
            EventRequest("Participant Create Test"));

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        using var client = await LoginAsOrganiserAsync();

        var eventId = await CreateEventAsync(
            client,
            "Event Before Update");

        var response = await client.PutAsJsonAsync(
            $"/api/events/{eventId}",
            EventRequest(
                "Event After Update",
                location: "Updated Location",
                eventType: "Walk",
                distanceKm: 5));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await client.GetAsync(
            $"/api/events/{eventId}");

        var updatedEvent = await getResponse.Content
            .ReadFromJsonAsync<EventResponse>();

        Assert.NotNull(updatedEvent);
        Assert.Equal("Event After Update", updatedEvent.EventName);
    }

    [Fact]
    public async Task UpdateEvent_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/events/1",
            EventRequest("Unauthorised Update"));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateEvent()
    {
        using var client = await LoginAsParticipantAsync();

        var response = await client.PutAsJsonAsync(
            "/api/events/1",
            EventRequest("Participant Update Test"));

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_ForMissingEvent_ReturnsNotFound()
    {
        using var client = await LoginAsOrganiserAsync();

        var response = await client.PutAsJsonAsync(
            "/api/events/99999",
            EventRequest("Missing Event Update"));

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent()
    {
        using var ownerClient = await LoginAsOrganiserAsync();

        var eventId = await CreateEventAsync(
            ownerClient,
            "Other Organiser Update Test");

        await AddSecondOrganiserAsync();

        using var otherOrganiser = await LoginAsync(
            SecondOrganiserEmail);

        var response = await otherOrganiser.PutAsJsonAsync(
            $"/api/events/{eventId}",
            EventRequest("Attempted Unauthorised Update"));

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent()
    {
        using var client = await LoginAsOrganiserAsync();

        var eventId = await CreateEventAsync(
            client,
            "Event Delete Test");

        var deleteResponse = await client.DeleteAsync(
            $"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync(
            $"/api/events/{eventId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/events/1");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotDeleteEvent()
    {
        using var client = await LoginAsParticipantAsync();

        var response = await client.DeleteAsync("/api/events/1");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_ForMissingEvent_ReturnsNotFound()
    {
        using var client = await LoginAsOrganiserAsync();

        var response = await client.DeleteAsync(
            "/api/events/99999");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent()
    {
        using var ownerClient = await LoginAsOrganiserAsync();

        var eventId = await CreateEventAsync(
            ownerClient,
            "Other Organiser Delete Test");

        await AddSecondOrganiserAsync();

        using var otherOrganiser = await LoginAsync(
            SecondOrganiserEmail);

        var response = await otherOrganiser.DeleteAsync(
            $"/api/events/{eventId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCreateEventWithDuplicateName()
    {
        using var client = await LoginAsOrganiserAsync();

        await CreateEventAsync(
            client,
            "Duplicate Event Name Test");

        var response = await client.PostAsJsonAsync(
            "/api/events",
            EventRequest("Duplicate Event Name Test"));

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUpdateEventToDuplicateName()
    {
        using var client = await LoginAsOrganiserAsync();

        await CreateEventAsync(
            client,
            "Existing Event Name Test");

        var secondEventId = await CreateEventAsync(
            client,
            "Second Event Name Test");

        var response = await client.PutAsJsonAsync(
            $"/api/events/{secondEventId}",
            EventRequest("Existing Event Name Test"));

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteEventWithCategories()
    {
        using var client = await LoginAsOrganiserAsync();

        // Event 1 has a category seeded by RaceDayApiFactory.
        var response = await client.DeleteAsync("/api/events/1");

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteEventWithEnrolments()
    {
        // The seeded event and category belong to the seeded organiser.
        // First create an enrolment as the seeded participant.
        using var participant = await LoginAsParticipantAsync();

        var enrolmentResponse = await participant.PostAsJsonAsync(
            "/api/events/1/enrolments",
            new
            {
                categoryId = 1
            });

        var enrolmentBody =
            await enrolmentResponse.Content.ReadAsStringAsync();

        Assert.True(
            enrolmentResponse.StatusCode == HttpStatusCode.Created,
            $"Enrolment creation failed: " +
            $"{(int)enrolmentResponse.StatusCode}. {enrolmentBody}");

        // The event must not be deleted while the enrolment exists.
        using var organiser = await LoginAsOrganiserAsync();

        var deleteResponse = await organiser.DeleteAsync("/api/events/1");

        Assert.Equal(
            HttpStatusCode.Conflict,
            deleteResponse.StatusCode);
    }

    private sealed class EventResponse
    {
        public int EventId { get; set; }

        public string EventName { get; set; } = string.Empty;
    }
}
