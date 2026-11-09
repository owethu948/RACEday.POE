namespace RaceDay.Api.Dtos;

/// <summary>Race-day weather for an event's venue.</summary>
public class WeatherResponse
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }

    /// <summary>The venue as the organiser typed it.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>The place the weather provider matched, e.g. "Soweto".</summary>
    public string PlaceName { get; set; } = string.Empty;

    /// <summary>True when this is the forecast for the event day; false when it is today's conditions (event is more than 16 days away).</summary>
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

    /// <summary>Excellent, Good, Fair or Challenging.</summary>
    public string Conditions { get; set; } = string.Empty;

    public List<string> Tips { get; set; } = new();
}
