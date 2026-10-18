
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

public class RaceDayApiFactory : WebApplicationFactory<Program>
{
    public const string TestPassword = "RaceDayTest123!";

    public const string ParticipantEmail =
        "test.participant@example.com";

    public const string OrganiserEmail =
        "test.organiser@example.com";

    public const string FakeProfilePictureUrl =
        "https://test.example.com/profile-pictures/test-image.png";

    private readonly string _databaseName =
        $"RaceDayTests_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RaceDayDbContext>>();
            services.RemoveAll<RaceDayDbContext>();

            services.AddDbContext<RaceDayDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IAzureBlobStorageService>();
            services.RemoveAll<AzureBlobStorageService>();

            services.AddSingleton<
                FakeAzureBlobStorageService>();

            services.AddSingleton<IAzureBlobStorageService>(
                serviceProvider =>
                    serviceProvider.GetRequiredService<
                        FakeAzureBlobStorageService>());
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RaceDayDbContext>();

        db.Database.EnsureCreated();

        // Seed roles.
        if (!db.Roles.Any())
        {
            db.Roles.AddRange(
                new Role
                {
                    RoleId = 1,
                    RoleName = "Organiser"
                },
                new Role
                {
                    RoleId = 2,
                    RoleName = "Participant"
                });

            db.SaveChanges();
        }

        // Seed test users.
        if (!db.Users.Any())
        {
            var hasher = new PasswordHasher<User>();

            var organiser = new User
            {
                UserId = 1,
                RoleId = 1,
                FirstName = "Test",
                LastName = "Organiser",
                Email = OrganiserEmail,
                CreatedAt = DateTime.UtcNow
            };

            organiser.PasswordHash =
                hasher.HashPassword(organiser, TestPassword);

            var participant = new User
            {
                UserId = 2,
                RoleId = 2,
                FirstName = "Test",
                LastName = "Participant",
                Email = ParticipantEmail,
                CreatedAt = DateTime.UtcNow
            };

            participant.PasswordHash =
                hasher.HashPassword(participant, TestPassword);

            db.Users.AddRange(organiser, participant);
            db.SaveChanges();
        }

        // Seed a test event.
        if (!db.Events.Any())
        {
            db.Events.Add(new Event
            {
                EventId = 1,
                OrganiserId = 1,
                EventName = "Test RaceDay Event",
                Description = "Event used for automated API tests",
                EventDate = new DateTime(2027, 12, 10),
                Location = "Johannesburg",
                DistanceKm = 10,
                EventType = "Run",
                CreatedAt = DateTime.UtcNow
            });

            db.SaveChanges();
        }

        // Seed a category for the test event.
        if (!db.Categories.Any())
        {
            db.Categories.Add(new Category
            {
                CategoryId = 1,
                EventId = 1,
                CategoryName = "Test Age Category",
                CategoryType = "Age",
                MinAge = 18,
                MaxAge = 35,
                CategoryDistanceKm = null
            });

            db.SaveChanges();
        }

        // Do not seed enrolments here.
        // Tests create their own enrolments when required.

        return host;
    }
}

public class FakeAzureBlobStorageService : IAzureBlobStorageService
{
    public Exception? UploadException { get; set; }

    public Task<string> UploadImageAsync(
        IFormFile file,
        string containerName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (UploadException is not null)
        {
            throw UploadException;
        }

        var url = containerName == "profile-pictures"
            ? RaceDayApiFactory.FakeProfilePictureUrl
            : $"https://test.example.com/{containerName}/test-image.png";

        return Task.FromResult(url);
    }
}
