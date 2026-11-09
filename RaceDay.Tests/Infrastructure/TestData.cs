using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests.Infrastructure;

/// <summary>A user who is registered and logged in; <see cref="Client"/> carries the session cookie.</summary>
public sealed record TestUser(HttpClient Client, string Email, int UserId, string Role);

/// <summary>Helpers that keep the individual tests short and readable.</summary>
public static class TestData
{
    public const string Password = "Password123!";

    public static string UniqueEmail(string prefix) => $"{prefix}.{Guid.NewGuid():N}@test.raceday.co.za";

    // ------------------------------------------------------------------ auth
    public static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client, string role, string? email = null, string? password = null, string? organiserCode = null)
    {
        var request = new RegisterRequest
        {
            FullName = "Test User",
            Email = email ?? UniqueEmail(role.ToLowerInvariant()),
            Password = password ?? Password,
            Role = role,
            OrganiserCode = organiserCode
        };
        return client.PostAsJsonAsync("/api/auth/register", request);
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string? password = null) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password ?? Password });

    /// <summary>Registers a fresh user with the given role, logs in and returns a client holding the session.</summary>
    public static async Task<TestUser> SignInAsync(RaceDayApiFactory factory, string role)
    {
        var client = factory.CreateClient();
        var email = UniqueEmail(role.ToLowerInvariant());
        var code = role == Roles.Organiser ? RaceDayApiFactory.OrganiserCode : null;

        var register = await RegisterAsync(client, role, email, organiserCode: code);
        register.EnsureSuccessStatusCode();

        var login = await LoginAsync(client, email);
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return new TestUser(client, email, auth!.UserId, role);
    }

    public static Task<TestUser> SignInOrganiserAsync(RaceDayApiFactory factory) => SignInAsync(factory, Roles.Organiser);
    public static Task<TestUser> SignInParticipantAsync(RaceDayApiFactory factory) => SignInAsync(factory, Roles.Participant);

    // ---------------------------------------------------------------- events
    public static EventRequest NewEvent(string? name = null, DateTime? date = null, string type = "Run") => new()
    {
        Name = name ?? $"Test Event {Guid.NewGuid():N}",
        Description = "A test road event.",
        EventDate = date ?? DateTime.Now.AddMonths(3),
        Location = "Polokwane, Limpopo",
        EventType = type,
        DistanceKm = 10m
    };

    public static async Task<EventResponse> CreateEventAsync(HttpClient organiser, EventRequest? request = null)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", request ?? NewEvent());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
    }

    public static CategoryRequest NewCategory(int maxParticipants = 100, string name = "10km") => new()
    {
        Name = name,
        DistanceKm = 10m,
        MaxParticipants = maxParticipants,
        EntryFee = 150m
    };

    public static async Task<CategoryResponse> AddCategoryAsync(HttpClient organiser, int eventId, CategoryRequest? request = null)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", request ?? NewCategory());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    /// <summary>Creates a future event with one category owned by <paramref name="organiser"/>.</summary>
    public static async Task<(EventResponse Event, CategoryResponse Category)> CreateEventWithCategoryAsync(
        HttpClient organiser, int maxParticipants = 100)
    {
        var ev = await CreateEventAsync(organiser);
        var category = await AddCategoryAsync(organiser, ev.EventId, NewCategory(maxParticipants));
        return (ev, category);
    }

    // ------------------------------------------------------------ enrolments
    public static async Task<EnrolmentResponse> EnrolAsync(HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsync($"/api/categories/{categoryId}/enrolments", content: null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrolmentResponse>())!;
    }

    // --------------------------------------------------------------- generic
    /// <summary>Sends any verb with an optional JSON body (POST/PUT default to an empty object).</summary>
    public static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        else if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(new { });
        return client.SendAsync(request);
    }
}
