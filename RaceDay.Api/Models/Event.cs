namespace RaceDay.Api.Models;

/// <summary>
/// Maps to dbo.Events. EventType and DistanceKm are the two columns added in Part 2
/// because the Part 2 brief requires every event to capture a distance and a type.
/// </summary>
public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string EventType { get; set; } = EventTypes.Run;
    public decimal DistanceKm { get; set; }

    /// <summary>Public Azure Blob Storage URL of the event banner image (null = no banner).</summary>
    public string? BannerUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public AppUser Organiser { get; set; } = null!;
    public ICollection<Category> Categories { get; set; } = new List<Category>();
}
