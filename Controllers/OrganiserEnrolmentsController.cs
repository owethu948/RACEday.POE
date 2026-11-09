using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Who has enrolled for one event, with category and status.</summary>
[RequireRole(Roles.Organiser)]
[Route("Organiser/Events/{eventId:int}/Enrolments")]
public class OrganiserEnrolmentsController : AppController
{
    private readonly IRaceDayApiClient _api;
    private readonly ISessionUser _user;

    public OrganiserEnrolmentsController(IRaceDayApiClient api, ISessionUser user)
    {
        _api = api;
        _user = user;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId, string? status, CancellationToken ct)
    {
        var ev = await _api.GetEventAsync(eventId, ct);
        if (ev.OrganiserId != _user.UserId)
        {
            SetError("You can only view enrolments for events that you created.");
            return RedirectToAction("Index", "OrganiserEvents");
        }

        var enrolments = await _api.GetEventEnrolmentsAsync(eventId, ct);
        if (!string.IsNullOrWhiteSpace(status))
            enrolments = enrolments.Where(e => string.Equals(e.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();

        return View(new EventEnrolmentsViewModel { Event = ev, Enrolments = enrolments, StatusFilter = status });
    }

    [HttpPost("{enrolmentId:int}/Cancel")]
    public async Task<IActionResult> Cancel(int eventId, int enrolmentId, CancellationToken ct)
    {
        try
        {
            await _api.CancelEnrolmentAsync(enrolmentId, ct);
            SetSuccess("The enrolment was cancelled.");
        }
        catch (ApiException ex) when (!ex.IsAuthFailure)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }
}
