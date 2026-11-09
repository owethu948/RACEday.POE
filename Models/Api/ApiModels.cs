namespace RaceDay.Web.Models.Api;

// These classes mirror the JSON contract of the RaceDay API (Part 2). They are plain data holders:
// no database types and no business rules live in the web project.

public class AuthDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class ProfileDto
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

public class EventDto
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public string? BannerUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public int EnrolmentCount { get; set; }
    public int ResultCount { get; set; }
    public decimal TotalEntryFees { get; set; }
}

public class EventDetailDto : EventDto
{
    public List<CategoryDto> Categories { get; set; } = new();
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int MaxParticipants { get; set; }
    public decimal EntryFee { get; set; }
    public int EnrolledCount { get; set; }
}

public class PagedDto<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<T> Items { get; set; } = new();

    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasResult { get; set; }
}

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class WeatherDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string PlaceName { get; set; } = string.Empty;
    public bool IsEventDayForecast { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public double TemperatureC { get; set; }
    public double? LowC { get; set; }
    public double? HighC { get; set; }
    public double WindKmh { get; set; }
    public int? HumidityPercent { get; set; }
    public int? RainChancePercent { get; set; }
    public double? UvIndex { get; set; }
    public string Conditions { get; set; } = string.Empty;
    public List<string> Tips { get; set; } = new();
}

public class UploadDto
{
    public string Url { get; set; } = string.Empty;
}

// ------------------------------------------------------------------ request bodies

public class LoginPayload
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterPayload
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? OrganiserCode { get; set; }
}

public class UpdateProfilePayload
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? EmergencyContact { get; set; }
    public string? ProfilePictureUrl { get; set; }
}

public class EventPayload
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    /// <summary>Must be null (not an empty string) when there is no banner: the API validates non-null values as URLs.</summary>
    public string? BannerUrl { get; set; }
}

public class CategoryPayload
{
    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int MaxParticipants { get; set; }
    public decimal EntryFee { get; set; }
}

public class ResultPayload
{
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Filters for GET api/events.</summary>
public class EventQuery
{
    public string? Search { get; set; }
    public string? Location { get; set; }
    public string? EventType { get; set; }
    public bool? Upcoming { get; set; }
    public int? OrganiserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Which API endpoint an uploaded image goes to.</summary>
public enum ImageKind
{
    Banner,
    ProfilePicture
}

/// <summary>The signed-in user plus the API session cookie that proves it.</summary>
public sealed record LoginResult(AuthDto User, string ApiSessionCookie);
