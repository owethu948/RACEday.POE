using System.Net;

namespace RaceDay.Web.Tests;

/// <summary>
/// The real web app running in memory against a fake API: login, role-based navigation, protected pages,
/// logout, and friendly behaviour when the API is down.
/// </summary>
public class WebAppTests : IClassFixture<WebFactory>
{
    private readonly WebFactory _factory;

    public WebAppTests(WebFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_CanBrowseUpcomingEvents()
    {
        var response = await _factory.CreateBrowser().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Stub Marathon", html);
        Assert.Contains("Log in", html);
        Assert.DoesNotContain("My Results", html);
    }

    [Theory]
    [InlineData("/Organiser/Dashboard")]
    [InlineData("/Organiser/Events")]
    [InlineData("/Participant/MyEvents")]
    [InlineData("/Participant/MyResults")]
    [InlineData("/Account/Profile")]
    public async Task Anonymous_IsRedirectedToLoginFromProtectedPages(string url)
    {
        var response = await _factory.CreateBrowser().GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Location());
    }

    [Fact]
    public async Task Organiser_LogsIn_SeesOrganiserMenuAndDashboard()
    {
        var browser = _factory.CreateBrowser();

        var login = await browser.LoginAsync("organiser@test.co.za");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Organiser/Dashboard", login.Location());

        var dashboard = await browser.GetStringAsync("/Organiser/Dashboard");
        Assert.Contains("Hello, Olivia Organiser", dashboard);
        Assert.Contains("Stub Marathon", dashboard);
        Assert.Contains("Manage Events", dashboard);
        Assert.DoesNotContain("My Results", dashboard);   // an Organiser must never see participant pages
    }

    [Fact]
    public async Task Organiser_CannotOpenParticipantPages()
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync("organiser@test.co.za");

        var response = await browser.GetAsync("/Participant/MyResults");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Participant_LogsIn_SeesParticipantMenu_AndCannotOpenOrganiserPages()
    {
        var browser = _factory.CreateBrowser();

        var login = await browser.LoginAsync("runner@test.co.za");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Location());

        var home = await browser.GetStringAsync("/");
        Assert.Contains("My Results", home);
        Assert.Contains("My Enrolments", home);
        Assert.DoesNotContain("Manage Events", home);

        var organiserPage = await browser.GetAsync("/Organiser/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, organiserPage.StatusCode);
        Assert.DoesNotContain("/Organiser", organiserPage.Location());
    }

    [Fact]
    public async Task Participant_PagesReachTheApiWithTheirOwnSession()
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync("runner@test.co.za");

        // The fake API answers 401 unless the participant's API cookie was forwarded.
        var myEvents = await browser.GetAsync("/Participant/MyEvents");

        Assert.Equal(HttpStatusCode.OK, myEvents.StatusCode);
        Assert.Contains("haven't enrolled", await myEvents.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsAFriendlyMessage()
    {
        var response = await _factory.CreateBrowser().LoginAsync("runner@test.co.za", "wrong-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // the form is shown again
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password.", html);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync("organiser@test.co.za");
        var token = await browser.GetTokenAsync("/Organiser/Dashboard");

        var logout = await browser.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        var after = await browser.GetAsync("/Organiser/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Contains("/Account/Login", after.Location());
    }

    [Fact]
    public async Task UnknownPage_ShowsTheFriendly404()
    {
        var response = await _factory.CreateBrowser().GetAsync("/this/page/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("couldn&#x27;t find", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WhenTheApiIsDown_TheUserSeesAFriendlyPageNotAnException()
    {
        await using var factory = new WebFactory(() => new StubHandler((_, _) => throw new HttpRequestException("connection refused")));

        var response = await factory.CreateBrowser().GetAsync("/");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("warming up", html);
        Assert.DoesNotContain("HttpRequestException", html);
        Assert.DoesNotContain("connection refused", html);
    }

    // ------------------------------------------------------------------ weather, notifications, enrol pop-up
    [Fact]
    public async Task WeatherPage_ShowsTheForecastAndTipsFromTheApi()
    {
        var response = await _factory.CreateBrowser().GetAsync("/Weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Stub Marathon", html);
        Assert.Contains("Partly cloudy", html);
        Assert.Contains("Excellent conditions", html);
        Assert.Contains("Race day tips", html);
    }

    [Fact]
    public async Task WeatherPage_WhenTheWeatherServiceIsDown_ShowsAFriendlyMessage()
    {
        await using var factory = new WebFactory(() => new FakeApiHandler(weatherDown: true));

        var response = await factory.CreateBrowser().GetAsync("/Weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Weather isn't available right now", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WeatherPage_WhenTheApiIsDown_ShowsRetryUiInsteadOfBreaking()
    {
        await using var factory = new WebFactory(() => new StubHandler((_, _) =>
            throw new HttpRequestException("connection refused")));

        var response = await factory.CreateBrowser().GetAsync("/Weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Weather is temporarily unavailable", html);
        Assert.Contains("Try again", html);
        Assert.DoesNotContain("connection refused", html);
    }

    [Fact]
    public async Task Notifications_AreBuiltFromApiData()
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync("runner@test.co.za");

        var html = await browser.GetStringAsync("/Notifications");

        Assert.Contains("Stub Marathon entries are open", html);
    }

    [Fact]
    public async Task Notifications_RequireLogin()
    {
        var response = await _factory.CreateBrowser().GetAsync("/Notifications");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task EnrolPopUp_ListsTheCategoriesForAParticipant()
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync("runner@test.co.za");

        var html = await browser.GetStringAsync("/Events/EnrolModal/1");

        Assert.Contains("21km", html);
        Assert.Contains("Confirm entry", html);
    }

    [Fact]
    public async Task Enrol_RecordsTheEntryThroughTheApi_AndShowsMyEnrolments()
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync("runner@test.co.za");
        var token = await browser.GetTokenAsync("/");

        var response = await browser.PostAsync("/Participant/Enrol", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["eventId"] = "1",
            ["categoryId"] = "5",
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Participant/MyEvents", response.Location());
    }

    [Fact]
    public async Task Enrol_WithoutPickingACategory_AsksForOne()
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync("runner@test.co.za");
        var token = await browser.GetTokenAsync("/");

        var response = await browser.PostAsync("/Participant/Enrol", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["eventId"] = "1",
            ["__RequestVerificationToken"] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Events/Details/1", response.Location());
    }

    [Fact]
    public async Task Organiser_CreateEventPage_HasCategoryRows()
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync("organiser@test.co.za");

        var html = await browser.GetStringAsync("/Organiser/Events/Create");

        Assert.Contains("Categories[0].Name", html);
        Assert.Contains("Create event", html);
    }

    [Fact]
    public async Task Organiser_CaptureResultsMenu_JumpsToAnEvent()
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync("organiser@test.co.za");

        var response = await browser.GetAsync("/Organiser/Results");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Organiser/Events/1/Results", response.Location());
    }
}
