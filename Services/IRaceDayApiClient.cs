using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services;

/// <summary>
/// The ONLY way the web application reads or writes data: HTTP calls to the RaceDay API.
/// Controllers depend on this interface, never on HttpClient, a database or Azure.
/// </summary>
public interface IRaceDayApiClient
{
    // ---- auth & profile
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default);
    Task RegisterAsync(RegisterPayload payload, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<ProfileDto> GetProfileAsync(CancellationToken ct = default);
    Task<ProfileDto> UpdateProfileAsync(UpdateProfilePayload payload, CancellationToken ct = default);

    // ---- events
    Task<PagedDto<EventDto>> GetEventsAsync(EventQuery query, CancellationToken ct = default);
    Task<EventDetailDto> GetEventAsync(int eventId, CancellationToken ct = default);
    Task<EventDto> CreateEventAsync(EventPayload payload, CancellationToken ct = default);
    Task<EventDto> UpdateEventAsync(int eventId, EventPayload payload, CancellationToken ct = default);
    Task DeleteEventAsync(int eventId, CancellationToken ct = default);

    // ---- categories
    Task<CategoryDto> CreateCategoryAsync(int eventId, CategoryPayload payload, CancellationToken ct = default);
    Task<CategoryDto> UpdateCategoryAsync(int categoryId, CategoryPayload payload, CancellationToken ct = default);
    Task DeleteCategoryAsync(int categoryId, CancellationToken ct = default);

    // ---- enrolments
    Task<EnrolmentDto> EnrolAsync(int categoryId, CancellationToken ct = default);
    Task<List<EnrolmentDto>> GetMyEnrolmentsAsync(CancellationToken ct = default);
    Task<List<EnrolmentDto>> GetEventEnrolmentsAsync(int eventId, CancellationToken ct = default);
    Task CancelEnrolmentAsync(int enrolmentId, CancellationToken ct = default);

    // ---- results
    Task<List<ResultDto>> GetEventResultsAsync(int eventId, CancellationToken ct = default);
    Task<List<ResultDto>> GetMyResultsAsync(CancellationToken ct = default);
    Task<ResultDto> CaptureResultAsync(int enrolmentId, ResultPayload payload, CancellationToken ct = default);
    Task<ResultDto> UpdateResultAsync(int resultId, ResultPayload payload, CancellationToken ct = default);

    // ---- weather
    Task<WeatherDto> GetWeatherAsync(int eventId, CancellationToken ct = default);

    // ---- files (forwarded to the API, which stores them in Azure Blob Storage)
    Task<string> UploadImageAsync(IFormFile file, ImageKind kind, CancellationToken ct = default);
}
