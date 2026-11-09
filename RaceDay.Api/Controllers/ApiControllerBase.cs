using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;

namespace RaceDay.Api.Controllers;

/// <summary>Shared helpers for all RaceDay controllers.</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the logged-in user, read from the server-side session (0 if not logged in).</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    /// <summary>Role of the logged-in user, read from the server-side session.</summary>
    protected string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);

    /// <summary>Returns an RFC 7807 problem response with the given status code and message.</summary>
    protected ObjectResult Fail(int statusCode, string detail)
    {
        var title = statusCode switch
        {
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            413 => "Payload Too Large",
            415 => "Unsupported Media Type",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            _ => "Error"
        };
        return Problem(detail: detail, statusCode: statusCode, title: title);
    }
}
