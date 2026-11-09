using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout.</summary>
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IConfiguration _config;

    public AuthController(RaceDayDbContext db, IPasswordService passwords, IConfiguration config)
    {
        _db = db;
        _passwords = passwords;
        _config = config;
    }

    /// <summary>Register a new Organiser or Participant account.</summary>
    /// <remarks>
    /// Creates the user and the matching profile row in a single transaction. The password is
    /// stored only as a salted PBKDF2 hash.
    ///
    /// Anyone may register as a **Participant**. To stop people simply promoting themselves,
    /// registering as an **Organiser** also requires the registration code in `organiserCode`
    /// (configured under `Auth:OrganiserRegistrationCode`; see the README).
    /// </remarks>
    /// <param name="request">Full name, email, password (min 8 chars), role ("Organiser" or "Participant") and, for Organisers, the organiser code.</param>
    /// <response code="201">Account created. Log in next to start a session.</response>
    /// <response code="400">Validation failed (e.g. invalid email, short password, unknown role).</response>
    /// <response code="403">Role is Organiser but the organiser code is missing or wrong.</response>
    /// <response code="409">An account with that email already exists.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var role = Lookup.Match(Roles.All, request.Role);
        if (role is null)
            return Fail(400, "Role must be either 'Organiser' or 'Participant'.");

        if (role == Roles.Organiser)
        {
            var requiredCode = _config["Auth:OrganiserRegistrationCode"];
            if (!string.IsNullOrEmpty(requiredCode) && request.OrganiserCode != requiredCode)
                return Fail(403, "A valid organiser registration code is required to register as an Organiser.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Fail(409, "An account with that email address already exists.");

        var user = new AppUser
        {
            Email = email,
            PasswordHash = _passwords.Hash(request.Password),
            Role = role,
            CreatedAt = DateTime.Now,
            Profile = new UserProfile { FullName = request.FullName.Trim() }
        };
        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two registrations with the same email at the same moment hit the unique index.
            return Fail(409, "An account with that email address already exists.");
        }

        var response = new AuthResponse
        {
            UserId = user.UserId,
            FullName = user.Profile.FullName,
            Email = user.Email,
            Role = user.Role
        };
        return Created("/api/users/me", response);
    }

    /// <summary>Log in with email and password.</summary>
    /// <remarks>
    /// On success the server creates a **server-side session** and stores the user's id and role in it.
    /// The browser keeps the session cookie (`.RaceDay.Session`), so every later request - including
    /// requests made from the Swagger UI - is authenticated automatically. Sessions expire after 30 minutes of inactivity.
    /// </remarks>
    /// <param name="request">Email and password.</param>
    /// <response code="200">Logged in. The response shows the user's id, name and role.</response>
    /// <response code="400">Email or password missing.</response>
    /// <response code="401">Email or password is incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !_passwords.Verify(request.Password, user.PasswordHash))
            return Fail(401, "Invalid email or password.");

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);

        return Ok(new AuthResponse
        {
            UserId = user.UserId,
            FullName = user.Profile?.FullName ?? user.Email,
            Email = user.Email,
            Role = user.Role
        });
    }

    /// <summary>Log out and end the current session.</summary>
    /// <remarks>Clears the server-side session, so the cookie no longer authenticates any request.</remarks>
    /// <response code="204">Session ended.</response>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }
}
