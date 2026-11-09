using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : AppController
{
    private const int PageSize = 9;

    private readonly IRaceDayApiClient _api;
    private readonly ISessionUser _user;

    public HomeController(IRaceDayApiClient api, ISessionUser user)
    {
        _api = api;
        _user = user;
    }

    /// <summary>Public home page: upcoming events with search and filters. Organisers go to their dashboard.</summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] EventFilter filter, CancellationToken ct)
    {
        if (_user.IsOrganiser) return RedirectToAction("Dashboard", "Organiser");

        try
        {
            filter.Page = Math.Max(1, filter.Page);
            var events = await _api.GetEventsAsync(new EventQuery
            {
                Search = filter.Search,
                Location = filter.Location,
                EventType = filter.EventType,
                Upcoming = true,
                Page = filter.Page,
                PageSize = PageSize
            }, ct);

            var model = new HomeIndexViewModel { Events = events, Filter = filter, IsParticipant = _user.IsParticipant };

            if (_user.IsParticipant)
            {
                var mine = (await _api.GetMyEnrolmentsAsync(ct)).Where(m => m.Status == "Confirmed").ToList();
                model.EnrolledEventIds = mine.Select(m => m.EventId).ToHashSet();
                model.FeesPaid = mine.Sum(m => m.EntryFee);
                model.RacesFinished = (await _api.GetMyResultsAsync(ct)).Count(r => r.Status == "Finished");
            }

            return View(model);
        }
        catch (ApiException ex)
        {
            // Catch the 503 (or other API errors) and show the friendly error page
            var errorModel = new ErrorViewModel
            {
                StatusCode = ex.StatusCode,
                Heading = ex.StatusCode == 503 ? "We're warming up" : "Something went wrong",
                Message = ex.Message
            };

            Response.StatusCode = ex.StatusCode;
            return View("Error", errorModel);
        }
    }

    [HttpGet]
    public IActionResult AccessDenied() => View("Error", new ErrorViewModel
    {
        StatusCode = 403,
        Heading = "No access",
        Message = "You don't have permission to see that page."
    });

    /// <summary>Handles 404s, 503s and other bare status codes (app.UseStatusCodePagesWithReExecute).</summary>
    [HttpGet]
    public IActionResult HttpStatus(int code)
    {
        var model = code switch
        {
            404 => new ErrorViewModel { StatusCode = 404, Heading = "We couldn't find that page", Message = "It may have been removed, or the link might be wrong." },
            503 => new ErrorViewModel { StatusCode = 503, Heading = "We're warming up", Message = "We can't reach the RaceDay service right now. Please try again in a minute." },
            _ => new ErrorViewModel { StatusCode = code, Heading = "Something went wrong", Message = "Please try again in a moment." }
        };
        Response.StatusCode = code;
        return View("Error", model);
    }

    /// <summary>Unexpected errors: a friendly page, never a stack trace.</summary>
    [HttpGet, HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult Error()
    {
        Response.StatusCode = 500;
        return View(new ErrorViewModel());
    }
}