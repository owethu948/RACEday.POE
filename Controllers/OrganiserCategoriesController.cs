using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Add, edit and delete the age or distance categories of one event.</summary>
[RequireRole(Roles.Organiser)]
[Route("Organiser/Events/{eventId:int}/Categories")]
public class OrganiserCategoriesController : AppController
{
    private readonly IRaceDayApiClient _api;
    private readonly ISessionUser _user;

    public OrganiserCategoriesController(IRaceDayApiClient api, ISessionUser user)
    {
        _api = api;
        _user = user;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId, CancellationToken ct)
    {
        var ev = await _api.GetEventAsync(eventId, ct);
        if (ev.OrganiserId != _user.UserId) return NotYours();

        return View(new CategoriesViewModel { Event = ev });
    }

    [HttpPost("Add")]
    public async Task<IActionResult> Add(int eventId, CategoryFormModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid(eventId);

        try
        {
            await _api.CreateCategoryAsync(eventId, ToPayload(form), ct);
            SetSuccess($"Category \"{form.Name.Trim()}\" added.");
        }
        catch (ApiException ex) when (!ex.IsAuthFailure)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost("{categoryId:int}/Edit")]
    public async Task<IActionResult> Edit(int eventId, int categoryId, CategoryFormModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Invalid(eventId);

        try
        {
            await _api.UpdateCategoryAsync(categoryId, ToPayload(form), ct);
            SetSuccess("Category updated.");
        }
        catch (ApiException ex) when (!ex.IsAuthFailure)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost("{categoryId:int}/Delete")]
    public async Task<IActionResult> Delete(int eventId, int categoryId, CancellationToken ct)
    {
        try
        {
            await _api.DeleteCategoryAsync(categoryId, ct);
            SetSuccess("Category deleted.");
        }
        catch (ApiException ex) when (!ex.IsAuthFailure)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    private static CategoryPayload ToPayload(CategoryFormModel form) => new()
    {
        Name = form.Name.Trim(),
        DistanceKm = form.DistanceKm,
        MaxParticipants = form.MaxParticipants,
        EntryFee = form.EntryFee
    };

    private IActionResult Invalid(int eventId)
    {
        var first = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
        SetError(first ?? "Please check the category details and try again.");
        return RedirectToAction(nameof(Index), new { eventId });
    }

    private IActionResult NotYours()
    {
        SetError("You can only manage events that you created.");
        return RedirectToAction("Index", "OrganiserEvents");
    }
}
