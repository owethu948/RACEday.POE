using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Finish times and positions captured by Organisers; viewed by Participants and the public.</summary>
public class ResultsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Capture the result for a participant's enrolment.</summary>
    /// <remarks>
    /// Only the Organiser who owns the event may capture results. A Finished result needs both
    /// finishTime (hh:mm:ss) and position; DNF and DSQ do not. Each enrolment can have one result.
    /// </remarks>
    /// <param name="enrolmentId">The enrolment the result belongs to.</param>
    /// <param name="request">Finish time, position and status (Finished, DNF or DSQ).</param>
    /// <response code="201">Result captured.</response>
    /// <response code="400">Validation failed (e.g. Finished without a position).</response>
    /// <response code="403">The enrolment is in a different Organiser's event.</response>
    /// <response code="404">No enrolment with that id.</response>
    /// <response code="409">A result already exists for this enrolment, or the enrolment was cancelled.</response>
    [HttpPost("api/enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CaptureResult(int enrolmentId, [FromBody] ResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
        if (enrolment is null) return Fail(404, $"Enrolment {enrolmentId} was not found.");
        if (enrolment.Category.Event.OrganiserId != CurrentUserId)
            return Fail(403, "You can only capture results for events that you created.");

        if (enrolment.Status != EnrolmentStatuses.Confirmed)
            return Fail(409, "Results cannot be captured for a cancelled enrolment.");
        if (enrolment.Result != null)
            return Fail(409, "A result already exists for this enrolment. Use PUT /api/results/{id} to correct it.");

        var error = Validate(request, out var status);
        if (error != null) return Fail(400, error);

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishTime = request.FinishTime,
            Position = request.Position,
            Status = status
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var response = await GetResultResponse(result.ResultId);
        return Created($"/api/events/{response.EventId}/results", response);
    }

    /// <summary>Correct a previously captured result.</summary>
    /// <remarks>Only the Organiser who owns the event may update its results.</remarks>
    /// <param name="id">The result id.</param>
    /// <param name="request">The corrected finish time, position and status.</param>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">The result is in a different Organiser's event.</response>
    /// <response code="404">No result with that id.</response>
    [HttpPut("api/results/{id:int}")]
    [SessionAuthorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateResult(int id, [FromBody] ResultRequest request)
    {
        var result = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return Fail(404, $"Result {id} was not found.");
        if (result.Enrolment.Category.Event.OrganiserId != CurrentUserId)
            return Fail(403, "You can only change results for events that you created.");

        var error = Validate(request, out var status);
        if (error != null) return Fail(400, error);

        result.FinishTime = request.FinishTime;
        result.Position = request.Position;
        result.Status = status;
        await _db.SaveChangesAsync();

        return Ok(await GetResultResponse(id));
    }

    /// <summary>Get the logged-in Participant's own results.</summary>
    /// <remarks>Personal performance history across all events, newest event first. Participants only see their own results.</remarks>
    /// <response code="200">The participant's results.</response>
    [HttpGet("api/users/me/results")]
    [SessionAuthorize(Roles = Roles.Participant)]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyResults()
    {
        var participantId = CurrentUserId;
        var items = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == participantId)
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .Select(EnrolmentResultMappings.ResultProjection)
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>Get the results (leaderboard) of an event.</summary>
    /// <remarks>Public - no login needed. Ordered by finishing position; entries without a position (DNF/DSQ) come last.</remarks>
    /// <param name="eventId">The event id.</param>
    /// <response code="200">The event's results.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("api/events/{eventId:int}/results")]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventResults(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return Fail(404, $"Event {eventId} was not found.");

        var items = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment.Category.EventId == eventId)
            .Select(EnrolmentResultMappings.ResultProjection)
            .ToListAsync();

        // Sorted in memory: tiny result sets, and avoids provider differences when ordering nullable values.
        var ordered = items.OrderBy(r => r.Position ?? int.MaxValue).ThenBy(r => r.ResultId).ToList();
        return Ok(ordered);
    }

    private async Task<ResultResponse> GetResultResponse(int resultId)
    {
        return await _db.Results.AsNoTracking()
            .Where(r => r.ResultId == resultId)
            .Select(EnrolmentResultMappings.ResultProjection)
            .FirstAsync();
    }

    /// <summary>Returns an error message, or null when the request is valid. Outputs the canonical status.</summary>
    private static string? Validate(ResultRequest request, out string status)
    {
        status = string.Empty;
        var matched = Lookup.Match(ResultStatuses.All, request.Status);
        if (matched is null) return "Status must be one of: Finished, DNF, DSQ.";
        status = matched;

        if (request.FinishTime is { } time && time < TimeSpan.Zero)
            return "FinishTime cannot be negative.";

        if (matched == ResultStatuses.Finished && (request.FinishTime is null || request.Position is null))
            return "A Finished result needs both finishTime and position.";

        return null;
    }
}
