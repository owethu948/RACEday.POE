using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using RaceDay.Web.Models.Api;
using System.Net.Sockets;

namespace RaceDay.Web.Services;

/// <summary>Typed HttpClient for the RaceDay API (registered with IHttpClientFactory in Program.cs).</summary>
public class RaceDayApiClient : IRaceDayApiClient
{
    /// <summary>Name of the cookie the API uses for its server-side session.</summary>
    public const string ApiSessionCookieName = ".RaceDay.Session";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public RaceDayApiClient(HttpClient http) => _http = http;

    // ================================================================= auth & profile
    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => _http.PostAsJsonAsync("api/auth/login", new LoginPayload { Email = email, Password = password }, JsonOptions, ct), ct);
        var user = await ReadAsync<AuthDto>(response, ct);

        var cookie = ExtractSessionCookie(response);
        if (cookie is null)
            throw new ApiException(502, "The RaceDay service did not start a session. Please try again.");

        return new LoginResult(user, cookie);
    }

    public async Task RegisterAsync(RegisterPayload payload, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => _http.PostAsJsonAsync("api/auth/register", payload, JsonOptions, ct), ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        using var response = await SendAsync(() => _http.PostAsync("api/auth/logout", content: null, ct), ct);
        await EnsureSuccessAsync(response, ct);
    }

    public Task<ProfileDto> GetProfileAsync(CancellationToken ct = default) => GetAsync<ProfileDto>("api/users/me", ct);

    public Task<ProfileDto> UpdateProfileAsync(UpdateProfilePayload payload, CancellationToken ct = default) =>
        PutAsync<ProfileDto>("api/users/me", payload, ct);

    // ================================================================= events
    public Task<PagedDto<EventDto>> GetEventsAsync(EventQuery query, CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["search"] = query.Search,
            ["location"] = query.Location,
            ["eventType"] = query.EventType,
            ["upcoming"] = query.Upcoming?.ToString().ToLowerInvariant(),
            ["organiserId"] = query.OrganiserId?.ToString(),
            ["page"] = query.Page.ToString(),
            ["pageSize"] = query.PageSize.ToString()
        };

        var present = parameters.Where(p => !string.IsNullOrWhiteSpace(p.Value))
                                .ToDictionary(p => p.Key, p => p.Value);

        return GetAsync<PagedDto<EventDto>>(QueryHelpers.AddQueryString("api/events", present), ct);
    }

    public Task<EventDetailDto> GetEventAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<EventDetailDto>($"api/events/{eventId}", ct);

    public Task<EventDto> CreateEventAsync(EventPayload payload, CancellationToken ct = default) =>
        PostAsync<EventDto>("api/events", payload, ct);

    public Task<EventDto> UpdateEventAsync(int eventId, EventPayload payload, CancellationToken ct = default) =>
        PutAsync<EventDto>($"api/events/{eventId}", payload, ct);

    public Task DeleteEventAsync(int eventId, CancellationToken ct = default) => DeleteAsync($"api/events/{eventId}", ct);

    // ================================================================= categories
    public Task<CategoryDto> CreateCategoryAsync(int eventId, CategoryPayload payload, CancellationToken ct = default) =>
        PostAsync<CategoryDto>($"api/events/{eventId}/categories", payload, ct);

    public Task<CategoryDto> UpdateCategoryAsync(int categoryId, CategoryPayload payload, CancellationToken ct = default) =>
        PutAsync<CategoryDto>($"api/categories/{categoryId}", payload, ct);

    public Task DeleteCategoryAsync(int categoryId, CancellationToken ct = default) => DeleteAsync($"api/categories/{categoryId}", ct);

    // ================================================================= enrolments
    public async Task<EnrolmentDto> EnrolAsync(int categoryId, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => _http.PostAsync($"api/categories/{categoryId}/enrolments", content: null, ct), ct);
        return await ReadAsync<EnrolmentDto>(response, ct);
    }

    public Task<List<EnrolmentDto>> GetMyEnrolmentsAsync(CancellationToken ct = default) =>
        GetAsync<List<EnrolmentDto>>("api/users/me/enrolments", ct);

    public Task<List<EnrolmentDto>> GetEventEnrolmentsAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<List<EnrolmentDto>>($"api/events/{eventId}/enrolments", ct);

    public Task CancelEnrolmentAsync(int enrolmentId, CancellationToken ct = default) => DeleteAsync($"api/enrolments/{enrolmentId}", ct);

    // ================================================================= results
    public Task<List<ResultDto>> GetEventResultsAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<List<ResultDto>>($"api/events/{eventId}/results", ct);

    public Task<List<ResultDto>> GetMyResultsAsync(CancellationToken ct = default) =>
        GetAsync<List<ResultDto>>("api/users/me/results", ct);

    public Task<ResultDto> CaptureResultAsync(int enrolmentId, ResultPayload payload, CancellationToken ct = default) =>
        PostAsync<ResultDto>($"api/enrolments/{enrolmentId}/result", payload, ct);

    public Task<ResultDto> UpdateResultAsync(int resultId, ResultPayload payload, CancellationToken ct = default) =>
        PutAsync<ResultDto>($"api/results/{resultId}", payload, ct);

    // ================================================================= weather
    public Task<WeatherDto> GetWeatherAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<WeatherDto>($"api/events/{eventId}/weather", ct);

    // ================================================================= files
    public async Task<string> UploadImageAsync(IFormFile file, ImageKind kind, CancellationToken ct = default)
    {
        var path = kind == ImageKind.Banner ? "api/files/banner" : "api/files/profile-picture";

        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        using var fileContent = new StreamContent(stream);
        if (MediaTypeHeaderValue.TryParse(file.ContentType, out var mediaType))
            fileContent.Headers.ContentType = mediaType;
        form.Add(fileContent, "file", Path.GetFileName(file.FileName));

        using var response = await SendAsync(() => _http.PostAsync(path, form, ct), ct);
        var result = await ReadAsync<UploadDto>(response, ct);
        return result.Url;
    }

    // ================================================================= plumbing
    internal static string? ExtractSessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return null;

        foreach (var header in values)
        {
            var pair = header.Split(';', 2)[0].Trim();
            if (pair.StartsWith(ApiSessionCookieName + "=", StringComparison.Ordinal))
                return pair;
        }

        return null;
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await SendAsync(() => _http.GetAsync(url, ct), ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<T> PostAsync<T>(string url, object payload, CancellationToken ct)
    {
        using var response = await SendAsync(() => _http.PostAsJsonAsync(url, payload, JsonOptions, ct), ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<T> PutAsync<T>(string url, object payload, CancellationToken ct)
    {
        using var response = await SendAsync(() => _http.PutAsJsonAsync(url, payload, JsonOptions, ct), ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task DeleteAsync(string url, CancellationToken ct)
    {
        using var response = await SendAsync(() => _http.DeleteAsync(url, ct), ct);
        await EnsureSuccessAsync(response, ct);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken ct)
    {
        try
        {
            return await send();
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(503,
                "The RaceDay service is temporarily unavailable. " +
                "If you are already registered, go to Home and start. " +
                "If you are not registered yet, please log in or register.", ex);
        }
        catch (TaskCanceledException ex)
        {
            // FIX: If the user refreshed or navigated away, don't crash the app. Throw a 499.
            if (ct.IsCancellationRequested)
            {
                throw new ApiException(499, "The request was cancelled by the user.", ex);
            }

            throw new ApiException(503,
                "The RaceDay service took too long to respond. " +
                "If you are already registered, go to Home and start. " +
                "If you are not registered yet, please log in or register.", ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
            throw await ApiException.FromResponseAsync(response, ct);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);

        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return value ?? throw new ApiException(502, "The RaceDay service returned an empty response.");
    }
}