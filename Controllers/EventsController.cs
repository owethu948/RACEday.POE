using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Public event details. Anyone can look; only signed-in Participants see the Enrol buttons.</summary>
public class EventsController : AppController
{
    private readonly IRaceDayApiClient _api;
    private readonly ISessionUser _user;

    public EventsController(IRaceDayApiClient api, ISessionUser user)
    {
        _api = api;
        _user = user;
    }

    /// <summary>The category picker shown in the "Enter event" pop-up (loaded on demand by site.js).</summary>
    [HttpGet]
    public async Task<IActionResult> EnrolModal(int id, CancellationToken ct)
    {
        if (!_user.IsParticipant) return StatusCode(StatusCodes.Status403Forbidden);

        var ev = await _api.GetEventAsync(id, ct);
        var mine = await _api.GetMyEnrolmentsAsync(ct);

        return PartialView("_EnrolModalBody", new EnrolModalViewModel
        {
            Event = ev,
            EnrolledCategoryIds = mine.Where(m => m.EventId == id && m.Status == "Confirmed").Select(m => m.CategoryId).ToHashSet()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var ev = await _api.GetEventAsync(id, ct);   // a missing event becomes the friendly 404 page
        var hasTakenPlace = ev.EventDate < DateTime.Now;

        var model = new EventDetailsViewModel { Event = ev, HasTakenPlace = hasTakenPlace };

        if (_user.IsParticipant)
        {
            var mine = await _api.GetMyEnrolmentsAsync(ct);
            model.EnrolledCategoryIds = mine
                .Where(m => m.EventId == id && m.Status == "Confirmed")
                .Select(m => m.CategoryId)
                .ToHashSet();
        }

        if (hasTakenPlace)
        {
            var results = await _api.GetEventResultsAsync(id, ct);
            model.TotalFinishers = results.Count(r => r.Status == "Finished");
            model.Leaderboard = results.Where(r => r.Status == "Finished").Take(10).ToList();
        }

        return View(model);
    }
}
