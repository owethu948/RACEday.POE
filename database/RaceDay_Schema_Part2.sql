/* ==========================================================================
   RaceDay - Database Schema
   Part 2 - Restful API Development (updated copy of the Part 1 script)
   Same 6 entities as the Part 1 ERD (Users, UserProfiles, Events, Categories,
   Enrolments, Results). Two columns were added to dbo.Events because the
   Part 2 brief requires every event to capture a distance and an event type:
       Events.EventType  NVARCHAR(20)  CHECK IN ('Run','Walk','Cycle')
       Events.DistanceKm DECIMAL(5,2)
   Seeded users now carry real PBKDF2 password hashes (password: Password123!)
   so they can log in through the API.
   NOTE: the API creates its own database (RaceDayDB_Part2) with EF Core
   Code-First. This script is for reference / manual comparison in SSMS.
   Target: SQL Server (SSMS)

   ========================================================================== */

IF DB_ID('RaceDayDB') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB;
END
GO

USE RaceDayDB;
GO

/* --------------------------------------------------------------------------
   Drop tables if they already exist (child -> parent order) so the script
   can be re-run cleanly on a fresh instance.
   -------------------------------------------------------------------------- */
IF OBJECT_ID('dbo.Results', 'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments', 'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.UserProfiles', 'U') IS NOT NULL DROP TABLE dbo.UserProfiles;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

/* ==========================================================================
   TABLE: Users
   Holds login/auth data for both Organisers and Participants (Role column
   distinguishes them).
   ========================================================================== */
CREATE TABLE dbo.Users (
    UserID          INT IDENTITY(1,1) PRIMARY KEY,
    Email           NVARCHAR(255)   NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(255)   NOT NULL,
    Role            NVARCHAR(20)    NOT NULL
                        CONSTRAINT CK_Users_Role CHECK (Role IN ('Organiser', 'Participant')),
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

/* ==========================================================================
   TABLE: UserProfiles
   1:1 extension of Users holding extended personal details.
   ========================================================================== */
CREATE TABLE dbo.UserProfiles (
    ProfileID           INT IDENTITY(1,1) PRIMARY KEY,
    UserID              INT             NOT NULL UNIQUE,
    FullName            NVARCHAR(150)   NOT NULL,
    PhoneNumber         NVARCHAR(20)    NULL,
    DateOfBirth         DATE            NULL,
    EmergencyContact    NVARCHAR(150)   NULL,
    CONSTRAINT FK_UserProfiles_Users FOREIGN KEY (UserID)
        REFERENCES dbo.Users(UserID) ON DELETE CASCADE
);
GO

/* ==========================================================================
   TABLE: Events
   Created and owned by an Organiser (Users.Role = 'Organiser').
   ========================================================================== */
CREATE TABLE dbo.Events (
    EventID         INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserID     INT             NOT NULL,
    Name            NVARCHAR(200)   NOT NULL,
    Description     NVARCHAR(1000)  NULL,
    EventDate       DATETIME        NOT NULL,
    Location        NVARCHAR(200)   NOT NULL,
    EventType       NVARCHAR(20)    NOT NULL DEFAULT 'Run'
                        CONSTRAINT CK_Events_EventType CHECK (EventType IN ('Run', 'Walk', 'Cycle')),
    DistanceKm      DECIMAL(5,2)    NOT NULL DEFAULT 0,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserID)
        REFERENCES dbo.Users(UserID)
);
GO

/* ==========================================================================
   TABLE: Categories
   Each event can have multiple entry categories (e.g. 5km, 10km, 21km).
   ========================================================================== */
CREATE TABLE dbo.Categories (
    CategoryID      INT IDENTITY(1,1) PRIMARY KEY,
    EventID         INT             NOT NULL,
    Name            NVARCHAR(100)   NOT NULL,
    DistanceKm      DECIMAL(5,2)    NOT NULL,
    MaxParticipants INT             NOT NULL DEFAULT 100,
    EntryFee        DECIMAL(8,2)    NOT NULL DEFAULT 0,
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventID)
        REFERENCES dbo.Events(EventID) ON DELETE CASCADE
);
GO

/* ==========================================================================
   TABLE: Enrolments
   Links a Participant (Users.Role = 'Participant') to a Category.
   ========================================================================== */
CREATE TABLE dbo.Enrolments (
    EnrolmentID     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantID   INT             NOT NULL,
    CategoryID      INT             NOT NULL,
    EnrolmentDate   DATETIME        NOT NULL DEFAULT GETDATE(),
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Confirmed'
                        CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Confirmed', 'Cancelled')),
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (ParticipantID)
        REFERENCES dbo.Users(UserID),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryID)
        REFERENCES dbo.Categories(CategoryID) ON DELETE CASCADE,
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantID, CategoryID)
);
GO

/* ==========================================================================
   TABLE: Results
   1:1 with Enrolments - one race-day result per enrolment.
   ========================================================================== */
CREATE TABLE dbo.Results (
    ResultID        INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID     INT             NOT NULL UNIQUE,
    FinishTime      TIME            NULL,
    Position        INT             NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Finished'
                        CONSTRAINT CK_Results_Status CHECK (Status IN ('Finished', 'DNF', 'DSQ')),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentID)
        REFERENCES dbo.Enrolments(EnrolmentID) ON DELETE CASCADE
);
GO

/* ==========================================================================
   SEED DATA
   2 Organisers, 2 Participants, 3 Events (a mix of past and upcoming, so
   the Results data is temporally realistic), categories per event, and
   enrolments/results.
   ========================================================================== */

-- Users: 2 Organisers, 2 Participants
INSERT INTO dbo.Users (Email, PasswordHash, Role) VALUES
('thabo.organiser@raceday.co.za', 'PBKDF2-SHA256$100000$13r/WWHoz2jh/sSeiyOuxg==$VNkrvP5rRl+qec+qOIoahglAPgJVxx6O2jfrDw18ZXw=', 'Organiser'),
('lindiwe.organiser@raceday.co.za', 'PBKDF2-SHA256$100000$13r/WWHoz2jh/sSeiyOuxg==$VNkrvP5rRl+qec+qOIoahglAPgJVxx6O2jfrDw18ZXw=', 'Organiser'),
('sipho.runner@raceday.co.za', 'PBKDF2-SHA256$100000$13r/WWHoz2jh/sSeiyOuxg==$VNkrvP5rRl+qec+qOIoahglAPgJVxx6O2jfrDw18ZXw=', 'Participant'),
('anke.cyclist@raceday.co.za', 'PBKDF2-SHA256$100000$13r/WWHoz2jh/sSeiyOuxg==$VNkrvP5rRl+qec+qOIoahglAPgJVxx6O2jfrDw18ZXw=', 'Participant');
GO

-- Matching profiles
INSERT INTO dbo.UserProfiles (UserID, FullName, PhoneNumber, DateOfBirth, EmergencyContact) VALUES
(1, 'Thabo Nkosi', '0821234567', '1985-03-12', 'Nomvula Nkosi - 0827654321'),
(2, 'Lindiwe Dube', '0839876543', '1990-07-25', 'Bongani Dube - 0836543210'),
(3, 'Sipho Mthembu', '0715551234', '1995-11-02', 'Zanele Mthembu - 0715559876'),
(4, 'Anke van der Merwe', '0725558765', '1998-01-18', 'Pieter van der Merwe - 0725551234');
GO

-- Events: 3 events, owned by the 2 Organisers.
-- EventID 1 (Comrades) is in the future -> no results yet, matches an
-- open/upcoming enrolment. EventID 2 and 3 are in the past -> already have
-- captured results, matching a completed event.
INSERT INTO dbo.Events (OrganiserID, Name, Description, EventDate, Location, EventType, DistanceKm) VALUES
(1, 'Comrades Marathon', 'Iconic ultra-marathon between Pietermaritzburg and Durban.', '2027-06-13 05:30:00', 'Pietermaritzburg, KwaZulu-Natal', 'Run', 87.70),
(1, 'Soweto Marathon', 'Community road race through the streets of Soweto.', '2025-11-09 06:00:00', 'Soweto, Johannesburg', 'Run', 42.20),
(2, 'Cape Town Cycle Tour', 'Scenic cycling event around the Cape Peninsula.', '2026-03-08 06:00:00', 'Cape Town, Western Cape', 'Cycle', 109.00);
GO

-- Categories: multiple per event
INSERT INTO dbo.Categories (EventID, Name, DistanceKm, MaxParticipants, EntryFee) VALUES
(1, 'Comrades - Down Run', 87.70, 20000, 950.00),
(2, '10km Fun Run', 10.00, 2000, 150.00),
(2, '21km Half Marathon', 21.10, 3000, 250.00),
(3, '109km Full Tour', 109.00, 15000, 650.00),
(3, '56km Half Tour', 56.00, 8000, 450.00);
GO

-- Enrolments: participants enrolled into categories
INSERT INTO dbo.Enrolments (ParticipantID, CategoryID, Status) VALUES
(3, 1, 'Confirmed'),   -- Sipho -> Comrades Down Run (upcoming, no result yet)
(3, 3, 'Confirmed'),   -- Sipho -> Soweto 21km Half Marathon (completed)
(4, 4, 'Confirmed'),   -- Anke  -> Cycle Tour Full 109km (completed)
(4, 5, 'Confirmed');   -- Anke  -> Cycle Tour Half 56km (completed)
GO

-- Results: captured for the two completed events only. EnrolmentID 1
-- (Comrades) intentionally has no row here, since that event is still
-- upcoming. EnrolmentID 4 shows a non-finish, to exercise every value of
-- the Results.Status CHECK constraint (Finished, DNF, DSQ).
INSERT INTO dbo.Results (EnrolmentID, FinishTime, Position, Status) VALUES
(2, '01:45:32', 214, 'Finished'),
(3, '03:12:08', 501, 'Finished'),
(4, NULL, NULL, 'DNF');
GO

PRINT 'RaceDay schema created and seeded successfully.';
