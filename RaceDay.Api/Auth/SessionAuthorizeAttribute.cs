using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Auth;

/// <summary>
/// Session-based authorisation. Reads the user id and role that were stored in the
/// server-side session at login:
///   * no session / not logged in            -> 401 Unauthorized
///   * logged in but role not in <see cref="Roles"/> -> 403 Forbidden
/// Use with no roles to allow any logged-in user.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    /// <summary>Comma-separated list of roles allowed (e.g. "Organiser"). Null/empty = any logged-in user.</summary>
    public string? Roles { get; set; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        if (userId is null || string.IsNullOrEmpty(role))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "You must be logged in to access this resource."
            })
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        if (!string.IsNullOrWhiteSpace(Roles))
        {
            var allowed = Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!allowed.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = $"This action requires the role: {Roles}. You are logged in as {role}."
                })
                { StatusCode = StatusCodes.Status403Forbidden };
            }
        }
    }
}
