namespace RaceDay.Api.Models;

/// <summary>The two roles in the RaceDay system (stored in Users.Role).</summary>
public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    public static readonly string[] All = { Organiser, Participant };
}

/// <summary>Allowed values for Events.EventType.</summary>
public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";

    public static readonly string[] All = { Run, Walk, Cycle };
}

/// <summary>Allowed values for Enrolments.Status (matches the Part 1 CHECK constraint).</summary>
public static class EnrolmentStatuses
{
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}

/// <summary>Allowed values for Results.Status (matches the Part 1 CHECK constraint).</summary>
public static class ResultStatuses
{
    public const string Finished = "Finished";
    public const string DNF = "DNF";
    public const string DSQ = "DSQ";

    public static readonly string[] All = { Finished, DNF, DSQ };
}

public static class Lookup
{
    /// <summary>Case-insensitive match against a list of allowed values; returns the canonical value or null.</summary>
    public static string? Match(string[] allowed, string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return allowed.FirstOrDefault(a => string.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
