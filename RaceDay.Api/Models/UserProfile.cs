namespace RaceDay.Api.Models;

/// <summary>Maps to dbo.UserProfiles - 1:1 extension of Users.</summary>
public class UserProfile
{
    public int ProfileId { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? EmergencyContact { get; set; }

    /// <summary>Public Azure Blob Storage URL of the profile picture (null = none).</summary>
    public string? ProfilePictureUrl { get; set; }

    public AppUser User { get; set; } = null!;
}
