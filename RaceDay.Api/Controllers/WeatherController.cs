using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Race-day weather and tips for an event's venue.</summary>
[Route("api/events/{id:int}/weather")]
public class WeatherController(RaceDayDbContext db, IWeatherService weather) : ApiControllerBase
{
    /// <summary>Get the weather for an event.</summary>
    /// <remarks>Public. Uses the forecast for the event day when it is within 16 days, otherwise today's conditions at the venue.</remarks>
    /// <param name="id">The event id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Weather, a running-conditions rating and tips.</response>
    /// <response code="404">No event with that id.</response>
    /// <response code="503">The weather provider is unreachable or does not know the venue.</response>
    [HttpGet]
    [ProducesResponseType(typeof(WeatherResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetWeather(int id, CancellationToken cancellationToken)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id, cancellationToken);
        if (ev is null) return Fail(404, $"Event {id} was not found.");

        try
        {
            var snapshot = await weather.GetAsync(ev.Location, ev.EventDate, cancellationToken);
            var (summary, icon) = WeatherAdvisor.Describe(snapshot.WeatherCode);

            return Ok(new WeatherResponse
            {
                EventId = ev.EventId,
                EventName = ev.Name,
                EventDate = ev.EventDate,
                Location = ev.Location,
                PlaceName = snapshot.PlaceName,
                IsEventDayForecast = snapshot.IsEventDayForecast,
                Summary = summary,
                Icon = icon,
                TemperatureC = Math.Round(snapshot.TemperatureC, 1),
                LowC = snapshot.LowC is { } low ? Math.Round(low, 1) : null,
                HighC = snapshot.HighC is { } high ? Math.Round(high, 1) : null,
                WindKmh = Math.Round(snapshot.WindKmh, 0),
                HumidityPercent = snapshot.HumidityPercent,
                RainChancePercent = snapshot.RainChancePercent,
                UvIndex = snapshot.UvIndex,
                Conditions = WeatherAdvisor.Rate(snapshot),
                Tips = WeatherAdvisor.Tips(snapshot, ev.EventType)
            });
        }
        catch (WeatherUnavailableException ex)
        {
            return Fail(503, ex.Message);
        }
    }
}
