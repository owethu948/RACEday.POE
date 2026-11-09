using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Participants entering events by choosing a category.</summary>
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Enrol the logged-in Participant in a category.</summary>
    /// <remarks>
    /// The participant id is taken from the session - the request has no body.
    /// The system records the link between the Participant, the event (via the category) and the category.
    /// An earlier cancelled enrolment in the same category is re-activated.
    /// </remarks>
    /// <param name="categoryId">The category the participant wants to enter.</param>
    /// <response code="201">Enrolment recorded with status Confirmed.</response>
    /// <response code="400">The event has already taken place.</response>
    /// <response code="404">No category with that id.</response>
    /// <response code="409">Already enrolled in this category, or the category is full.</response>
    [HttpPost("api/categories/{categoryId:int}/enrolments")]
    [SessionAuthorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int categoryId)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null) return Fail(404, $"Category {categoryId} was not found.");

        if (category.Event.EventDate < DateTime.Now)
            return Fail(400, "This event has already taken place, so enrolments are closed.");

        var participantId = CurrentUserId;
        var existing = await _db.Enrolments.FirstOrDefaultAsync(e => e.CategoryId == categoryId && e.ParticipantId == participantId);
        if (existing != null && existing.Status == EnrolmentStatuses.Confirmed)
            return Fail(409, "You are already enrolled in this category.");

        var confirmedCount = await _db.Enrolments.CountAsync(e => e.CategoryId == categoryId && e.Status == EnrolmentStatuses.Confirmed);
        if (confirmedCount >= category.MaxParticipants)
            return Fail(409, "This category is full.");

        Enrolment enrolment;
        if (existing != null)
        {
            existing.Status = EnrolmentStatuses.Confirmed;
            existing.EnrolmentDate = DateTime.Now;
            enrolment = existing;
        }
        else
        {
            enrolment = new Enrolment
            {
                ParticipantId = participantId,
                CategoryId = categoryId,
                EnrolmentDate = DateTime.Now,
                Status = EnrolmentStatuses.Confirmed
            };
            _db.Enrolments.Add(enrolment);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Fail(409, "You are already enrolled in this category.");
        }

        var response = await _db.Enrolments.AsNoTracking()
            .Where(e => e.EnrolmentId == enrolment.EnrolmentId)
            .Select(EnrolmentResultMappings.EnrolmentProjection)
            .FirstAsync();
        return Created("/api/users/me/enrolments", response);
    }

    /// <summary>List the logged-in Participant's own enrolments.</summary>
    /// <remarks>Past and upcoming events, newest event first.</remarks>
    /// <response code="200">The participant's enrolments.</response>
    [HttpGet("api/users/me/enrolments")]
    [SessionAuthorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyEnrolments()
    {
        var participantId = CurrentUserId;
        var items = await _db.Enrolments.AsNoTracking()
            .Where(e => e.ParticipantId == participantId)
            .OrderByDescending(e => e.Category.Event.EventDate)
            .Select(EnrolmentResultMappings.EnrolmentProjection)
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>List all enrolments for an event.</summary>
    /// <remarks>Only the Organiser who owns the event may view its enrolments.</remarks>
    /// <param name="eventId">The event id.</param>
    /// <response code="200">Every enrolment (Confirmed and Cancelled) in the event's categories.</response>
    /// <response code="403">The event belongs to a different Organiser.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventEnrolments(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Fail(404, $"Event {eventId} was not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You can only view enrolments for events that you created.");

        var items = await _db.Enrolments.AsNoTracking()
            .Where(e => e.Category.EventId == eventId)
            .OrderBy(e => e.CategoryId).ThenBy(e => e.EnrolmentId)
            .Select(EnrolmentResultMappings.EnrolmentProjection)
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>Cancel an enrolment.</summary>
    /// <remarks>
    /// Sets the status to Cancelled (the row is kept). May be called by the enrolled Participant
    /// or by the Organiser who owns the event; anyone else gets 403.
    /// </remarks>
    /// <param name="id">The enrolment id.</param>
    /// <response code="204">Enrolment cancelled.</response>
    /// <response code="403">The enrolment is not yours and the event is not yours.</response>
    /// <response code="404">No enrolment with that id.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var enrolment = await _db.Enrolments.Include(e => e.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return Fail(404, $"Enrolment {id} was not found.");

        var isOwnerParticipant = CurrentRole == Roles.Participant && enrolment.ParticipantId == CurrentUserId;
        var isEventOrganiser = CurrentRole == Roles.Organiser && enrolment.Category.Event.OrganiserId == CurrentUserId;
        if (!isOwnerParticipant && !isEventOrganiser)
            return Fail(403, "You can only cancel your own enrolment, or enrolments in events that you created.");

        if (enrolment.Status != EnrolmentStatuses.Cancelled)
        {
            enrolment.Status = EnrolmentStatuses.Cancelled;
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }
}
