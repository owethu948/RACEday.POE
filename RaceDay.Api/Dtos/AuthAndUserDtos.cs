using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Body for POST /api/auth/register.</summary>
public class RegisterRequest
{
    /// <summary>The user's full name, e.g. "Sipho Mthembu".</summary>
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Unique email address, used to log in.</summary>
    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    /// <summary>At least 8 characters. Stored only as a salted hash.</summary>
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Registration code issued by RaceDay. Required only when Role is "Organiser", so that
    /// not just anyone can give themselves organiser powers. Leave empty for Participants.
    /// </summary>
    public string? OrganiserCode { get; set; }
}

/// <summary>Body for POST /api/auth/login.</summary>
public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Returned after a successful registration or login.</summary>
public class AuthResponse
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

/// <summary>The logged-in user's full profile.</summary>
public class ProfileResponse
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? EmergencyContact { get; set; }
    public string? ProfilePictureUrl { get; set; }
}

/// <summary>Body for PUT /api/users/me.</summary>
public class UpdateProfileRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>Format yyyy-MM-dd.</summary>
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(150)]
    public string? EmergencyContact { get; set; }

    /// <summary>Optional picture URL, as returned by POST /api/files/profile-picture. Null or empty removes it.</summary>
    [Url, StringLength(500)]
    public string? ProfilePictureUrl { get; set; }
}

/// <summary>Limited profile an Organiser may see for another user.</summary>
public class PublicProfileResponse
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
