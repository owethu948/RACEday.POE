using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests;

/// <summary>Minimal in-memory ISession so filters and handlers can be tested without a web server.</summary>
internal sealed class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _values = [];

    public bool IsAvailable => true;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public IEnumerable<string> Keys => _values.Keys;
    public void Clear() => _values.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _values.Remove(key);
    public void Set(string key, byte[] value) => _values[key] = value;
    public bool TryGetValue(string key, out byte[] value)
    {
        if (_values.TryGetValue(key, out var found))
        {
            value = found;
            return true;
        }

        value = Array.Empty<byte>();
        return false;
    }
}

internal sealed class TestSessionFeature : ISessionFeature
{
    public ISession Session { get; set; } = new TestSession();
}

internal static class TestHttp
{
    public static DefaultHttpContext ContextWithSession(Action<ISession>? configure = null)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        var feature = new TestSessionFeature();
        context.Features.Set<ISessionFeature>(feature);
        configure?.Invoke(feature.Session);
        return context;
    }
}

/// <summary>Replies with whatever the supplied function returns and remembers every request it saw.</summary>
public sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _respond;

    public StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) => _respond = respond;

    public List<(HttpRequestMessage Request, string Body)> Seen { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Seen.Add((request, body));
        return _respond(request, body);
    }
}

/// <summary>A pretend RaceDay API: enough behaviour to drive the whole web app in integration tests.</summary>
public sealed class FakeApiHandler : HttpMessageHandler
{
    public const string SessionCookieName = ".RaceDay.Session";

    private readonly bool _weatherDown;

    public FakeApiHandler(bool weatherDown = false) => _weatherDown = weatherDown;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.ToLowerInvariant();
        var signedIn = request.Headers.TryGetValues("Cookie", out var cookies) && cookies.Any(c => c.Contains(SessionCookieName + "="));

        HttpResponseMessage response;
        if (path == "/api/auth/login" && request.Method == HttpMethod.Post)
            response = Login(request);
        else if (path == "/api/auth/logout")
            response = new HttpResponseMessage(HttpStatusCode.NoContent);
        else if (path == "/api/events" && request.Method == HttpMethod.Get)
            response = Json(Paged());
        else if (path == "/api/users/me/enrolments")
            response = signedIn ? Json(Array.Empty<object>()) : Problem(HttpStatusCode.Unauthorized, "You must be logged in to access this resource.");
        else if (path == "/api/events/1/weather")
            response = _weatherDown ? Problem(HttpStatusCode.ServiceUnavailable, "The weather service is not available right now.") : Json(Weather());
        else if (path == "/api/events/1" && request.Method == HttpMethod.Get)
            response = Json(Detail());
        else if (path == "/api/categories/5/enrolments" && request.Method == HttpMethod.Post)
            response = signedIn ? Json(Enrolment()) : Problem(HttpStatusCode.Unauthorized, "You must be logged in to access this resource.");
        else if (path == "/api/events/1/enrolments" || path == "/api/events/1/results")
            response = Json(Array.Empty<object>());
        else if (path == "/api/users/me/results")
            response = signedIn ? Json(Array.Empty<object>()) : Problem(HttpStatusCode.Unauthorized, "You must be logged in to access this resource.");
        else
            response = Problem(HttpStatusCode.NotFound, "Not found in the fake API.");

        return Task.FromResult(response);
    }

    private static HttpResponseMessage Login(HttpRequestMessage request)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        var email = doc.RootElement.GetProperty("email").GetString();
        var password = doc.RootElement.GetProperty("password").GetString();

        (int Id, string Name, string Role)? user = (email, password) switch
        {
            ("organiser@test.co.za", "Password123!") => (10, "Olivia Organiser", "Organiser"),
            ("runner@test.co.za", "Password123!") => (20, "Ruth Runner", "Participant"),
            _ => null
        };

        if (user is null) return Problem(HttpStatusCode.Unauthorized, "Invalid email or password.");

        var response = Json(body: new { UserId = user.Value.Id, FullName = user.Value.Name, Email = email, Role = user.Value.Role });
        response.Headers.Add("Set-Cookie", $"{SessionCookieName}=session-{user.Value.Id}; path=/; samesite=lax; httponly");
        return response;
    }

    private static object Paged() => new
    {
        Page = 1,
        PageSize = 9,
        TotalCount = 1,
        Items = new[]
        {
            new
            {
                EventId = 1, OrganiserId = 10, Name = "Stub Marathon", Description = "A pretend race.",
                EventDate = DateTime.Now.AddMonths(2), Location = "Polokwane", EventType = "Run", DistanceKm = 42.2m,
                BannerUrl = (string?)null, CreatedAt = DateTime.Now, EnrolmentCount = 5
            }
        }
    };

    private static object Weather() => new
    {
        EventId = 1, EventName = "Stub Marathon", EventDate = DateTime.Now.AddMonths(2), Location = "Polokwane", PlaceName = "Polokwane",
        IsEventDayForecast = false, Summary = "Partly cloudy", Icon = "\u26C5", TemperatureC = 18.0, LowC = (double?)null, HighC = (double?)null,
        WindKmh = 10.0, HumidityPercent = (int?)50, RainChancePercent = (int?)null, UvIndex = (double?)null,
        Conditions = "Excellent", Tips = new[] { "Ideal temperatures. Start hydrating the night before." }
    };

    private static object Detail() => new
    {
        EventId = 1, OrganiserId = 10, Name = "Stub Marathon", Description = "A pretend race.",
        EventDate = DateTime.Now.AddMonths(2), Location = "Polokwane", EventType = "Run", DistanceKm = 42.2m,
        BannerUrl = (string?)null, CreatedAt = DateTime.Now, EnrolmentCount = 5,
        Categories = new[] { new { CategoryId = 5, EventId = 1, Name = "21km", DistanceKm = 21.1m, MaxParticipants = 100, EntryFee = 250m, EnrolledCount = 5 } }
    };

    private static object Enrolment() => new
    {
        EnrolmentId = 77, ParticipantId = 20, ParticipantName = "Ruth Runner", CategoryId = 5, CategoryName = "21km", DistanceKm = 21.1m, EntryFee = 250m,
        EventId = 1, EventName = "Stub Marathon", EventDate = DateTime.Now.AddMonths(2), EnrolmentDate = DateTime.Now, Status = "Confirmed", HasResult = false
    };

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private static HttpResponseMessage Problem(HttpStatusCode status, string detail) =>
        new(status) { Content = JsonContent.Create(new { Title = status.ToString(), Status = (int)status, Detail = detail }) };
}

/// <summary>Boots the real web app but replaces the API's HTTP handler with a stub.</summary>
public sealed class WebFactory : WebApplicationFactory<Program>
{
    private readonly Func<HttpMessageHandler> _apiHandler;

    public WebFactory() : this(null) { }

    internal WebFactory(Func<HttpMessageHandler>? apiHandler) => _apiHandler = apiHandler ?? (() => new FakeApiHandler());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<IRaceDayApiClient, RaceDayApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(_apiHandler);
        });
    }

    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });
}

public static class BrowserExtensions
{
    #pragma warning disable SYSLIB1045
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled);
    #pragma warning restore SYSLIB1045

    public static async Task<string> GetTokenAsync(this HttpClient browser, string url)
    {
        var html = await browser.GetStringAsync(url);
        var match = Token.Match(html);
        Assert.True(match.Success, $"No anti-forgery token found on {url}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public static async Task<HttpResponseMessage> LoginAsync(this HttpClient browser, string email, string password = "Password123!")
    {
        var token = await browser.GetTokenAsync("/Account/Login");
        return await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }));
    }

    public static string Location(this HttpResponseMessage response) => response.Headers.Location?.OriginalString ?? string.Empty;
}
