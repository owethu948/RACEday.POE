using System.Globalization;
using System.Text.Json;

namespace RaceDay.Api.Services;

/// <summary>What the weather service learned about a venue on a given day.</summary>
public sealed record WeatherSnapshot(
    string PlaceName,
    bool IsEventDayForecast,
    int WeatherCode,
    double TemperatureC,
    double? LowC,
    double? HighC,
    double WindKmh,
    int? HumidityPercent,
    int? RainChancePercent,
    double? UvIndex);

/// <summary>Thrown when the weather provider cannot be reached or does not know the place.</summary>
public class WeatherUnavailableException : Exception
{
    public WeatherUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

public interface IWeatherService
{
    /// <summary>Weather for a venue. Uses the forecast for <paramref name="eventDate"/> when it is within the provider's range, otherwise the current conditions.</summary>
    /// <exception cref="WeatherUnavailableException">The venue could not be found or the provider is down.</exception>
    Task<WeatherSnapshot> GetAsync(string location, DateTime eventDate, CancellationToken cancellationToken = default);
}

/// <summary>Free, key-less weather from Open-Meteo (geocoding + 16-day forecast).</summary>
public class OpenMeteoWeatherService : IWeatherService
{
    private const string GeocodeUrl = "https://geocoding-api.open-meteo.com/v1/search";
    private const string ForecastUrl = "https://api.open-meteo.com/v1/forecast";

    private readonly HttpClient _http;

    public OpenMeteoWeatherService(HttpClient http) => _http = http;

    public async Task<WeatherSnapshot> GetAsync(string location, DateTime eventDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var (name, latitude, longitude) = await FindPlaceAsync(location, cancellationToken);

            var url = $"{ForecastUrl}?latitude={latitude.ToString(CultureInfo.InvariantCulture)}&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
                      "&current=temperature_2m,relative_humidity_2m,wind_speed_10m,weather_code" +
                      "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,wind_speed_10m_max,uv_index_max" +
                      "&timezone=auto&forecast_days=16";

            using var response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

            return Parse(name, doc.RootElement, eventDate);
        }
        catch (WeatherUnavailableException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new WeatherUnavailableException("The weather service is not available right now.", ex);
        }
    }

    /// <summary>Turns the provider's JSON into a snapshot. Public so it can be tested without a network.</summary>
    public static WeatherSnapshot Parse(string placeName, JsonElement root, DateTime eventDate)
    {
        var day = eventDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (root.TryGetProperty("daily", out var daily))
        {
            var times = daily.GetProperty("time").EnumerateArray().Select(t => t.GetString()).ToList();
            var index = times.IndexOf(day);
            if (index >= 0)
            {
                var high = Number(daily, "temperature_2m_max", index);
                var low = Number(daily, "temperature_2m_min", index);
                return new WeatherSnapshot(
                    placeName,
                    IsEventDayForecast: true,
                    WeatherCode: (int)(Number(daily, "weather_code", index) ?? 0),
                    TemperatureC: high ?? low ?? 0,
                    LowC: low,
                    HighC: high,
                    WindKmh: Number(daily, "wind_speed_10m_max", index) ?? 0,
                    HumidityPercent: null,
                    RainChancePercent: Number(daily, "precipitation_probability_max", index) is { } rain ? (int)rain : null,
                    UvIndex: Number(daily, "uv_index_max", index));
            }
        }

        var current = root.GetProperty("current");
        return new WeatherSnapshot(
            placeName,
            IsEventDayForecast: false,
            WeatherCode: current.TryGetProperty("weather_code", out var code) && code.ValueKind == JsonValueKind.Number ? code.GetInt32() : 0,
            TemperatureC: current.GetProperty("temperature_2m").GetDouble(),
            LowC: null,
            HighC: null,
            WindKmh: current.TryGetProperty("wind_speed_10m", out var wind) && wind.ValueKind == JsonValueKind.Number ? wind.GetDouble() : 0,
            HumidityPercent: current.TryGetProperty("relative_humidity_2m", out var humidity) && humidity.ValueKind == JsonValueKind.Number ? (int)humidity.GetDouble() : null,
            RainChancePercent: null,
            UvIndex: null);
    }

    private static double? Number(JsonElement daily, string name, int index)
    {
        if (!daily.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array) return null;
        var items = array.EnumerateArray().ToList();
        if (index >= items.Count || items[index].ValueKind != JsonValueKind.Number) return null;
        return items[index].GetDouble();
    }

    private async Task<(string Name, double Latitude, double Longitude)> FindPlaceAsync(string location, CancellationToken cancellationToken)
    {
        // "Soweto, Johannesburg" -> try "Soweto", then the whole text, then "Johannesburg".
        var parts = location.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidates = parts.Length > 1
            ? new[] { parts[0], location.Trim(), parts[^1] }
            : new[] { location.Trim() };

        foreach (var candidate in candidates.Distinct())
        {
            var url = $"{GeocodeUrl}?name={Uri.EscapeDataString(candidate)}&count=1&language=en&format=json";
            using var response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

            if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                var first = results[0];
                var name = first.TryGetProperty("name", out var n) ? n.GetString() ?? candidate : candidate;
                return (name, first.GetProperty("latitude").GetDouble(), first.GetProperty("longitude").GetDouble());
            }
        }

        throw new WeatherUnavailableException($"We couldn't find \"{location}\" on the weather map.");
    }
}

/// <summary>Plain-language description, icon and race-day advice for a snapshot.</summary>
public static class WeatherAdvisor
{
    public static (string Summary, string Icon) Describe(int code) => code switch
    {
        0 => ("Clear sky", "\u2600\uFE0F"),
        1 => ("Mainly clear", "\U0001F324\uFE0F"),
        2 => ("Partly cloudy", "\u26C5"),
        3 => ("Overcast", "\u2601\uFE0F"),
        45 or 48 => ("Fog", "\U0001F32B\uFE0F"),
        >= 51 and <= 57 => ("Drizzle", "\U0001F326\uFE0F"),
        >= 61 and <= 67 => ("Rain", "\U0001F327\uFE0F"),
        >= 71 and <= 77 => ("Snow", "\u2744\uFE0F"),
        >= 80 and <= 82 => ("Showers", "\U0001F327\uFE0F"),
        85 or 86 => ("Snow showers", "\u2744\uFE0F"),
        >= 95 => ("Thunderstorms", "\u26C8\uFE0F"),
        _ => ("Mixed conditions", "\U0001F324\uFE0F")
    };

    /// <summary>"Excellent", "Good", "Fair" or "Challenging" running (or riding) conditions.</summary>
    public static string Rate(WeatherSnapshot w)
    {
        var rain = w.RainChancePercent ?? 0;
        if (w.WeatherCode >= 95 || w.TemperatureC >= 32 || w.TemperatureC <= 0 || w.WindKmh >= 45) return "Challenging";
        if (rain >= 60 || w.TemperatureC >= 28 || w.TemperatureC < 5 || w.WindKmh >= 32) return "Fair";
        if (w.TemperatureC is >= 9 and <= 21 && rain < 30 && w.WindKmh < 25) return "Excellent";
        return "Good";
    }

    public static List<string> Tips(WeatherSnapshot w, string eventType)
    {
        var tips = new List<string>();
        var rain = w.RainChancePercent ?? 0;
        var high = w.HighC ?? w.TemperatureC;

        if (high >= 30) tips.Add("It will be hot. Start hydrating the day before, drink at every water point and slow your pace.");
        else if (high >= 24) tips.Add("Warm conditions: wear light, moisture-wicking fabric and carry or collect water along the route.");
        else if (w.TemperatureC < 5) tips.Add("Very cold start. Wear layers you can shed, and keep warm at the start line until the gun goes.");
        else if (w.TemperatureC < 12) tips.Add("Cool conditions: a light top layer at the start works well, you'll warm up quickly.");
        else tips.Add("Ideal temperatures. Start hydrating the night before and wear light moisture-wicking fabric.");

        if (rain >= 50) tips.Add("Rain is likely. Pack a light waterproof layer and anti-chafe balm, and take extra care on wet roads.");
        else if (rain >= 30) tips.Add("A shower is possible, so keep a light rain layer in your bag.");

        if (w.WindKmh >= 30) tips.Add("Strong wind. Expect a tougher pace into the headwind and use other runners or riders for shelter.");

        if ((w.UvIndex ?? 0) >= 6) tips.Add("The UV index is high. Wear sunscreen, a cap and sunglasses.");
        else if (w.UvIndex is null && w.TemperatureC >= 20) tips.Add("Sunscreen is recommended for daytime events.");

        if (eventType == "Cycle" && w.WindKmh >= 25) tips.Add("Cyclists: check your tyre pressure and watch for crosswinds on exposed sections.");

        return tips;
    }
}
