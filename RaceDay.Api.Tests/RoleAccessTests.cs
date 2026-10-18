
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RaceDay.Api.Tests;

public class RoleAccessTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public RoleAccessTests(RaceDayApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> LoginAsAsync(string email)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email,
                password = RaceDayApiFactory.TestPassword
            });

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"Login failed for {email}. " +
            $"Status: {(int)response.StatusCode}. " +
            $"Response: {responseBody}");

        return client;
    }

    [Fact]
    public async Task Participant_CannotAccessOrganiserDashboard()
    {
        using var client = await LoginAsAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await client.GetAsync(
            "/api/organiser/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotAccessParticipantDashboard()
    {
        using var client = await LoginAsAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.GetAsync(
            "/api/participant/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent()
    {
        using var client = await LoginAsAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await client.PostAsJsonAsync(
            "/api/events",
            new
            {
                eventName = "Test Run",
                description = "Test event",
                eventDate = "2027-12-10",
                location = "Johannesburg",
                distanceKm = 10,
                eventType = "Run"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCreateEnrolment()
    {
        using var client = await LoginAsAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.PostAsJsonAsync(
            "/api/enrolments",
            new
            {
                eventId = 1,
                categoryId = 1
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
