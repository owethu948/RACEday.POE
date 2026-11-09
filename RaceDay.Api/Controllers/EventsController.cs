using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Browse events (public) and manage them (Organiser who owns the event).</summary>
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>List events, with optional search, filtering and paging.</summary>
    /// <remarks>Public - no login needed. Upcoming events are returned soonest-first; otherwise newest-first.</remarks>
    /// <param name="search">Only events whose name or description contains this text.</param>
    /// <param name="location">Only events whose location contains this text.</param>
    /// <param name="upcoming">true = only future events, false = only past events, omitted = all.</param>
    /// <param name="eventType">Only events of this type: Run, Walk or Cycle.</param>
    /// <param name="organiserId">Only events created by this Organiser (used by the Organiser dashboard).</param>
    /// <param name="page">Page number, starting at 1 (default 1).</param>
    /// <param name="pageSize">Items per page, 1 to 100 (default 20).</param>
    /// <response code="200">A page of events plus the total count.</response>
    /// <response code="400">page or pageSize is out of range.</response>
    [HttpGet("api/events")]
    [ProducesResponseType(typeof(PagedResponse<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? search,
        [FromQuery] string? location,
        [FromQuery] bool? upcoming,
        [FromQuery] string? eventType,
        [FromQuery] int? organiserId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) return Fail(400, "page must be 1 or greater.");
        if (pageSize < 1 || pageSize > 100) return Fail(400, "pageSize must be between 1 and 100.");

        IQueryable<Event> query = _db.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(e => e.Name.ToLower().Contains(s) || (e.Description != null && e.Description.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var l = location.Trim().ToLower();
            query = query.Where(e => e.Location.ToLower().Contains(l));
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var type = Lookup.Match(EventTypes.All, eventType);
            if (type is null) return Fail(400, "eventType must be one of: Run, Walk, Cycle.");
            query = query.Where(e => e.EventType == type);
        }

        if (organiserId is not null)
            query = query.Where(e => e.OrganiserId == organiserId);

        var now = DateTime.Now;
        if (upcoming == true) query = query.Where(e => e.EventDate >= now);
        else if (upcoming == false) query = query.Where(e => e.EventDate < now);

        var total = await query.CountAsync();

        var ordered = upcoming == true
            ? query.OrderBy(e => e.EventDate)
            : query.OrderByDescending(e => e.EventDate);

        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // Confirmed enrolments per event on this page (one grouped query, not one per event).
        var eventIds = items.Select(e => e.EventId).ToList();
        // Fees are summed in memory: SQLite (used by the tests) cannot SUM decimal columns in SQL.
        var confirmed = await _db.Enrolments.AsNoTracking()
            .Where(en => en.Status == EnrolmentStatuses.Confirmed && eventIds.Contains(en.Category.EventId))
            .Select(en => new { EventId = en.Category.EventId, Fee = en.Category.EntryFee, HasResult = en.Result != null })
            .ToListAsync();
        var stats = confirmed
            .GroupBy(x => x.EventId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Results: g.Count(x => x.HasResult), Fees: g.Sum(x => x.Fee)));

        return Ok(new PagedResponse<EventResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(e =>
            {
                var s = stats.GetValueOrDefault(e.EventId);
                return e.ToResponse(s.Count, s.Results, s.Fees);
            }).ToList()
        });
    }

    /// <summary>Get one event, including its categories.</summary>
    /// <remarks>Public - no login needed.</remarks>
    /// <param name="id">The event id.</param>
    /// <response code="200">The event with its categories and how many people have enrolled in each.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("api/events/{id:int}")]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Fail(404, $"Event {id} was not found.");

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == id)
            .OrderBy(c => c.CategoryId)
            .Select(EventMappings.CategoryProjection)
            .ToListAsync();

        return Ok(ev.ToDetailResponse(categories));
    }

    /// <summary>Create a new event owned by the logged-in Organiser.</summary>
    /// <remarks>Each event captures a name, description, date, location, distance and type (Run, Walk or Cycle).</remarks>
    /// <param name="request">The event details.</param>
    /// <response code="201">Event created. The Location header points at it.</response>
    /// <response code="400">Validation failed (e.g. missing name, invalid event type).</response>
    [HttpPost("api/events")]
    [SessionAuthorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEvent([FromBody] EventRequest request)
    {
        var eventType = Lookup.Match(EventTypes.All, request.EventType);
        if (eventType is null) return Fail(400, "EventType must be one of: Run, Walk, Cycle.");

        var ev = new Event
        {
            OrganiserId = CurrentUserId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            EventType = eventType,
            DistanceKm = request.DistanceKm,
            BannerUrl = NormaliseUrl(request.BannerUrl),
            CreatedAt = DateTime.Now
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEvent), new { id = ev.EventId }, ev.ToResponse());
    }

    /// <summary>Update an event.</summary>
    /// <remarks>Only the Organiser who created the event may update it.</remarks>
    /// <param name="id">The event id.</param>
    /// <param name="request">The new event details.</param>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">The event belongs to a different Organiser.</response>
    /// <response code="404">No event with that id.</response>
    [HttpPut("api/events/{id:int}")]
    [SessionAuthorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(int id, [FromBody] EventRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Fail(404, $"Event {id} was not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You can only change events that you created.");

        var eventType = Lookup.Match(EventTypes.All, request.EventType);
        if (eventType is null) return Fail(400, "EventType must be one of: Run, Walk, Cycle.");

        ev.Name = request.Name.Trim();
        ev.Description = request.Description?.Trim();
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.EventType = eventType;
        ev.DistanceKm = request.DistanceKm;
        ev.BannerUrl = NormaliseUrl(request.BannerUrl);
        await _db.SaveChangesAsync();

        return Ok(ev.ToResponse());
    }

    /// <summary>Delete an event.</summary>
    /// <remarks>Only the owning Organiser may delete it. Its categories, enrolments and results are deleted too (cascade).</remarks>
    /// <param name="id">The event id.</param>
    /// <response code="204">Event deleted.</response>
    /// <response code="403">The event belongs to a different Organiser.</response>
    /// <response code="404">No event with that id.</response>
    [HttpDelete("api/events/{id:int}")]
    [SessionAuthorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Fail(404, $"Event {id} was not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You can only delete events that you created.");

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? NormaliseUrl(string? url) => string.IsNullOrWhiteSpace(url) ? null : url.Trim();
}
