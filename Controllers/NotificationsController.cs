using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>The bell menu. Built from real API data when the bell is clicked (loaded by site.js), never on every page view.</summary>
[RequireLogin]
public class NotificationsController : AppController
{
    private readonly IRaceDayApiClient _api;
    private readonly ISessionUser _user;

    public NotificationsController(IRaceDayApiClient api, ISessionUser user)
    {
        _api = api;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var items = _user.IsOrganiser ? await ForOrganiserAsync(ct) : await ForParticipantAsync(ct);

        if (items.Count == 0)
        {
            items.Add(new NotificationItem
            {
                Icon = "\U0001F380",
                Text = "You're all caught up. We'll tell you when something happens.",
                When = "Now",
                Unread = false
            });
        }

        return PartialView("_Notifications", items);
    }

    private async Task<List<NotificationItem>> ForParticipantAsync(CancellationToken ct)
    {
        var items = new List<NotificationItem>();
        var now = DateTime.Now;

        var results = await _api.GetMyResultsAsync(ct);
        foreach (var r in results.OrderByDescending(r => r.EventDate).Take(2))
        {
            items.Add(new NotificationItem
            {
                Icon = "\U0001F3C5",
                Text = $"Your {r.EventName} result has been captured",
                When = Display.Date(r.EventDate),
                Url = Url.Action("MyResults", "Participant")
            });
        }

        var enrolments = await _api.GetMyEnrolmentsAsync(ct);
        foreach (var e in enrolments.Where(e => e.Status == "Confirmed" && e.EventDate >= now).OrderBy(e => e.EventDate).Take(2))
        {
            items.Add(new NotificationItem
            {
                Icon = "\U0001F4C5",
                Text = $"{e.EventName} is {Display.DaysUntil(e.EventDate)}",
                When = Display.Date(e.EventDate),
                Url = Url.Action("Details", "Events", new { id = e.EventId })
            });
        }

        var enrolledIds = enrolments.Where(e => e.Status == "Confirmed").Select(e => e.EventId).ToHashSet();
        var open = await _api.GetEventsAsync(new EventQuery { Upcoming = true, PageSize = 50 }, ct);
        foreach (var ev in open.Items.Where(e => !enrolledIds.Contains(e.EventId)).OrderByDescending(e => e.CreatedAt).Take(2))
        {
            items.Add(new NotificationItem
            {
                Icon = "\U0001F389",
                Text = $"{ev.Name} entries are open",
                When = Display.Date(ev.EventDate),
                Url = Url.Action("Details", "Events", new { id = ev.EventId }),
                Unread = false
            });
        }

        return items;
    }

    private async Task<List<NotificationItem>> ForOrganiserAsync(CancellationToken ct)
    {
        var items = new List<NotificationItem>();
        var now = DateTime.Now;
        var page = await _api.GetEventsAsync(new EventQuery { OrganiserId = _user.UserId, PageSize = 100 }, ct);

        foreach (var ev in page.Items.Where(e => e.EventDate < now && e.EnrolmentCount > e.ResultCount).OrderByDescending(e => e.EventDate).Take(3))
        {
            items.Add(new NotificationItem
            {
                Icon = "\u23F1\uFE0F",
                Text = $"{ev.Name} has finished: {Display.Plural(ev.EnrolmentCount - ev.ResultCount, "result")} still to capture",
                When = Display.Date(ev.EventDate),
                Url = Url.Action("Index", "OrganiserResults", new { eventId = ev.EventId })
            });
        }

        foreach (var ev in page.Items.Where(e => e.EventDate >= now).OrderBy(e => e.EventDate).Take(2))
        {
            items.Add(new NotificationItem
            {
                Icon = "\U0001F4E3",
                Text = $"{ev.Name} has {Display.Plural(ev.EnrolmentCount, "enrolment")} and is {Display.DaysUntil(ev.EventDate)}",
                When = Display.Date(ev.EventDate),
                Url = Url.Action("Index", "OrganiserEnrolments", new { eventId = ev.EventId }),
                Unread = false
            });
        }

        return items;
    }
}
