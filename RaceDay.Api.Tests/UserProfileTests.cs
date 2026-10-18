
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace RaceDay.Api.Tests;

public class UserProfileTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public UserProfileTests(RaceDayApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMyProfile_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyProfile_AfterLogin_ReturnsProfileAndPictureUrlField()
    {
        using var client = _factory.CreateClient();

        await LoginAsParticipant(client);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;

        Assert.Equal(
            RaceDayApiFactory.ParticipantEmail,
            root.GetProperty("email").GetString());

        Assert.True(root.TryGetProperty("profilePictureUrl", out _));
        Assert.Equal("Participant", root.GetProperty("role").GetString());
    }

    [Fact]
    public async Task UpdateMyProfile_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/users/me",
            new
            {
                firstName = "Updated",
                lastName = "Participant",
                phoneNumber = "0712345678"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMyProfile_AfterLogin_UpdatesProfileAndReturnsPictureUrlField()
    {
        using var client = _factory.CreateClient();

        await LoginAsParticipant(client);

        var response = await client.PutAsJsonAsync(
            "/api/users/me",
            new
            {
                firstName = "Updated",
                lastName = "Profile",
                phoneNumber = "0712345678"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var user = json.RootElement.GetProperty("user");

        Assert.Equal("Updated", user.GetProperty("firstName").GetString());
        Assert.Equal("Profile", user.GetProperty("lastName").GetString());
        Assert.True(user.TryGetProperty("profilePictureUrl", out _));

        var getResponse = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var getJson = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Updated",
            getJson.RootElement.GetProperty("firstName").GetString());
    }

    private static async Task LoginAsParticipant(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = RaceDayApiFactory.ParticipantEmail,
                password = RaceDayApiFactory.TestPassword
            });

        Assert.True(
            response.IsSuccessStatusCode,
            $"Login failed: {await response.Content.ReadAsStringAsync()}");
    }
}
