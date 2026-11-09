namespace RaceDay.Api.Models;

/// <summary>Maps to dbo.Users - login data for both Organisers and Participants.</summary>
public class AppUser
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash of the password - the plain password is never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = Roles.Participant;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public UserProfile? Profile { get; set; }
    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
