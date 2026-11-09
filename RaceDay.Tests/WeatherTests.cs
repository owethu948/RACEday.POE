using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

public class WeatherTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public WeatherTests(RaceDayApiFactory factory) => _factory = factory;

    // ------------------------------------------------------------ endpoint
    [Fact]
    public async Task GetWeather_ReturnsSummaryRatingAndTips_WithoutLogin()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var ev = await TestData.CreateEventAsync(organiser.Client);

        var weather = await _factory.CreateClient().GetFromJsonAsync<WeatherResponse>($"/api/events/{ev.EventId}/weather");

        Assert.Equal(ev.EventId, weather!.EventId);
        Assert.Equal("Partly cloudy", weather.Summary);
        Assert.Equal("Excellent", weather.Conditions);
        Assert.True(weather.IsEventDayForecast);
        Assert.NotEmpty(weather.Tips);
    }

    [Fact]
    public async Task GetWeather_UnknownEvent_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/999999/weather");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWeather_WhenProviderCannotFindTheVenue_Returns503()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var request = TestData.NewEvent();
        request.Location = "Atlantis";
        var ev = await TestData.CreateEventAsync(organiser.Client, request);

        var response = await _factory.CreateClient().GetAsync($"/api/events/{ev.EventId}/weather");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // ------------------------------------------------------------ advisor
    [Theory]
    [InlineData(18, 10, 10, "Excellent")]
    [InlineData(25, 10, 10, "Good")]
    [InlineData(30, 10, 10, "Fair")]
    [InlineData(34, 10, 10, "Challenging")]
    [InlineData(18, 10, 70, "Fair")]
    [InlineData(18, 50, 10, "Challenging")]
    public void Rate_ReflectsTemperatureRainAndWind(double temp, double wind, int rain, string expected)
    {
        var snapshot = new WeatherSnapshot("X", true, 2, temp, null, null, wind, null, rain, null);

        Assert.Equal(expected, WeatherAdvisor.Rate(snapshot));
    }

    [Fact]
    public void Tips_MentionHeatRainAndUvWhenTheyApply()
    {
        var hot = new WeatherSnapshot("X", true, 61, 31, 20, 31, 10, null, 80, 9);

        var tips = string.Join(" ", WeatherAdvisor.Tips(hot, "Run"));

        Assert.Contains("hot", tips, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rain", tips);
        Assert.Contains("UV", tips);
    }

    [Theory]
    [InlineData(0, "Clear sky")]
    [InlineData(2, "Partly cloudy")]
    [InlineData(63, "Rain")]
    [InlineData(95, "Thunderstorms")]
    public void Describe_MapsWeatherCodes(int code, string expected) =>
        Assert.Equal(expected, WeatherAdvisor.Describe(code).Summary);

    // ------------------------------------------------------------ provider parsing
    private const string ForecastJson = """
        {
          "current": { "temperature_2m": 21.4, "relative_humidity_2m": 55, "wind_speed_10m": 12.0, "weather_code": 1 },
          "daily": {
            "time": ["2026-11-08", "2026-11-09", "2026-11-10"],
            "weather_code": [0, 3, 61],
            "temperature_2m_max": [24.1, 19.8, 17.0],
            "temperature_2m_min": [11.0, 9.5, 8.0],
            "precipitation_probability_max": [5, 20, 75],
            "wind_speed_10m_max": [14.0, 22.5, 31.0],
            "uv_index_max": [8.1, 6.2, 3.0]
          }
        }
        """;

    [Fact]
    public void Parse_UsesTheEventDaysForecastWhenItIsInRange()
    {
        using var doc = JsonDocument.Parse(ForecastJson);

        var snapshot = OpenMeteoWeatherService.Parse("Soweto", doc.RootElement, new DateTime(2026, 11, 10, 7, 0, 0));

        Assert.True(snapshot.IsEventDayForecast);
        Assert.Equal(17.0, snapshot.TemperatureC);
        Assert.Equal(8.0, snapshot.LowC);
        Assert.Equal(75, snapshot.RainChancePercent);
        Assert.Equal(61, snapshot.WeatherCode);
        Assert.Equal(31.0, snapshot.WindKmh);
    }

    [Fact]
    public void Parse_FallsBackToCurrentConditionsWhenTheEventIsFurtherAway()
    {
        using var doc = JsonDocument.Parse(ForecastJson);

        var snapshot = OpenMeteoWeatherService.Parse("Soweto", doc.RootElement, new DateTime(2027, 6, 13));

        Assert.False(snapshot.IsEventDayForecast);
        Assert.Equal(21.4, snapshot.TemperatureC);
        Assert.Equal(55, snapshot.HumidityPercent);
    }

    [Fact]
    public async Task Service_TriesTheNextPlaceNameWhenTheFirstIsUnknown_AndThenReadsTheForecast()
    {
        var requests = new List<string>();
        var handler = new StubHttpHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            requests.Add(url);

            if (url.Contains("geocoding-api"))
            {
                // "Soweto" is unknown, "Soweto, Johannesburg" is not tried as a single name by this stub, "Johannesburg" is.
                var found = url.Contains("name=Johannesburg");
                var body = found ? """{"results":[{"name":"Johannesburg","latitude":-26.2,"longitude":28.04}]}""" : "{}";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ForecastJson) };
        });
        var service = new OpenMeteoWeatherService(new HttpClient(handler));

        var snapshot = await service.GetAsync("Soweto, Johannesburg", new DateTime(2026, 11, 9));

        Assert.Equal("Johannesburg", snapshot.PlaceName);
        Assert.True(snapshot.IsEventDayForecast);
        Assert.Contains(requests, r => r.Contains("api.open-meteo.com/v1/forecast") && r.Contains("latitude=-26.2"));
    }

    [Fact]
    public async Task Service_WhenNothingMatches_ThrowsWeatherUnavailable()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        var service = new OpenMeteoWeatherService(new HttpClient(handler));

        await Assert.ThrowsAsync<WeatherUnavailableException>(() => service.GetAsync("Nowhere", DateTime.Now));
    }

    [Fact]
    public async Task Service_WhenTheProviderIsDown_ThrowsWeatherUnavailable()
    {
        var handler = new StubHttpHandler(_ => throw new HttpRequestException("offline"));
        var service = new OpenMeteoWeatherService(new HttpClient(handler));

        await Assert.ThrowsAsync<WeatherUnavailableException>(() => service.GetAsync("Soweto", DateTime.Now));
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }
}
