
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

public class ResultTests : IDisposable
{
    private readonly RaceDayApiFactory _factory =
        new RaceDayApiFactory();

    private const string SecondParticipantEmail =
        "result.participant2@example.com";

    private const string SecondOrganiserEmail =
        "result.organiser2@example.com";

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

    private async Task<int> CreateEnrolmentAsync()
    {
        using var participantClient = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await participantClient.PostAsJsonAsync(
            "/api/enrolments",
            new
            {
                eventId = 1,
                categoryId = 1
            });

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected enrolment creation to return 201 Created, " +
            $"received {(int)response.StatusCode}. Response: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement
            .GetProperty("enrolmentId")
            .GetInt32();
    }

    private static object ValidResult() => new
    {
        finishPosition = 1,
        finishTime = "00:35:42"
    };

    private async Task CreateResultAsync(
        HttpClient organiserClient,
        int enrolmentId)
    {
        var response = await organiserClient.PostAsJsonAsync(
            $"/api/enrolments/{enrolmentId}/result",
            ValidResult());

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Result creation failed: " +
            $"{(int)response.StatusCode}. {body}");
    }

    private async Task AddSecondUserAsync(
        int userId,
        string email,
        int roleId,
        string firstName)
    {
        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        var hasher = new PasswordHasher<User>();

        var user = new User
        {
            UserId = userId,
            RoleId = roleId,
            FirstName = firstName,
            LastName = "Test",
            Email = email,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash =
            hasher.HashPassword(
                user,
                RaceDayApiFactory.TestPassword);

        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private async Task AddSecondParticipantAsync()
    {
        await AddSecondUserAsync(
            3,
            SecondParticipantEmail,
            2,
            "Second");
    }

    private async Task AddSecondOrganiserAndEventAsync()
    {
        await AddSecondUserAsync(
            3,
            SecondOrganiserEmail,
            1,
            "Second");

        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        db.Events.Add(new Event
        {
            EventId = 2,
            OrganiserId = 3,
            EventName = "Second Organiser Event",
            Description = "Event owned by the second organiser",
            EventDate = new DateTime(2027, 12, 11),
            Location = "Cape Town",
            DistanceKm = 5,
            EventType = "Run",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    // CREATE RESULT TESTS

    [Fact]
    public async Task CreateResult_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/enrolments/1/result",
            ValidResult());

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateResult_AsParticipant_ReturnsForbidden()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await client.PostAsJsonAsync(
            "/api/enrolments/1/result",
            ValidResult());

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateResult_ForMissingEnrolment_ReturnsNotFound()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.PostAsJsonAsync(
            "/api/enrolments/99999/result",
            ValidResult());

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateResult_WithValidData_ReturnsCreated()
    {
        var enrolmentId = await CreateEnrolmentAsync();

        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        await CreateResultAsync(client, enrolmentId);
    }

    [Fact]
    public async Task CreateResult_TwiceForSameEnrolment_ReturnsConflict()
    {
        var enrolmentId = await CreateEnrolmentAsync();

        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var route = $"/api/enrolments/{enrolmentId}/result";

        var firstResponse = await client.PostAsJsonAsync(
            route,
            ValidResult());

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync(
            route,
            ValidResult());

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondResponse.StatusCode);
    }

    [Fact]
    public async Task CreateResult_WithInvalidFinishTime_ReturnsBadRequest()
    {
        var enrolmentId = await CreateEnrolmentAsync();

        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.PostAsJsonAsync(
            $"/api/enrolments/{enrolmentId}/result",
            new
            {
                finishPosition = 1,
                finishTime = "-00:01:00"
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateResult_WithFinishTimeOf24Hours_ReturnsBadRequest()
    {
        var enrolmentId = await CreateEnrolmentAsync();

        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.PostAsJsonAsync(
            $"/api/enrolments/{enrolmentId}/result",
            new
            {
                finishPosition = 1,
                finishTime = "1.00:00:00"
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateResult_WithInvalidFinishPosition_ReturnsBadRequest(
        int finishPosition)
    {
        var enrolmentId = await CreateEnrolmentAsync();

        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.PostAsJsonAsync(
            $"/api/enrolments/{enrolmentId}/result",
            new
            {
                finishPosition,
                finishTime = "00:35:42"
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateResult_AsAnotherOrganiser_ReturnsForbidden()
    {
        await AddSecondOrganiserAndEventAsync();

        var enrolmentId = await CreateEnrolmentAsync();

        using var client = await LoginAsync(
            SecondOrganiserEmail);

        var response = await client.PostAsJsonAsync(
            $"/api/enrolments/{enrolmentId}/result",
            ValidResult());

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    // GET MY RESULTS TESTS

    [Fact]
    public async Task GetMyResults_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/api/results/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetMyResults_AsOrganiser_ReturnsForbidden()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.GetAsync(
            "/api/results/me");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task GetMyResults_AfterResultIsRecorded_ReturnsOwnResult()
    {
        var enrolmentId = await CreateEnrolmentAsync();

        using var organiserClient = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        await CreateResultAsync(organiserClient, enrolmentId);

        using var participantClient = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await participantClient.GetAsync(
            "/api/results/me");

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK, received " +
            $"{(int)response.StatusCode}. {body}");

        using var document = JsonDocument.Parse(body);

        var found = false;

        foreach (var result in document.RootElement.EnumerateArray())
        {
            if (result.GetProperty("enrolmentId").GetInt32()
                == enrolmentId)
            {
                found = true;
                break;
            }
        }

        Assert.True(found, $"Result for enrolment {enrolmentId} was not returned.");
    }

    // GET EVENT RESULTS TESTS

    [Fact]
    public async Task GetEventResults_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/api/events/1/results");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetEventResults_AsParticipant_ReturnsForbidden()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await client.GetAsync(
            "/api/events/1/results");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task GetEventResults_ForMissingEvent_ReturnsNotFound()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.GetAsync(
            "/api/events/99999/results");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetEventResults_AsAnotherOrganiser_ReturnsForbidden()
    {
        await AddSecondOrganiserAndEventAsync();

        using var participantClient = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var enrolmentId = await CreateEnrolmentAsync();

        using var ownerClient = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        await CreateResultAsync(ownerClient, enrolmentId);

        using var otherOrganiserClient = await LoginAsync(
            SecondOrganiserEmail);

        var response = await otherOrganiserClient.GetAsync(
            "/api/events/1/results");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    // GET SINGLE RESULT TESTS

    [Fact]
    public async Task GetEnrolmentResult_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/api/enrolments/1/result");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetEnrolmentResult_BeforeResultExists_ReturnsNotFound()
    {
        var enrolmentId = await CreateEnrolmentAsync();

        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await client.GetAsync(
            $"/api/enrolments/{enrolmentId}/result");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetEnrolmentResult_AsAnotherParticipant_ReturnsForbidden()
    {
        await AddSecondParticipantAsync();

        var enrolmentId = await CreateEnrolmentAsync();

        using var organiserClient = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        await CreateResultAsync(organiserClient, enrolmentId);

        using var otherParticipantClient = await LoginAsync(
            SecondParticipantEmail);

        var response = await otherParticipantClient.GetAsync(
            $"/api/enrolments/{enrolmentId}/result");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    // UPDATE RESULT TESTS

    [Fact]
    public async Task UpdateResult_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/enrolments/1/result",
            new
            {
                finishPosition = 2,
                finishTime = "00:36:00"
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateResult_AsParticipant_ReturnsForbidden()
    {
        using var client = await LoginAsync(
            RaceDayApiFactory.ParticipantEmail);

        var response = await client.PutAsJsonAsync(
            "/api/enrolments/1/result",
            new
            {
                finishPosition = 2,
                finishTime = "00:36:00"
            });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateResult_ForEnrolmentWithoutResult_ReturnsNotFound()
    {
        var enrolmentId = await CreateEnrolmentAsync();

        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        var response = await client.PutAsJsonAsync(
            $"/api/enrolments/{enrolmentId}/result",
            new
            {
                finishPosition = 2,
                finishTime = "00:36:00"
            });

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateResult_WithValidData_ReturnsOkAndPersistsChanges()
    {
        var enrolmentId = await CreateEnrolmentAsync();

        using var client = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        await CreateResultAsync(client, enrolmentId);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/enrolments/{enrolmentId}/result",
            new
            {
                finishPosition = 2,
                finishTime = "00:36:00"
            });

        var updateBody = await updateResponse.Content.ReadAsStringAsync();

        Assert.True(
            updateResponse.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK, received " +
            $"{(int)updateResponse.StatusCode}. {updateBody}");

        using var document = JsonDocument.Parse(updateBody);

        Assert.Equal(
            enrolmentId,
            document.RootElement.GetProperty("enrolmentId").GetInt32());

        Assert.Equal(
            2,
            document.RootElement.GetProperty("finishPosition").GetInt32());

        Assert.Equal(
            "00:36:00",
            document.RootElement.GetProperty("finishTime").GetString());
    }

    [Fact]
    public async Task UpdateResult_AsAnotherOrganiser_ReturnsForbidden()
    {
        await AddSecondOrganiserAndEventAsync();

        var enrolmentId = await CreateEnrolmentAsync();

        using var ownerClient = await LoginAsync(
            RaceDayApiFactory.OrganiserEmail);

        await CreateResultAsync(ownerClient, enrolmentId);

        using var otherOrganiserClient = await LoginAsync(
            SecondOrganiserEmail);

        var response = await otherOrganiserClient.PutAsJsonAsync(
            $"/api/enrolments/{enrolmentId}/result",
            new
            {
                finishPosition = 2,
                finishTime = "00:36:00"
            });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
