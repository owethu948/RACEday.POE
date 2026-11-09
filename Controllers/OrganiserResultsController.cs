using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Capture and publish finish times and positions once an event has taken place.</summary>
[RequireRole(Roles.Organiser)]
[Route("Organiser/Events/{eventId:int}/Results")]
public class OrganiserResultsController : AppController
{
    // h:mm:ss or hh:mm:ss  (e.g. 1:45:32). Minutes and seconds must be 00-59.
    private static readonly Regex TimePattern = new(@"^\d{1,3}:[0-5]\d:[0-5]\d$", RegexOptions.Compiled);

    private static readonly string[] Statuses = ["Finished", "DNF", "DSQ"];

    private readonly IRaceDayApiClient _api;
    private readonly ISessionUser _user;

    public OrganiserResultsController(IRaceDayApiClient api, ISessionUser user)
    {
        _api = api;
        _user = user;
    }

    /// <summary>"Capture Results" in the menu: jump to the most recent event that has already taken place.</summary>
    [HttpGet("~/Organiser/Results")]
    public async Task<IActionResult> Landing(CancellationToken ct)
    {
        var page = await _api.GetEventsAsync(new EventQuery { OrganiserId = _user.UserId, PageSize = 100 }, ct);
        var now = DateTime.Now;
        var target = page.Items.Where(e => e.EventDate < now).OrderByDescending(e => e.EventDate).FirstOrDefault()
                     ?? page.Items.OrderBy(e => e.EventDate).FirstOrDefault();

        if (target is null)
        {
            SetError("Create an event first - then you can capture its results here.");
            return RedirectToAction("Create", "OrganiserEvents");
        }

        return RedirectToAction(nameof(Index), new { eventId = target.EventId });
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId, CancellationToken ct)
    {
        var ev = await _api.GetEventAsync(eventId, ct);
        if (ev.OrganiserId != _user.UserId)
        {
            SetError("You can only capture results for events that you created.");
            return RedirectToAction("Index", "OrganiserEvents");
        }

        var enrolments = await _api.GetEventEnrolmentsAsync(eventId, ct);
        var results = await _api.GetEventResultsAsync(eventId, ct);
        var resultsByEnrolment = results.ToDictionary(r => r.EnrolmentId);

        var rows = enrolments
            .Where(e => e.Status == "Confirmed")
            .Select(e =>
            {
                resultsByEnrolment.TryGetValue(e.EnrolmentId, out var result);
                return new ResultRow
                {
                    EnrolmentId = e.EnrolmentId,
                    ParticipantName = e.ParticipantName,
                    CategoryName = e.CategoryName,
                    ResultId = result?.ResultId,
                    FinishTime = result?.FinishTime,
                    Position = result?.Position,
                    Status = result?.Status ?? "Finished"
                };
            })
            .OrderBy(r => r.Position ?? int.MaxValue)
            .ThenBy(r => r.ParticipantName)
            .ToList();

        var mine = await _api.GetEventsAsync(new EventQuery { OrganiserId = _user.UserId, PageSize = 100 }, ct);

        return View(new EventResultsViewModel
        {
            Event = ev,
            OrganiserEvents = mine.Items.OrderByDescending(e => e.EventDate).ToList(),
            HasTakenPlace = ev.EventDate < DateTime.Now,
            Rows = rows
        });
    }

    [HttpPost("{enrolmentId:int}/Save")]
    public async Task<IActionResult> Save(int eventId, int enrolmentId, ResultFormModel form, CancellationToken ct)
    {
        var problem = Validate(form, out var finishTime);
        if (problem is not null)
        {
            SetError(problem);
            return RedirectToAction(nameof(Index), new { eventId });
        }

        var isFinished = form.Status == "Finished";
        var payload = new ResultPayload
        {
            Status = form.Status,
            FinishTime = isFinished ? finishTime : null,
            Position = isFinished ? form.Position : null
        };

        try
        {
            if (form.ResultId is { } resultId)
                await _api.UpdateResultAsync(resultId, payload, ct);
            else
                await _api.CaptureResultAsync(enrolmentId, payload, ct);

            SetSuccess("Result saved and published to the participant.");
        }
        catch (ApiException ex) when (!ex.IsAuthFailure)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    /// <summary>Turns what was typed into a TimeSpan and gives a friendly message when it can't.</summary>
    internal static string? Validate(ResultFormModel form, out TimeSpan? finishTime)
    {
        finishTime = null;

        if (!Statuses.Contains(form.Status))
            return "Choose Finished, DNF or DSQ.";

        if (form.Status != "Finished") return null;

        var text = form.FinishTime?.Trim();
        if (string.IsNullOrEmpty(text) || !TimePattern.IsMatch(text))
            return "Enter the finish time as hours:minutes:seconds, for example 1:45:32.";

        if (form.Position is null or < 1)
            return "Enter the finishing position (1 or higher).";

        var parts = text.Split(':');
        finishTime = new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
        return null;
    }
}
