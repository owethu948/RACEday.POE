using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Data;

/// <summary>
/// Seeds the same demo data as the Part 1 SQL script (2 Organisers, 2 Participants,
/// 3 events, categories, enrolments, results) - but with real password hashes.
/// Every seeded account uses the password <see cref="DemoPassword"/>.
/// Only runs when the Users table is empty.
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Password123!";

    public static async Task SeedAsync(RaceDayDbContext db, IPasswordService passwords)
    {
        if (await db.Users.AnyAsync()) return;

        var hash = passwords.Hash(DemoPassword);

        var thabo = new AppUser
        {
            Email = "thabo.organiser@raceday.co.za", PasswordHash = hash, Role = Roles.Organiser,
            Profile = new UserProfile { FullName = "Thabo Nkosi", PhoneNumber = "0821234567", DateOfBirth = new DateOnly(1985, 3, 12), EmergencyContact = "Nomvula Nkosi - 0827654321" }
        };
        var lindiwe = new AppUser
        {
            Email = "lindiwe.organiser@raceday.co.za", PasswordHash = hash, Role = Roles.Organiser,
            Profile = new UserProfile { FullName = "Lindiwe Dube", PhoneNumber = "0839876543", DateOfBirth = new DateOnly(1990, 7, 25), EmergencyContact = "Bongani Dube - 0836543210" }
        };
        var sipho = new AppUser
        {
            Email = "sipho.runner@raceday.co.za", PasswordHash = hash, Role = Roles.Participant,
            Profile = new UserProfile { FullName = "Sipho Mthembu", PhoneNumber = "0715551234", DateOfBirth = new DateOnly(1995, 11, 2), EmergencyContact = "Zanele Mthembu - 0715559876" }
        };
        var anke = new AppUser
        {
            Email = "anke.cyclist@raceday.co.za", PasswordHash = hash, Role = Roles.Participant,
            Profile = new UserProfile { FullName = "Anke van der Merwe", PhoneNumber = "0725558765", DateOfBirth = new DateOnly(1998, 1, 18), EmergencyContact = "Pieter van der Merwe - 0725551234" }
        };

        var comrades = new Event
        {
            Organiser = thabo, Name = "Comrades Marathon",
            Description = "Iconic ultra-marathon between Pietermaritzburg and Durban.",
            EventDate = new DateTime(2027, 6, 13, 5, 30, 0), Location = "Pietermaritzburg, KwaZulu-Natal",
            EventType = EventTypes.Run, DistanceKm = 87.70m
        };
        var soweto = new Event
        {
            Organiser = thabo, Name = "Soweto Marathon",
            Description = "Community road race through the streets of Soweto.",
            EventDate = new DateTime(2025, 11, 9, 6, 0, 0), Location = "Soweto, Johannesburg",
            EventType = EventTypes.Run, DistanceKm = 42.20m
        };
        var cycleTour = new Event
        {
            Organiser = lindiwe, Name = "Cape Town Cycle Tour",
            Description = "Scenic cycling event around the Cape Peninsula.",
            EventDate = new DateTime(2026, 3, 8, 6, 0, 0), Location = "Cape Town, Western Cape",
            EventType = EventTypes.Cycle, DistanceKm = 109.00m
        };

        var comradesDown = new Category { Event = comrades, Name = "Comrades - Down Run", DistanceKm = 87.70m, MaxParticipants = 20000, EntryFee = 950.00m };
        var tenKm = new Category { Event = soweto, Name = "10km Fun Run", DistanceKm = 10.00m, MaxParticipants = 2000, EntryFee = 150.00m };
        var halfMarathon = new Category { Event = soweto, Name = "21km Half Marathon", DistanceKm = 21.10m, MaxParticipants = 3000, EntryFee = 250.00m };
        var fullTour = new Category { Event = cycleTour, Name = "109km Full Tour", DistanceKm = 109.00m, MaxParticipants = 15000, EntryFee = 650.00m };
        var halfTour = new Category { Event = cycleTour, Name = "56km Half Tour", DistanceKm = 56.00m, MaxParticipants = 8000, EntryFee = 450.00m };

        var e1 = new Enrolment { Participant = sipho, Category = comradesDown, Status = EnrolmentStatuses.Confirmed };
        var e2 = new Enrolment { Participant = sipho, Category = halfMarathon, Status = EnrolmentStatuses.Confirmed };
        var e3 = new Enrolment { Participant = anke, Category = fullTour, Status = EnrolmentStatuses.Confirmed };
        var e4 = new Enrolment { Participant = anke, Category = halfTour, Status = EnrolmentStatuses.Confirmed };

        db.Users.AddRange(thabo, lindiwe, sipho, anke);
        db.Events.AddRange(comrades, soweto, cycleTour);
        db.Categories.AddRange(comradesDown, tenKm, halfMarathon, fullTour, halfTour);
        db.Enrolments.AddRange(e1, e2, e3, e4);

        db.Results.AddRange(
            new Result { Enrolment = e2, FinishTime = new TimeSpan(1, 45, 32), Position = 214, Status = ResultStatuses.Finished },
            new Result { Enrolment = e3, FinishTime = new TimeSpan(3, 12, 8), Position = 501, Status = ResultStatuses.Finished },
            new Result { Enrolment = e4, FinishTime = null, Position = null, Status = ResultStatuses.DNF });

        await db.SaveChangesAsync();
    }
}
