using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// Code-First model. Table names, column names, lengths, keys, unique constraints,
/// CHECK constraints and cascade rules mirror the Part 1 ERD / RaceDay_Schema.sql.
/// SQL Server-only details (datetime column type, GETDATE() defaults) are applied
/// only when running on SQL Server so the same model also runs on SQLite in the tests.
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var sqlServer = Database.IsSqlServer();

        // ---------------------------------------------------------------- Users
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName("UserID");
            e.Property(x => x.Email).IsRequired().HasMaxLength(255);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(255);
            e.Property(x => x.Role).IsRequired().HasMaxLength(20);
            if (sqlServer)
            {
                e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            }
        });

        // --------------------------------------------------------- UserProfiles
        modelBuilder.Entity<UserProfile>(e =>
        {
            e.ToTable("UserProfiles");
            e.HasKey(x => x.ProfileId);
            e.Property(x => x.ProfileId).HasColumnName("ProfileID");
            e.Property(x => x.UserId).HasColumnName("UserID");
            e.Property(x => x.FullName).IsRequired().HasMaxLength(150);
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
            e.Property(x => x.EmergencyContact).HasMaxLength(150);
            e.Property(x => x.ProfilePictureUrl).HasMaxLength(500);
            e.HasOne(x => x.User)
                .WithOne(u => u.Profile)
                .HasForeignKey<UserProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --------------------------------------------------------------- Events
        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Events", t => t.HasCheckConstraint("CK_Events_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')"));
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventId).HasColumnName("EventID");
            e.Property(x => x.OrganiserId).HasColumnName("OrganiserID");
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.BannerUrl).HasMaxLength(500);
            e.Property(x => x.Location).IsRequired().HasMaxLength(200);
            e.Property(x => x.EventType).IsRequired().HasMaxLength(20);
            e.Property(x => x.DistanceKm).HasPrecision(5, 2);
            if (sqlServer)
            {
                e.Property(x => x.EventDate).HasColumnType("datetime");
                e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            }
            e.HasOne(x => x.Organiser)
                .WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ----------------------------------------------------------- Categories
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.CategoryId).HasColumnName("CategoryID");
            e.Property(x => x.EventId).HasColumnName("EventID");
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.DistanceKm).HasPrecision(5, 2);
            e.Property(x => x.EntryFee).HasPrecision(8, 2);
            e.HasOne(x => x.Event)
                .WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------------------------------------------------- Enrolments
        modelBuilder.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t => t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Confirmed', 'Cancelled')"));
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.EnrolmentId).HasColumnName("EnrolmentID");
            e.Property(x => x.ParticipantId).HasColumnName("ParticipantID");
            e.Property(x => x.CategoryId).HasColumnName("CategoryID");
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            if (sqlServer)
            {
                e.Property(x => x.EnrolmentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            }
            e.HasIndex(x => new { x.ParticipantId, x.CategoryId })
                .IsUnique()
                .HasDatabaseName("UQ_Enrolments_Participant_Category");
            e.HasOne(x => x.Participant)
                .WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category)
                .WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // -------------------------------------------------------------- Results
        modelBuilder.Entity<Result>(e =>
        {
            e.ToTable("Results", t => t.HasCheckConstraint("CK_Results_Status", "[Status] IN ('Finished', 'DNF', 'DSQ')"));
            e.HasKey(x => x.ResultId);
            e.Property(x => x.ResultId).HasColumnName("ResultID");
            e.Property(x => x.EnrolmentId).HasColumnName("EnrolmentID");
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            e.HasOne(x => x.Enrolment)
                .WithOne(en => en.Result)
                .HasForeignKey<Result>(x => x.EnrolmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
