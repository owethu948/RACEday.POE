using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests;

/// <summary>The typed HttpClient: what it sends to the API and how it turns replies and errors into something usable.</summary>
public class ApiClientTests
{
    private static RaceDayApiClient ClientFor(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") });

    private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = JsonContent.Create(body) };

    private static IFormFile PngFile(string name = "banner.png", string contentType = "image/png")
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task Login_ReturnsTheUserAndTheApiSessionCookie()
    {
        var handler = new StubHandler((_, _) =>
        {
            var response = Json(new { UserId = 7, FullName = "Test User", Email = "t@test.co.za", Role = "Organiser" });
            response.Headers.Add("Set-Cookie", ".RaceDay.Session=abc123; path=/; samesite=lax; httponly");
            return response;
        });

        var result = await ClientFor(handler).LoginAsync("t@test.co.za", "Password123!");

        Assert.Equal(7, result.User.UserId);
        Assert.Equal("Organiser", result.User.Role);
        Assert.Equal(".RaceDay.Session=abc123", result.ApiSessionCookie);
        Assert.Equal("/api/auth/login", handler.Seen.Single().Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsApiExceptionWithTheApisMessage()
    {
        var handler = new StubHandler((_, _) =>
            Json(new { Title = "Unauthorized", Status = 401, Detail = "Invalid email or password." }, HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<ApiException>(() => ClientFor(handler).LoginAsync("t@test.co.za", "nope"));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("Invalid email or password.", ex.Message);
        Assert.True(ex.IsAuthFailure);
    }

    [Fact]
    public async Task ValidationErrors_AreFlattenedIntoOneReadableMessage()
    {
        var handler = new StubHandler((_, _) => Json(new
        {
            Title = "One or more validation errors occurred.",
            Status = 400,
            Errors = new Dictionary<string, string[]>
            {
                ["Name"] = ["The Name field is required."],
                ["DistanceKm"] = ["The field DistanceKm must be between 0.01 and 999.99."]
            }
        }, HttpStatusCode.BadRequest));

        var ex = await Assert.ThrowsAsync<ApiException>(() => ClientFor(handler).CreateEventAsync(new EventPayload()));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("The Name field is required.", ex.Message);
        Assert.Contains("between 0.01 and 999.99", ex.Message);
    }

    [Fact]
    public async Task ErrorWithAnEmptyBody_GetsAFriendlyDefaultMessage()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Conflict));

        var ex = await Assert.ThrowsAsync<ApiException>(() => ClientFor(handler).EnrolAsync(5));

        Assert.Equal(409, ex.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public async Task ConnectionFailure_BecomesFriendlyServiceUnavailableApiException()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => ClientFor(handler).GetEventsAsync(new EventQuery()));

        Assert.Equal(503, ex.StatusCode);
        Assert.Contains("temporarily unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task GetEvents_SendsOnlyTheFiltersThatWereChosen()
    {
        var handler = new StubHandler((_, _) => Json(new { Page = 1, PageSize = 9, TotalCount = 0, Items = Array.Empty<object>() }));

        await ClientFor(handler).GetEventsAsync(new EventQuery { Search = "comrades", EventType = "Run", Upcoming = true, PageSize = 9 });

        var query = handler.Seen.Single().Request.RequestUri!.Query;
        Assert.Contains("search=comrades", query);
        Assert.Contains("eventType=Run", query);
        Assert.Contains("upcoming=true", query);
        Assert.Contains("pageSize=9", query);
        Assert.DoesNotContain("location=", query);
        Assert.DoesNotContain("organiserId=", query);
    }

    [Fact]
    public async Task UploadImage_PostsMultipartFileToTheApiAndReturnsTheUrl()
    {
        const string url = "https://acct.blob.core.windows.net/raceday-images/banners/abc.png";
        var handler = new StubHandler((_, _) => Json(new { Url = url }));

        var result = await ClientFor(handler).UploadImageAsync(PngFile(), ImageKind.Banner);

        Assert.Equal(url, result);
        var (request, body) = handler.Seen.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/files/banner", request.RequestUri!.AbsolutePath);
        Assert.StartsWith("multipart/form-data", request.Content!.Headers.ContentType!.MediaType);
        Assert.True(body.Contains("name=file") || body.Contains("name=\"file\""), "multipart body should contain a field called 'file'");
        Assert.Contains("banner.png", body);
    }

    [Fact]
    public async Task UploadImage_ProfilePictureUsesItsOwnEndpoint()
    {
        var handler = new StubHandler((_, _) => Json(new { Url = "https://x.blob.core.windows.net/c/profiles/p.png" }));

        await ClientFor(handler).UploadImageAsync(PngFile("me.png"), ImageKind.ProfilePicture);

        Assert.Equal("/api/files/profile-picture", handler.Seen.Single().Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ApiSessionHandler_AddsTheUsersApiCookieToOutgoingRequests()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = TestHttp.ContextWithSession(s => s.SetString(SessionKeys.ApiCookie, ".RaceDay.Session=zzz"))
        };
        var inner = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new ApiSessionHandler(accessor) { InnerHandler = inner };

        using var invoker = new HttpMessageInvoker(handler);
        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://api.test/api/users/me"), CancellationToken.None);

        var sent = inner.Seen.Single().Request;
        Assert.True(sent.Headers.TryGetValues("Cookie", out var values));
        Assert.Equal(".RaceDay.Session=zzz", values!.Single());
    }

    [Fact]
    public async Task ApiSessionHandler_SendsNoCookieWhenNobodyIsSignedIn()
    {
        var accessor = new HttpContextAccessor { HttpContext = TestHttp.ContextWithSession() };
        var inner = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new ApiSessionHandler(accessor) { InnerHandler = inner };

        using var invoker = new HttpMessageInvoker(handler);
        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://api.test/api/events"), CancellationToken.None);

        Assert.False(inner.Seen.Single().Request.Headers.Contains("Cookie"));
    }
}
