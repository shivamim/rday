
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

public class CategoryTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public CategoryTests(RaceDayApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> LoginAsOrganiserAsync()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = RaceDayApiFactory.OrganiserEmail,
                password = RaceDayApiFactory.TestPassword
            });

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"Organiser login failed: {(int)response.StatusCode}. {body}");

        return client;
    }

    private async Task<HttpClient> LoginAsParticipantAsync()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = RaceDayApiFactory.ParticipantEmail,
                password = RaceDayApiFactory.TestPassword
            });

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"Participant login failed: {(int)response.StatusCode}. {body}");

        return client;
    }

    private static object ValidAgeCategory(string name) => new
    {
        categoryName = name,
        categoryType = "Age",
        minAge = 18,
        maxAge = 35,
        categoryDistanceKm = (decimal?)null
    };

    private async Task<int> CreateCategoryAsync(
        HttpClient client,
        string? name = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/events/1/categories",
            ValidAgeCategory(name ?? $"Test Category {Guid.NewGuid():N}"));

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Category creation failed with HTTP {(int)response.StatusCode}. Response: {body}");

        using var json = JsonDocument.Parse(body);

        return json.RootElement
            .GetProperty("categoryId")
            .GetInt32();
    }

    private async Task AddSecondOrganiserAsync()
    {
        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        const string email = "second.organiser.categories@example.com";

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            return;
        }

        var role = await db.Roles
            .FirstAsync(r => r.RoleName == "Organiser");

        var user = new User
        {
            RoleId = role.RoleId,
            FirstName = "Second",
            LastName = "Organiser",
            Email = email,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = new PasswordHasher<User>()
            .HashPassword(user, RaceDayApiFactory.TestPassword);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Events.Add(new Event
        {
            OrganiserId = user.UserId,
            EventName = $"Second Organiser Event {Guid.NewGuid():N}",
            Description = "Event for category ownership tests",
            EventDate = new DateTime(2027, 12, 10),
            Location = "Johannesburg",
            DistanceKm = 5,
            EventType = "Run",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> LoginAsSecondOrganiserAsync()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "second.organiser.categories@example.com",
                password = RaceDayApiFactory.TestPassword
            });

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"Second organiser login failed: {(int)response.StatusCode}. {body}");

        return client;
    }

    [Fact]
    public async Task GetCategories_ForMissingEvent_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/api/events/99999/categories");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCategories_ForExistingEvent_ReturnsCategoryDetails()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/api/events/1/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Test Age Category", body);
        Assert.Contains("CategoryDistanceKm", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCategory_ForMissingEvent_ReturnsNotFound()
    {
        using var client = await LoginAsOrganiserAsync();

        var response = await client.PostAsJsonAsync(
            "/api/events/99999/categories",
            ValidAgeCategory($"Missing Event {Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithoutLogin_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/events/1/categories",
            ValidAgeCategory($"Unauthenticated {Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithInvalidAgeRange_ReturnsBadRequest()
    {
        using var client = await LoginAsOrganiserAsync();

        var response = await client.PostAsJsonAsync(
            "/api/events/1/categories",
            new
            {
                categoryName = $"Invalid Age {Guid.NewGuid():N}",
                categoryType = "Age",
                minAge = 40,
                maxAge = 18,
                categoryDistanceKm = (decimal?)null
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateName_ReturnsConflict()
    {
        using var client = await LoginAsOrganiserAsync();

        var categoryName = $"Duplicate {Guid.NewGuid():N}";

        var firstResponse = await client.PostAsJsonAsync(
            "/api/events/1/categories",
            ValidAgeCategory(categoryName));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync(
            "/api/events/1/categories",
            ValidAgeCategory(categoryName));

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithUnsupportedType_ReturnsBadRequest()
    {
        using var client = await LoginAsOrganiserAsync();

        var response = await client.PostAsJsonAsync(
            "/api/events/1/categories",
            new
            {
                categoryName = $"Unsupported {Guid.NewGuid():N}",
                categoryType = "Speed",
                minAge = (int?)null,
                maxAge = (int?)null,
                categoryDistanceKm = (decimal?)null
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_AsOwner_UpdatesCategory()
    {
        using var client = await LoginAsOrganiserAsync();

        var categoryId = await CreateCategoryAsync(client);
        var updatedName = $"Updated {Guid.NewGuid():N}";

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/categories/{categoryId}",
            ValidAgeCategory(updatedName));

        var updateBody = await updateResponse.Content.ReadAsStringAsync();

        Assert.True(
            updateResponse.StatusCode == HttpStatusCode.OK,
            $"Update failed with HTTP {(int)updateResponse.StatusCode}. Response: {updateBody}");

        using var updateJson = JsonDocument.Parse(updateBody);

        Assert.Equal(
            updatedName,
            updateJson.RootElement.GetProperty("categoryName").GetString());
    }

    [Fact]
    public async Task UpdateCategory_ForMissingId_ReturnsNotFound()
    {
        using var client = await LoginAsOrganiserAsync();

        var response = await client.PutAsJsonAsync(
            "/api/categories/99999",
            ValidAgeCategory($"Missing Category {Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_AsAnotherOrganiser_ReturnsForbidden()
    {
        await AddSecondOrganiserAsync();

        using var ownerClient = await LoginAsOrganiserAsync();
        var categoryId = await CreateCategoryAsync(ownerClient);

        using var otherOrganiserClient = await LoginAsSecondOrganiserAsync();

        var response = await otherOrganiserClient.PutAsJsonAsync(
            $"/api/categories/{categoryId}",
            ValidAgeCategory($"Unauthorised Update {Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_AsOwnerWithoutEnrolments_ReturnsSuccess()
    {
        using var client = await LoginAsOrganiserAsync();

        var categoryId = await CreateCategoryAsync(client);

        var deleteResponse = await client.DeleteAsync(
            $"/api/categories/{categoryId}");

        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();

        Assert.True(
            deleteResponse.StatusCode == HttpStatusCode.NoContent,
            $"Category deletion failed with HTTP {(int)deleteResponse.StatusCode}. Response: {deleteBody}");

        var getResponse = await client.GetAsync(
            $"/api/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithEnrolments_ReturnsConflict()
    {
        using var client = await LoginAsOrganiserAsync();

        var categoryId = await CreateCategoryAsync(client);

        using var participantClient = await LoginAsParticipantAsync();

        var enrolmentResponse = await participantClient.PostAsJsonAsync(
            "/api/events/1/enrolments",
            new { categoryId });

        var enrolmentBody = await enrolmentResponse.Content.ReadAsStringAsync();

        Assert.True(
            enrolmentResponse.StatusCode == HttpStatusCode.Created,
            $"Enrolment creation failed with HTTP {(int)enrolmentResponse.StatusCode}. Response: {enrolmentBody}");

        var deleteResponse = await client.DeleteAsync(
            $"/api/categories/{categoryId}");

        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();

        Assert.True(
            deleteResponse.StatusCode == HttpStatusCode.Conflict,
            $"Expected HTTP 409 when deleting a category with enrolments, but received HTTP {(int)deleteResponse.StatusCode}. Response: {deleteBody}");
    }

    [Fact]
    public async Task UpdateCategory_AsParticipant_ReturnsForbidden()
    {
        using var organiserClient = await LoginAsOrganiserAsync();
        var categoryId = await CreateCategoryAsync(organiserClient);

        using var participantClient = await LoginAsParticipantAsync();

        var updateResponse = await participantClient.PutAsJsonAsync(
            $"/api/categories/{categoryId}",
            ValidAgeCategory($"Attempted Update {Guid.NewGuid():N}"));

        Assert.Equal(
            HttpStatusCode.Forbidden,
            updateResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_AsParticipant_ReturnsForbidden()
    {
        using var organiserClient = await LoginAsOrganiserAsync();
        var categoryId = await CreateCategoryAsync(organiserClient);

        using var participantClient = await LoginAsParticipantAsync();

        var deleteResponse = await participantClient.DeleteAsync(
            $"/api/categories/{categoryId}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            deleteResponse.StatusCode);
    }
}
