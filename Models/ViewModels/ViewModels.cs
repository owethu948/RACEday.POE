using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models.ViewModels;

// ============================================================ shared
public class ErrorViewModel
{
    public int StatusCode { get; set; } = 500;
    public string Heading { get; set; } = "Something went wrong";
    public string Message { get; set; } = "Please try again in a moment.";
}

// ============================================================ account
public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "That doesn't look like an email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your full name."), StringLength(150, MinimumLength = 2, ErrorMessage = "Your name must be 2 to 150 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "That doesn't look like an email address."), StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a password."), StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Type your password again."), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The two passwords don't match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose how you'll use RaceDay.")]
    [Display(Name = "I want to")]
    public string Role { get; set; } = "Participant";

    [Display(Name = "Organiser code")]
    public string? OrganiserCode { get; set; }
}

public class ProfileViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your full name."), StringLength(150, MinimumLength = 2, ErrorMessage = "Your name must be 2 to 150 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Keep the phone number to 20 characters or fewer.")]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(150, ErrorMessage = "Keep the emergency contact to 150 characters or fewer.")]
    [Display(Name = "Emergency contact")]
    public string? EmergencyContact { get; set; }

    public string? ProfilePictureUrl { get; set; }

    [Display(Name = "Profile picture")]
    public IFormFile? NewPicture { get; set; }

    public bool RemovePicture { get; set; }
}

// ============================================================ public / participant
public class EventFilter
{
    public string? Search { get; set; }
    public string? Location { get; set; }
    public string? EventType { get; set; }
    public int Page { get; set; } = 1;

    public bool IsFiltered =>
        !string.IsNullOrWhiteSpace(Search) || !string.IsNullOrWhiteSpace(Location) || !string.IsNullOrWhiteSpace(EventType);
}

public class HomeIndexViewModel
{
    public PagedDto<EventDto> Events { get; set; } = new();
    public EventFilter Filter { get; set; } = new();

    // Participant-only extras (all zero / empty for visitors)
    public bool IsParticipant { get; set; }
    public HashSet<int> EnrolledEventIds { get; set; } = new();
    public decimal FeesPaid { get; set; }
    public int RacesFinished { get; set; }
}

public class EventDetailsViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public bool HasTakenPlace { get; set; }
    /// <summary>Categories the signed-in participant is currently Confirmed in.</summary>
    public HashSet<int> EnrolledCategoryIds { get; set; } = new();
    public List<ResultDto> Leaderboard { get; set; } = new();
    public int TotalFinishers { get; set; }
}

public class MyResultRow
{
    public ResultDto Result { get; set; } = new();
    public int TotalFinishers { get; set; }
}

public class MyEnrolmentsViewModel
{
    public List<EnrolmentDto> Items { get; set; } = new();

    /// <summary>"all", "upcoming" or "past".</summary>
    public string Tab { get; set; } = "all";
    public int AllCount { get; set; }
    public int UpcomingCount { get; set; }
    public int PastCount { get; set; }
}

public class MyResultsViewModel
{
    public List<MyResultRow> Rows { get; set; } = new();
    public int Finished => Rows.Count(r => r.Result.Status == "Finished");
    public int DidNotFinish => Rows.Count(r => r.Result.Status != "Finished");

    /// <summary>Fastest finish in the category the participant has finished most often (null when they have no finishes).</summary>
    public ResultDto? PersonalBest { get; set; }
}

public class WeatherPageViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public int? SelectedEventId { get; set; }
    public WeatherDto? Weather { get; set; }

    /// <summary>Friendly reason when the forecast could not be loaded.</summary>
    public string? Problem { get; set; }
}

public class EnrolModalViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public HashSet<int> EnrolledCategoryIds { get; set; } = new();
}

public class NotificationItem
{
    public string Icon { get; set; } = "\U0001F380";
    public string Text { get; set; } = string.Empty;
    public string When { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool Unread { get; set; } = true;
}

// ============================================================ organiser
public class OrganiserDashboardViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public int TotalEvents { get; set; }
    public int TotalEnrolments { get; set; }
    public int ResultsCaptured { get; set; }
    public decimal TotalEntryFees { get; set; }
    public int UpcomingCount { get; set; }
    public EventDto? NextEvent { get; set; }
    public int MaxEnrolments { get; set; }
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Give the event a name."), StringLength(200, MinimumLength = 2, ErrorMessage = "The name must be 2 to 200 characters.")]
    [Display(Name = "Event name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "The description can be up to 1000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose the date and time the event starts.")]
    [Display(Name = "Date and time")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Enter where the event takes place."), StringLength(200, ErrorMessage = "The location can be up to 200 characters.")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose an event type.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Range(0.01, 999.99, ErrorMessage = "Distance must be between 0.01 and 999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; }

    public string? ExistingBannerUrl { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public bool RemoveBanner { get; set; }

    /// <summary>Categories typed on the Create page, so an event and its categories are made in one go.</summary>
    public List<CategoryRowInput> Categories { get; set; } = new();

    public bool IsEdit => EventId.HasValue;
}

/// <summary>One category row typed on the Create-event page. Unvalidated by attributes: blank rows are simply ignored.</summary>
public class CategoryRowInput
{
    public string? Name { get; set; }
    public decimal? DistanceKm { get; set; }
    public int? MaxParticipants { get; set; }
    public decimal? EntryFee { get; set; }

    public bool IsBlank =>
        string.IsNullOrWhiteSpace(Name) && DistanceKm is null && MaxParticipants is null && EntryFee is null;
}

public class CategoriesViewModel
{
    public EventDetailDto Event { get; set; } = new();
}

public class CategoryFormModel
{
    [Required(ErrorMessage = "Give the category a name."), StringLength(100, MinimumLength = 1, ErrorMessage = "The name can be up to 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 999.99, ErrorMessage = "Distance must be between 0.01 and 999.99 km.")]
    public decimal DistanceKm { get; set; }

    [Range(1, 1_000_000, ErrorMessage = "Maximum participants must be at least 1.")]
    public int MaxParticipants { get; set; } = 100;

    [Range(0, 999_999.99, ErrorMessage = "Entry fee can't be negative.")]
    public decimal EntryFee { get; set; }
}

public class EventEnrolmentsViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();
    public string? StatusFilter { get; set; }
    public int ConfirmedCount => Enrolments.Count(e => e.Status == "Confirmed");
}

public class ResultRow
{
    public int EnrolmentId { get; set; }
    public int? ResultId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = "Finished";
    public bool HasResult => ResultId.HasValue;
}

public class EventResultsViewModel
{
    public EventDetailDto Event { get; set; } = new();

    /// <summary>The organiser's other events, shown as tabs at the top of the page.</summary>
    public List<EventDto> OrganiserEvents { get; set; } = new();
    public bool HasTakenPlace { get; set; }
    public List<ResultRow> Rows { get; set; } = new();
}

public class ResultFormModel
{
    public int? ResultId { get; set; }

    [Required]
    public string Status { get; set; } = "Finished";

    public string? FinishTime { get; set; }

    public int? Position { get; set; }
}
