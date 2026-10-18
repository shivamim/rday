
using System.Net;
using System.Text.Json;
using Xunit;

namespace RaceDay.Api.Tests;

public class ApiEndpointTests : IClassFixture<RaceDayApiFactory>
{
    private readonly HttpClient _client;

    public ApiEndpointTests(RaceDayApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetEvents_WithoutLogin_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMissingEvent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/events/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_WithMatchingEventType_ReturnsMatchingEvents()
    {
        var response = await _client.GetAsync("/api/events?eventType=Run");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var events = document.RootElement.EnumerateArray().ToList();

        Assert.Contains(events, item =>
            item.GetProperty("eventType").GetString() == "Run");

        Assert.All(events, item =>
            Assert.Equal("Run", item.GetProperty("eventType").GetString()));
    }

    [Fact]
    public async Task GetEvents_WithNonMatchingEventType_ReturnsEmptyList()
    {
        var response = await _client.GetAsync(
            "/api/events?eventType=NonExistentEventType");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        Assert.Empty(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task GetEvents_WithMatchingDate_ReturnsEventsOnThatDate()
    {
        var response = await _client.GetAsync(
            "/api/events?eventDate=2027-12-10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var events = document.RootElement.EnumerateArray().ToList();

        Assert.Contains(events, item =>
            item.GetProperty("eventName").GetString() ==
            "Test RaceDay Event");

        Assert.All(events, item =>
            Assert.Equal(
                new DateTime(2027, 12, 10),
                item.GetProperty("eventDate").GetDateTime().Date));
    }

    [Fact]
    public async Task GetEvents_WithMatchingLocation_ReturnsMatchingEvents()
    {
        var response = await _client.GetAsync(
            "/api/events?location=Johannesburg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var events = document.RootElement.EnumerateArray().ToList();

        Assert.Contains(events, item =>
            item.GetProperty("eventName").GetString() ==
            "Test RaceDay Event");

        Assert.All(events, item =>
            Assert.Contains(
                "Johannesburg",
                item.GetProperty("location").GetString()!));
    }

    [Fact]
    public async Task GetEvents_WithCombinedFilters_ReturnsMatchingEvents()
    {
        var response = await _client.GetAsync(
            "/api/events?eventType=Run&eventDate=2027-12-10&location=Johannesburg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var events = document.RootElement.EnumerateArray().ToList();

        Assert.Contains(events, item =>
            item.GetProperty("eventName").GetString() ==
            "Test RaceDay Event");

        Assert.All(events, item =>
        {
            Assert.Equal("Run", item.GetProperty("eventType").GetString());

            Assert.Equal(
                new DateTime(2027, 12, 10),
                item.GetProperty("eventDate").GetDateTime().Date);

            Assert.Contains(
                "Johannesburg",
                item.GetProperty("location").GetString()!);
        });
    }

    [Fact]
    public async Task ParticipantDashboard_WithoutLogin_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(
            "/api/participant/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OrganiserDashboard_WithoutLogin_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(
            "/api/organiser/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithoutLogin_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync(
            "/api/events",
            new StringContent(
                """
                {
                    "eventName": "Test Run",
                    "description": "Integration test event",
                    "eventDate": "2027-12-10",
                    "location": "Johannesburg",
                    "distanceKm": 10,
                    "eventType": "Run"
                }
                """,
                System.Text.Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEnrolment_WithoutLogin_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync(
            "/api/enrolments",
            new StringContent(
                """
                {
                    "eventId": 1,
                    "categoryId": 1
                }
                """,
                System.Text.Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
