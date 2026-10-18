using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace RaceDay.Api.Tests;

public class EnrolmentStatusTests : IDisposable
{
    private readonly RaceDayApiFactory _factory = new RaceDayApiFactory();

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = RaceDayApiFactory.TestPassword });

        Assert.True(response.IsSuccessStatusCode);

        return client;
    }

    private async Task<int> EnrolAsync()
    {
        using var participant = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await participant.PostAsJsonAsync(
            "/api/events/1/enrolments",
            new { categoryId = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("enrolmentId").GetInt32();
    }

    [Fact]
    public async Task UpdateStatus_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/events/1/enrolments/1/status",
            new { enrolmentStatus = "Confirmed" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_AsParticipant_ReturnsForbidden()
    {
        var enrolmentId = await EnrolAsync();

        using var participant = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await participant.PutAsJsonAsync(
            $"/api/events/1/enrolments/{enrolmentId}/status",
            new { enrolmentStatus = "Confirmed" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_AsOwningOrganiser_ReturnsOk()
    {
        var enrolmentId = await EnrolAsync();

        using var organiser = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await organiser.PutAsJsonAsync(
            $"/api/events/1/enrolments/{enrolmentId}/status",
            new { enrolmentStatus = "Confirmed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Confirmed", body);
    }

    [Fact]
    public async Task UpdateStatus_WithInvalidStatus_ReturnsBadRequest()
    {
        var enrolmentId = await EnrolAsync();

        using var organiser = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await organiser.PutAsJsonAsync(
            $"/api/events/1/enrolments/{enrolmentId}/status",
            new { enrolmentStatus = "Cancelled" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_ForMissingEnrolment_ReturnsNotFound()
    {
        using var organiser = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await organiser.PutAsJsonAsync(
            "/api/events/1/enrolments/99999/status",
            new { enrolmentStatus = "Confirmed" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}