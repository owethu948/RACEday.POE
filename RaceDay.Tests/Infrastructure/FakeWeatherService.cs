using RaceDay.Api.Services;

namespace RaceDay.Tests.Infrastructure;

/// <summary>Pleasant, predictable weather for every venue except "Atlantis", which the provider "cannot find".</summary>
public sealed class FakeWeatherService : IWeatherService
{
    public Task<WeatherSnapshot> GetAsync(string location, DateTime eventDate, CancellationToken cancellationToken = default)
    {
        if (location.Contains("Atlantis", StringComparison.OrdinalIgnoreCase))
            throw new WeatherUnavailableException("We couldn't find that place on the weather map.");

        return Task.FromResult(new WeatherSnapshot(
            PlaceName: "Testville",
            IsEventDayForecast: true,
            WeatherCode: 2,
            TemperatureC: 18,
            LowC: 9,
            HighC: 18,
            WindKmh: 10,
            HumidityPercent: null,
            RainChancePercent: 10,
            UvIndex: 5));
    }
}
