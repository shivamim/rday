using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ROLES
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");

            entity.HasKey(e => e.RoleId)
                .HasName("PK_Roles");

            entity.Property(e => e.RoleId)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.RoleName)
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired();

            entity.HasIndex(e => e.RoleName)
                .IsUnique()
                .HasDatabaseName("UQ_Roles_RoleName");
        });

        // SEED DEFAULT ROLES
        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                RoleId = 1,
                RoleName = "Organiser"
            },
            new Role
            {
                RoleId = 2,
                RoleName = "Participant"
            }
        );

        // USERS
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(e => e.UserId)
                .HasName("PK_Users");

            entity.Property(e => e.UserId)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.FirstName)
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.LastName)
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .IsUnicode(false)
                .IsRequired();

            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("UQ_Users_Email");

            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.Property(e => e.ProfilePictureUrl)
                .HasMaxLength(500)
                .IsUnicode(false);

            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne<Role>()
                .WithMany()
                .HasForeignKey(e => e.RoleId)
                .HasConstraintName("FK_Users_Roles")
                .OnDelete(DeleteBehavior.NoAction);
        });

        // EVENTS
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Events", table =>
            {
                table.HasCheckConstraint(
                    "CK_Events_DistanceKm",
                    "[DistanceKm] > 0");

                table.HasCheckConstraint(
                    "CK_Events_EventType",
                    "[EventType] IN ('Run', 'Walk', 'Cycle')");
            });

            entity.HasKey(e => e.EventId)
                .HasName("PK_Events");

            entity.Property(e => e.EventId)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.EventName)
                .HasMaxLength(150)
                .IsUnicode(false)
                .IsRequired();

            entity.HasIndex(e => e.EventName)
                .IsUnique()
                .HasDatabaseName("UQ_Events_EventName");

            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.EventDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(e => e.Location)
                .HasMaxLength(200)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.DistanceKm)
                .HasPrecision(6, 2)
                .IsRequired();

            entity.Property(e => e.EventType)
                .HasMaxLength(10)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.BannerImageUrl)
                .HasMaxLength(500)
                .IsUnicode(false);

            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.OrganiserId)
                .HasConstraintName("FK_Events_Organiser")
                .OnDelete(DeleteBehavior.NoAction);
        });

        // CATEGORIES
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories", table =>
            {
                table.HasCheckConstraint(
                    "CK_Categories_CategoryType",
                    "[CategoryType] IN ('Age', 'Distance')");

                table.HasCheckConstraint(
                    "CK_Categories_AgeRange",
                    "(([MinAge] IS NULL AND [MaxAge] IS NULL) OR " +
                    "([MinAge] IS NOT NULL AND [MaxAge] IS NOT NULL " +
                    "AND [MinAge] >= 0 AND [MaxAge] >= [MinAge]))");

                table.HasCheckConstraint(
                    "CK_Categories_Distance",
                    "[CategoryDistanceKm] IS NULL OR [CategoryDistanceKm] > 0");
            });

            entity.HasKey(e => e.CategoryId)
                .HasName("PK_Categories");

            entity.Property(e => e.CategoryId)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.CategoryName)
                .HasMaxLength(100)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.CategoryType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired();

            entity.Property(e => e.CategoryDistanceKm)
                .HasPrecision(6, 2);

            entity.HasIndex(e => new
            {
                e.EventId,
                e.CategoryName
            })
            .IsUnique()
            .HasDatabaseName("UQ_Categories_Event_CategoryName");

            entity.HasOne<Event>()
                .WithMany()
                .HasForeignKey(e => e.EventId)
                .HasConstraintName("FK_Categories_Events")
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ENROLMENTS
        modelBuilder.Entity<Enrolment>(entity =>
        {
            entity.ToTable("Enrolments", table =>
            {
                table.HasCheckConstraint(
                    "CK_Enrolments_Status",
                    "([EnrolmentStatus] = 'Confirmed' OR " +
                    "[EnrolmentStatus] = 'Pending')");
            });

            entity.HasKey(e => e.EnrolmentId)
                .HasName("PK_Enrolments");

            entity.Property(e => e.EnrolmentId)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.EnrolmentStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending")
                .IsRequired();

            entity.Property(e => e.EnrolledAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(e => new
            {
                e.ParticipantId,
                e.EventId
            })
            .IsUnique()
            .HasDatabaseName("UQ_Enrolments_Participant_Event");

            entity.HasOne(e => e.Participant)
                .WithMany()
                .HasForeignKey(e => e.ParticipantId)
                .HasConstraintName("FK_Enrolments_Participant")
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Event)
                .WithMany()
                .HasForeignKey(e => e.EventId)
                .HasConstraintName("FK_Enrolments_Event")
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .HasConstraintName("FK_Enrolments_Category")
                .OnDelete(DeleteBehavior.NoAction);
        });

        // RESULTS
        modelBuilder.Entity<Result>(entity =>
        {
            entity.ToTable("Results", table =>
            {
                table.HasCheckConstraint(
                    "CK_Results_FinishPosition",
                    "[FinishPosition] > 0");
            });

            entity.HasKey(e => e.ResultId)
                .HasName("PK_Results");

            entity.Property(e => e.ResultId)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.FinishTime)
                .HasColumnType("time")
                .IsRequired();

            entity.Property(e => e.RecordedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(e => e.EnrolmentId)
                .IsUnique()
                .HasDatabaseName("UQ_Results_Enrolment");

            entity.HasOne(e => e.Enrolment)
                .WithOne(e => e.Result)
                .HasForeignKey<Result>(e => e.EnrolmentId)
                .HasConstraintName("FK_Results_Enrolment")
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}