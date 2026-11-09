namespace RaceDay.Web.Services;

/// <summary>Names of the values kept in the web application's session.</summary>
public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
    public const string DisplayName = "DisplayName";
    /// <summary>The "name=value" of the API's session cookie for this user.</summary>
    public const string ApiCookie = "ApiSessionCookie";
}

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

/// <summary>Read-only view of who is signed in, available to controllers and Razor views.</summary>
public interface ISessionUser
{
    bool IsAuthenticated { get; }
    int UserId { get; }
    string Role { get; }
    string DisplayName { get; }
    string Initials { get; }
    bool IsOrganiser { get; }
    bool IsParticipant { get; }
}

public class SessionUser : ISessionUser
{
    private readonly IHttpContextAccessor _accessor;

    public SessionUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ISession? Session
    {
        get
        {
            var context = _accessor.HttpContext;
            // Session middleware may not have run (e.g. static files, error pages).
            return context?.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>() is null ? null : context.Session;
        }
    }

    public bool IsAuthenticated => UserId > 0 && !string.IsNullOrEmpty(Role);
    public int UserId => Session?.GetInt32(SessionKeys.UserId) ?? 0;
    public string Role => Session?.GetString(SessionKeys.Role) ?? string.Empty;
    public string DisplayName => Session?.GetString(SessionKeys.DisplayName) ?? string.Empty;
    public bool IsOrganiser => IsAuthenticated && string.Equals(Role, Roles.Organiser, StringComparison.OrdinalIgnoreCase);
    public bool IsParticipant => IsAuthenticated && string.Equals(Role, Roles.Participant, StringComparison.OrdinalIgnoreCase);

    public string Initials
    {
        get
        {
            var parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant()
            };
        }
    }
}

/// <summary>Writes and clears the signed-in state. One place, so login, register and logout cannot drift apart.</summary>
public static class SessionWriter
{
    public static void SignIn(ISession session, Models.Api.LoginResult result)
    {
        session.Clear();
        session.SetInt32(SessionKeys.UserId, result.User.UserId);
        session.SetString(SessionKeys.Role, result.User.Role);
        session.SetString(SessionKeys.DisplayName, result.User.FullName);
        session.SetString(SessionKeys.ApiCookie, result.ApiSessionCookie);
    }

    public static void SetDisplayName(ISession session, string displayName) =>
        session.SetString(SessionKeys.DisplayName, displayName);
}
