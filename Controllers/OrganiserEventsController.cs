using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Create, edit and delete events (including the banner image).</summary>
[RequireRole(Roles.Organiser)]
[Route("Organiser/Events")]
public class OrganiserEventsController : AppController
{
    private readonly IRaceDayApiClient _api;
    private readonly ISessionUser _user;

    public OrganiserEventsController(IRaceDayApiClient api, ISessionUser user)
    {
        _api = api;
        _user = user;
    }

    // ------------------------------------------------------------------ list
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var page = await _api.GetEventsAsync(new EventQuery { OrganiserId = _user.UserId, PageSize = 100 }, ct);
        return View(page.Items.OrderByDescending(e => e.EventDate).ToList());
    }

    // ------------------------------------------------------------------ create
    [HttpGet("Create")]
    public IActionResult Create() => View("Form", new EventFormViewModel
    {
        EventDate = DateTime.Today.AddMonths(2).AddHours(7),
        Categories = new List<CategoryRowInput> { new() }
    });

    [HttpPost("Create")]
    public Task<IActionResult> Create(EventFormViewModel model, CancellationToken ct) => SaveAsync(model, null, ct);

    // ------------------------------------------------------------------ edit
    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var ev = await _api.GetEventAsync(id, ct);
        if (ev.OrganiserId != _user.UserId) return NotYours();

        return View("Form", new EventFormViewModel
        {
            EventId = ev.EventId,
            Name = ev.Name,
            Description = ev.Description,
            EventDate = ev.EventDate,
            Location = ev.Location,
            EventType = ev.EventType,
            DistanceKm = ev.DistanceKm,
            ExistingBannerUrl = ev.BannerUrl
        });
    }

    [HttpPost("Edit/{id:int}")]
    public Task<IActionResult> Edit(int id, EventFormViewModel model, CancellationToken ct) => SaveAsync(model, id, ct);

    // ------------------------------------------------------------------ delete
    [HttpPost("Delete/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await _api.DeleteEventAsync(id, ct);
            SetSuccess("The event and everything attached to it was deleted.");
        }
        catch (ApiException ex) when (!ex.IsAuthFailure)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------ shared save
    private async Task<IActionResult> SaveAsync(EventFormViewModel model, int? id, CancellationToken ct)
    {
        model.EventId = id;

        string? bannerUrl = null;
        if (id is not null)
        {
            var existing = await _api.GetEventAsync(id.Value, ct);
            if (existing.OrganiserId != _user.UserId) return NotYours();
            bannerUrl = existing.BannerUrl;          // the API's copy, not a posted hidden field
            model.ExistingBannerUrl = bannerUrl;
        }

        var uploadProblem = ImageUploadValidator.Validate(model.Banner);
        if (uploadProblem is not null)
            ModelState.AddModelError(nameof(model.Banner), uploadProblem);

        var categoryRows = id is null ? model.Categories.Where(c => !c.IsBlank).ToList() : new List<CategoryRowInput>();
        for (var i = 0; i < categoryRows.Count; i++)
        {
            var problem = CategoryRowProblem(categoryRows[i]);
            if (problem is not null) ModelState.AddModelError(string.Empty, $"Category {i + 1}: {problem}");
        }

        if (!ModelState.IsValid)
        {
            if (model.Categories.Count == 0) model.Categories.Add(new CategoryRowInput());
            return View("Form", model);
        }

        try
        {
            if (model.RemoveBanner) bannerUrl = null;
            if (model.Banner is { Length: > 0 })
                bannerUrl = await _api.UploadImageAsync(model.Banner, ImageKind.Banner, ct);   // MVC -> API -> Azure

            var payload = new EventPayload
            {
                Name = model.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                EventDate = model.EventDate!.Value,
                Location = model.Location.Trim(),
                EventType = model.EventType,
                DistanceKm = model.DistanceKm,
                BannerUrl = bannerUrl
            };

            if (id is null)
            {
                var created = await _api.CreateEventAsync(payload, ct);

                // Categories typed on the same page are added right after the event exists.
                var failed = new List<string>();
                foreach (var row in categoryRows)
                {
                    try
                    {
                        await _api.CreateCategoryAsync(created.EventId, new CategoryPayload
                        {
                            Name = row.Name!.Trim(),
                            DistanceKm = row.DistanceKm!.Value,
                            MaxParticipants = row.MaxParticipants!.Value,
                            EntryFee = row.EntryFee ?? 0
                        }, ct);
                    }
                    catch (ApiException ex) when (!ex.IsAuthFailure)
                    {
                        failed.Add($"{row.Name}: {ex.Message}");
                    }
                }

                if (failed.Count > 0)
                {
                    SetError("Your event was created, but some categories couldn't be added (" + string.Join("; ", failed) + "). You can add them here.");
                    return RedirectToAction("Index", "OrganiserCategories", new { eventId = created.EventId });
                }

                if (categoryRows.Count == 0)
                {
                    SetSuccess("Event created. Add your categories so participants can enrol.");
                    return RedirectToAction("Index", "OrganiserCategories", new { eventId = created.EventId });
                }

                SetSuccess($"Event created with {Display.Plural(categoryRows.Count, "category", "categories")}.");
                return RedirectToAction(nameof(Index));
            }

            await _api.UpdateEventAsync(id.Value, payload, ct);
            SetSuccess("Event updated.");
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex) when (!ex.IsAuthFailure)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Form", model);
        }
    }

    /// <summary>Friendly check of one typed category row (the API repeats the real validation).</summary>
    internal static string? CategoryRowProblem(CategoryRowInput row)
    {
        if (string.IsNullOrWhiteSpace(row.Name)) return "give it a name.";
        if (row.Name.Trim().Length > 100) return "the name can be up to 100 characters.";
        if (row.DistanceKm is null || row.DistanceKm <= 0m) return "enter a distance greater than 0 km.";
        if (row.DistanceKm > 999.99m) return "the distance can be at most 999.99 km.";
        if (row.MaxParticipants is null || row.MaxParticipants < 1) return "enter the maximum number of participants (at least 1).";
        if (row.EntryFee < 0m) return "the entry fee can't be negative.";
        return null;
    }

    private IActionResult NotYours()
    {
        SetError("You can only manage events that you created.");
        return RedirectToAction(nameof(Index));
    }
}
