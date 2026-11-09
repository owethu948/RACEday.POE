using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Everything a Participant does after logging in: enrol, track enrolments, see results.</summary>
[RequireRole(Roles.Participant)]
public class ParticipantController : AppController
{
    private readonly IRaceDayApiClient _api;

    public ParticipantController(IRaceDayApiClient api) => _api = api;

    // ------------------------------------------------------------------ enrol
    [HttpPost]
    public async Task<IActionResult> Enrol(int eventId, int? categoryId, CancellationToken ct)
    {
        if (categoryId is null)
        {
            SetError("Choose a category before enrolling.");
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        try
        {
            var enrolment = await _api.EnrolAsync(categoryId.Value, ct);   // recorded immediately by the API
            SetSuccess($"You're in! You are enrolled for {enrolment.EventName} ({enrolment.CategoryName}).");
            return RedirectToAction(nameof(MyEvents));
        }
        catch (ApiException ex) when (!ex.IsAuthFailure)
        {
            SetError(ex.Message);
            return RedirectToAction("Details", "Events", new { id = eventId });
        }
    }

    // ------------------------------------------------------------------ my events
    [HttpGet]
    public async Task<IActionResult> MyEvents(string? tab, CancellationToken ct)
    {
        var all = await _api.GetMyEnrolmentsAsync(ct);
        var now = DateTime.Now;
        tab = (tab == "upcoming" || tab == "past") ? tab : "all";

        var items = tab switch
        {
            "upcoming" => all.Where(e => e.EventDate >= now).OrderBy(e => e.EventDate).ToList(),
            "past" => all.Where(e => e.EventDate < now).OrderByDescending(e => e.EventDate).ToList(),
            _ => all.OrderByDescending(e => e.EventDate >= now).ThenBy(e => e.EventDate >= now ? e.EventDate : DateTime.MaxValue).ThenByDescending(e => e.EventDate).ToList()
        };

        return View(new MyEnrolmentsViewModel
        {
            Items = items,
            Tab = tab,
            AllCount = all.Count,
            UpcomingCount = all.Count(e => e.EventDate >= now),
            PastCount = all.Count(e => e.EventDate < now)
        });
    }

    [HttpPost]
    public async Task<IActionResult> CancelEnrolment(int enrolmentId, CancellationToken ct)
    {
        try
        {
            await _api.CancelEnrolmentAsync(enrolmentId, ct);
            SetSuccess("Your enrolment has been cancelled.");
        }
        catch (ApiException ex) when (!ex.IsAuthFailure)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(MyEvents));
    }

    // ------------------------------------------------------------------ my results
    [HttpGet]
    public async Task<IActionResult> MyResults(CancellationToken ct)
    {
        var results = await _api.GetMyResultsAsync(ct);

        // "47th out of 312 finishers" needs the number of finishers in each event.
        // One lookup per distinct event, run one after another because they share the user's session.
        var finishersByEvent = new Dictionary<int, int>();
        foreach (var eventId in results.Select(r => r.EventId).Distinct())
        {
            var board = await _api.GetEventResultsAsync(eventId, ct);
            finishersByEvent[eventId] = board.Count(r => r.Status == "Finished");
        }

        // Personal best = the fastest finish in the category the participant has finished most often
        // (comparing a 10 km time with a marathon time would be meaningless).
        var personalBest = results
            .Where(r => r.Status == "Finished" && r.FinishTime is not null)
            .GroupBy(r => r.CategoryName)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Min(r => r.FinishTime))
            .FirstOrDefault()
            ?.OrderBy(r => r.FinishTime)
            .First();

        var model = new MyResultsViewModel
        {
            PersonalBest = personalBest,
            Rows = results
                .OrderByDescending(r => r.EventDate)
                .Select(r => new MyResultRow
                {
                    Result = r,
                    TotalFinishers = finishersByEvent.GetValueOrDefault(r.EventId)
                }).ToList()
        };

        return View(model);
    }
}
