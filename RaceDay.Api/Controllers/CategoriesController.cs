using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Age or distance categories within an event.</summary>
public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>List the categories of an event.</summary>
    /// <remarks>Public - both roles (and anonymous visitors) can view categories.</remarks>
    /// <param name="eventId">The event id.</param>
    /// <response code="200">The event's categories (e.g. 10km, 21km, Under 20, Senior).</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategories(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return Fail(404, $"Event {eventId} was not found.");

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.CategoryId)
            .Select(EventMappings.CategoryProjection)
            .ToListAsync();

        return Ok(categories);
    }

    /// <summary>Add a category to an event.</summary>
    /// <remarks>Only the Organiser who owns the event may add categories.</remarks>
    /// <param name="eventId">The event id.</param>
    /// <param name="request">Name (e.g. "10km" or "Under 20"), distance in km, maximum participants and entry fee.</param>
    /// <response code="201">Category created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">The event belongs to a different Organiser.</response>
    /// <response code="404">No event with that id.</response>
    [HttpPost("api/events/{eventId:int}/categories")]
    [SessionAuthorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddCategory(int eventId, [FromBody] CategoryRequest request)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Fail(404, $"Event {eventId} was not found.");
        if (ev.OrganiserId != CurrentUserId) return Fail(403, "You can only add categories to events that you created.");

        var category = new Category
        {
            EventId = eventId,
            Name = request.Name.Trim(),
            DistanceKm = request.DistanceKm,
            MaxParticipants = request.MaxParticipants,
            EntryFee = request.EntryFee
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var response = new CategoryResponse
        {
            CategoryId = category.CategoryId,
            EventId = category.EventId,
            Name = category.Name,
            DistanceKm = category.DistanceKm,
            MaxParticipants = category.MaxParticipants,
            EntryFee = category.EntryFee,
            EnrolledCount = 0
        };
        return Created($"/api/events/{eventId}/categories", response);
    }

    /// <summary>Update a category.</summary>
    /// <remarks>Only the Organiser who owns the category's event may update it.</remarks>
    /// <param name="id">The category id.</param>
    /// <param name="request">The new category details.</param>
    /// <response code="200">The updated category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">The category belongs to a different Organiser's event.</response>
    /// <response code="404">No category with that id.</response>
    [HttpPut("api/categories/{id:int}")]
    [SessionAuthorize(Roles = Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return Fail(404, $"Category {id} was not found.");
        if (category.Event.OrganiserId != CurrentUserId) return Fail(403, "You can only change categories of events that you created.");

        category.Name = request.Name.Trim();
        category.DistanceKm = request.DistanceKm;
        category.MaxParticipants = request.MaxParticipants;
        category.EntryFee = request.EntryFee;
        await _db.SaveChangesAsync();

        var response = await _db.Categories.AsNoTracking()
            .Where(c => c.CategoryId == id)
            .Select(EventMappings.CategoryProjection)
            .FirstAsync();
        return Ok(response);
    }

    /// <summary>Delete a category.</summary>
    /// <remarks>Only the Organiser who owns the category's event may delete it. Its enrolments and results are deleted too (cascade).</remarks>
    /// <param name="id">The category id.</param>
    /// <response code="204">Category deleted.</response>
    /// <response code="403">The category belongs to a different Organiser's event.</response>
    /// <response code="404">No category with that id.</response>
    [HttpDelete("api/categories/{id:int}")]
    [SessionAuthorize(Roles = Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return Fail(404, $"Category {id} was not found.");
        if (category.Event.OrganiserId != CurrentUserId) return Fail(403, "You can only delete categories of events that you created.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
