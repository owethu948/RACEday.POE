using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Race-day weather and tips for an event's venue. Public; the data comes from the API.</summary>
public class WeatherController : AppController
{
    private readonly IRaceDayApiClient _api;
    private readonly ISessionUser _user;

    public WeatherController(IRaceDayApiClient api, ISessionUser user)
    {
        _api = api;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? eventId, CancellationToken ct)
    {
        var events = new List<EventDto>();
        string? problem = null;

        // Weather is a public, read-only page. If the API has not been started yet (or is
        // restarting), keep the request on this page so the user gets a retry-friendly UI.
        try
        {
            var page = await _api.GetEventsAsync(new EventQuery { Upcoming = true, PageSize = 100 }, ct);
            events = page.Items.OrderBy(e => e.EventDate).ToList();
        }
        catch (ApiException ex) when (ex.StatusCode is 502 or 503)
        {
            problem = ex.Message;
        }

        var selected = eventId;
        if (selected is null && _user.IsParticipant && events.Count > 0)
        {
            // Start on the next event this participant has entered.
            try
            {
                var mine = await _api.GetMyEnrolmentsAsync(ct);
                selected = mine.Where(m => m.Status == "Confirmed" && m.EventDate >= DateTime.Now)
                               .OrderBy(m => m.EventDate)
                               .Select(m => (int?)m.EventId)
                               .FirstOrDefault();
            }
            catch (ApiException ex) when (ex.StatusCode is 401 or 403 or 502 or 503)
            {
                // Public event data is still useful if the participant session/API is unavailable.
                problem ??= ex.Message;
            }
        }
        selected ??= events.FirstOrDefault()?.EventId;

        var model = new WeatherPageViewModel { Events = events, SelectedEventId = selected, Problem = problem };

        if (selected is not null)
        {
            try
            {
                model.Weather = await _api.GetWeatherAsync(selected.Value, ct);
            }
            catch (ApiException ex) when (ex.StatusCode is 404 or 502 or 503)
            {
                model.Problem = ex.Message;
            }
        }

        return View(model);
    }
}
