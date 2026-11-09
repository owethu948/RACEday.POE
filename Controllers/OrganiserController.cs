using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>The Organiser's home: a summary of their events, enrolments and what is coming up.</summary>
[RequireRole(Roles.Organiser)]
public class OrganiserController : AppController
{
    private readonly IRaceDayApiClient _api;
    private readonly ISessionUser _user;

    public OrganiserController(IRaceDayApiClient api, ISessionUser user)
    {
        _api = api;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var page = await _api.GetEventsAsync(new EventQuery { OrganiserId = _user.UserId, PageSize = 100 }, ct);

        var now = DateTime.Now;
        var events = page.Items
            .OrderByDescending(e => e.EventDate >= now)                       // upcoming first ...
            .ThenBy(e => e.EventDate >= now ? e.EventDate : DateTime.MaxValue) // ... soonest first
            .ThenByDescending(e => e.EventDate)                                // then most recent past events
            .ToList();

        var model = new OrganiserDashboardViewModel
        {
            Events = events,
            TotalEvents = page.TotalCount,
            TotalEnrolments = events.Sum(e => e.EnrolmentCount),
            ResultsCaptured = events.Sum(e => e.ResultCount),
            TotalEntryFees = events.Sum(e => e.TotalEntryFees),
            UpcomingCount = events.Count(e => e.EventDate >= now),
            NextEvent = events.Where(e => e.EventDate >= now).MinBy(e => e.EventDate),
            MaxEnrolments = events.Count == 0 ? 0 : events.Max(e => e.EnrolmentCount)
        };

        return View(model);
    }
}
