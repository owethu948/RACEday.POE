using Microsoft.EntityFrameworkCore;

namespace RaceDay.Api.Data;

/// <summary>
/// EnsureCreated() builds a missing database but never alters one that already exists. A developer who
/// ran Part 2 therefore already has tables without the Part 3 image columns. These idempotent statements
/// add them, and do nothing on a database that is already up to date.
/// </summary>
public static class SchemaUpgrades
{
    public static Task ApplyAsync(RaceDayDbContext db) => db.Database.ExecuteSqlRawAsync(
        """
        IF COL_LENGTH('dbo.Events', 'BannerUrl') IS NULL
            ALTER TABLE dbo.Events ADD BannerUrl NVARCHAR(500) NULL;
        IF COL_LENGTH('dbo.UserProfiles', 'ProfilePictureUrl') IS NULL
            ALTER TABLE dbo.UserProfiles ADD ProfilePictureUrl NVARCHAR(500) NULL;
        """);
}
