using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Profile of the logged-in user, and limited lookups of other users.</summary>
public class UsersController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public UsersController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Get the profile of the logged-in user.</summary>
    /// <response code="200">The caller's profile.</response>
    [HttpGet("api/users/me")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe()
    {
        var user = await _db.Users.AsNoTracking().Include(u => u.Profile).FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return Fail(401, "Your account no longer exists. Please log in again.");
        return Ok(ToProfile(user));
    }

    /// <summary>Update the profile of the logged-in user.</summary>
    /// <remarks>Both Organisers and Participants can update their own full name, phone number, date of birth (yyyy-MM-dd) and emergency contact.</remarks>
    /// <param name="request">The new profile values.</param>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation failed.</response>
    [HttpPut("api/users/me")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return Fail(401, "Your account no longer exists. Please log in again.");

        user.Profile ??= new UserProfile { UserId = user.UserId };
        user.Profile.FullName = request.FullName.Trim();
        user.Profile.PhoneNumber = request.PhoneNumber?.Trim();
        user.Profile.DateOfBirth = request.DateOfBirth;
        user.Profile.EmergencyContact = request.EmergencyContact?.Trim();
        user.Profile.ProfilePictureUrl = string.IsNullOrWhiteSpace(request.ProfilePictureUrl) ? null : request.ProfilePictureUrl.Trim();

        await _db.SaveChangesAsync();
        return Ok(ToProfile(user));
    }

    /// <summary>Get another user's public profile (Organiser only).</summary>
    /// <remarks>Used by an Organiser reviewing enrolments. Returns only the id, full name and role.</remarks>
    /// <param name="id">The user id.</param>
    /// <response code="200">The user's public profile.</response>
    /// <response code="404">No user with that id.</response>
    [HttpGet("api/users/{id:int}")]
    [SessionAuthorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(PublicProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _db.Users.AsNoTracking().Include(u => u.Profile).FirstOrDefaultAsync(u => u.UserId == id);
        if (user is null) return Fail(404, $"User {id} was not found.");

        return Ok(new PublicProfileResponse
        {
            UserId = user.UserId,
            FullName = user.Profile?.FullName ?? user.Email,
            Role = user.Role
        });
    }

    private static ProfileResponse ToProfile(AppUser user) => new()
    {
        UserId = user.UserId,
        Email = user.Email,
        Role = user.Role,
        FullName = user.Profile?.FullName ?? string.Empty,
        PhoneNumber = user.Profile?.PhoneNumber,
        DateOfBirth = user.Profile?.DateOfBirth,
        EmergencyContact = user.Profile?.EmergencyContact,
        ProfilePictureUrl = user.Profile?.ProfilePictureUrl
    };
}
