using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>Body for POST/PUT of an event.</summary>
public class EventRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>Date and time the event starts, e.g. 2027-06-13T05:30:00.</summary>
    [Required]
    public DateTime? EventDate { get; set; }

    [Required, StringLength(200)]
    public string Location { get; set; } = string.Empty;

    /// <summary>One of "Run", "Walk" or "Cycle".</summary>
    [Required]
    public string EventType { get; set; } = string.Empty;

    /// <summary>Headline distance of the event in kilometres.</summary>
    [Range(0.01, 999.99)]
    public decimal DistanceKm { get; set; }

    /// <summary>Optional banner image URL, as returned by POST /api/files/banner. Null or empty removes the banner.</summary>
    [Url, StringLength(500)]
    public string? BannerUrl { get; set; }
}

public class EventResponse
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public string? BannerUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Number of Confirmed enrolments across all categories of the event.</summary>
    public int EnrolmentCount { get; set; }

    /// <summary>How many Confirmed enrolments already have a published result (list endpoint only).</summary>
    public int ResultCount { get; set; }

    /// <summary>Sum of the entry fees of all Confirmed enrolments, in rand (list endpoint only).</summary>
    public decimal TotalEntryFees { get; set; }
}

/// <summary>An event together with its categories.</summary>
public class EventDetailResponse : EventResponse
{
    public List<CategoryResponse> Categories { get; set; } = new();
}

/// <summary>Paged list wrapper used by GET /api/events.</summary>
public class PagedResponse<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<T> Items { get; set; } = new();
}

/// <summary>Body for POST/PUT of a category.</summary>
public class CategoryRequest
{
    /// <summary>Display name, e.g. "10km", "21km", "Under 20" or "Senior".</summary>
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 999.99)]
    public decimal DistanceKm { get; set; }

    [Range(1, 1_000_000)]
    public int MaxParticipants { get; set; }

    [Range(0, 999_999.99)]
    public decimal EntryFee { get; set; }
}

public class CategoryResponse
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int MaxParticipants { get; set; }
    public decimal EntryFee { get; set; }

    /// <summary>Number of Confirmed enrolments in this category so far.</summary>
    public int EnrolledCount { get; set; }
}

public static class EventMappings
{
    public static EventResponse ToResponse(this Event e, int enrolmentCount = 0, int resultCount = 0, decimal totalEntryFees = 0) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        Name = e.Name,
        Description = e.Description,
        EventDate = e.EventDate,
        Location = e.Location,
        EventType = e.EventType,
        DistanceKm = e.DistanceKm,
        BannerUrl = e.BannerUrl,
        CreatedAt = e.CreatedAt,
        EnrolmentCount = enrolmentCount,
        ResultCount = resultCount,
        TotalEntryFees = totalEntryFees
    };

    public static EventDetailResponse ToDetailResponse(this Event e, List<CategoryResponse> categories) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        Name = e.Name,
        Description = e.Description,
        EventDate = e.EventDate,
        Location = e.Location,
        EventType = e.EventType,
        DistanceKm = e.DistanceKm,
        BannerUrl = e.BannerUrl,
        CreatedAt = e.CreatedAt,
        EnrolmentCount = categories.Sum(c => c.EnrolledCount),
        Categories = categories
    };

    /// <summary>EF projection so the Confirmed-enrolment count is computed in SQL.</summary>
    public static readonly Expression<Func<Category, CategoryResponse>> CategoryProjection = c => new CategoryResponse
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        Name = c.Name,
        DistanceKm = c.DistanceKm,
        MaxParticipants = c.MaxParticipants,
        EntryFee = c.EntryFee,
        EnrolledCount = c.Enrolments.Count(en => en.Status == EnrolmentStatuses.Confirmed)
    };
}
