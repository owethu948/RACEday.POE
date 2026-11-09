/* ============================================================================
   RaceDay - Part 3 schema changes (image support)

   You normally DON'T need to run this: the API adds these columns by itself when it starts.
   It is here as documentation, and for anyone managing the database by hand.
   Safe to run more than once.
   ============================================================================ */

IF COL_LENGTH('dbo.Events', 'BannerUrl') IS NULL
    ALTER TABLE dbo.Events ADD BannerUrl NVARCHAR(500) NULL;   -- public Azure Blob URL of the event banner
GO

IF COL_LENGTH('dbo.UserProfiles', 'ProfilePictureUrl') IS NULL
    ALTER TABLE dbo.UserProfiles ADD ProfilePictureUrl NVARCHAR(500) NULL;   -- public Azure Blob URL of the profile picture
GO
