
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using Xunit;

namespace RaceDay.Api.Tests;

public class AuthenticationTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public AuthenticationTests(RaceDayApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = RaceDayApiFactory.ParticipantEmail,
                password = "WrongPassword123!"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccess()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = RaceDayApiFactory.ParticipantEmail,
                password = RaceDayApiFactory.TestPassword
            });

        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected successful login but received {(int)response.StatusCode}: " +
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Logout_ClearsSession()
    {
        using var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = RaceDayApiFactory.ParticipantEmail,
                password = RaceDayApiFactory.TestPassword
            });

        Assert.True(
            loginResponse.IsSuccessStatusCode,
            $"Login failed: {await loginResponse.Content.ReadAsStringAsync()}");

        var logoutResponse = await client.PostAsync(
            "/api/auth/logout",
            content: null);

        Assert.True(
            logoutResponse.IsSuccessStatusCode,
            $"Logout failed: {await logoutResponse.Content.ReadAsStringAsync()}");

        var dashboardResponse = await client.GetAsync(
            "/api/participant/dashboard");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            dashboardResponse.StatusCode);
    }

    [Theory]
    [InlineData("Participant")]
    [InlineData("Organiser")]
    public async Task Register_WithValidRole_CreatesAccountWithHashedPassword(
        string roleName)
    {
        using var client = _factory.CreateClient();

        var email = $"new.{roleName.ToLowerInvariant()}.{Guid.NewGuid():N}@example.com";
        const string password = "NewTestPassword123!";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                firstName = "New",
                lastName = "User",
                email,
                password,
                phoneNumber = "0712345678",
                roleName
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        var registeredUser = await db.Users
            .SingleOrDefaultAsync(user => user.Email == email);

        Assert.NotNull(registeredUser);
        Assert.NotEqual(password, registeredUser.PasswordHash);

        var hasher = new PasswordHasher<User>();

        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(
                registeredUser,
                registeredUser.PasswordHash,
                password));

        var registeredRole = await db.Roles
            .Where(role => role.RoleId == registeredUser.RoleId)
            .Select(role => role.RoleName)
            .SingleAsync();

        Assert.Equal(roleName, registeredRole);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                firstName = "Duplicate",
                lastName = "Participant",
                email = RaceDayApiFactory.ParticipantEmail,
                password = "NewTestPassword123!",
                phoneNumber = "0712345678",
                roleName = "Participant"
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidRole_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                firstName = "Invalid",
                lastName = "Role",
                email = $"invalid.role.{Guid.NewGuid():N}@example.com",
                password = "NewTestPassword123!",
                roleName = "Administrator"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidPasswordLength_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                firstName = "Short",
                lastName = "Password",
                email = $"short.password.{Guid.NewGuid():N}@example.com",
                password = "short",
                roleName = "Participant"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
