using System.Globalization;

namespace RaceDay.Web.Helpers;

/// <summary>
/// Presentation helpers shared by every view, so the same value always looks the same everywhere
/// (a Confirmed enrolment is green on the participant's page AND the organiser's page).
/// </summary>
public static class Display
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Bootstrap classes for a status badge. Single source of truth for status colours.</summary>
    public static string BadgeClass(string? status) => status switch
    {
        "Confirmed" or "Finished" => "bg-success",
        "Pending" or "DNF" => "bg-warning text-dark",
        "Cancelled" or "DSQ" => "bg-danger",
        _ => "bg-secondary"
    };

    /// <summary>The same statuses as the prototype's soft pill colours (green / amber / red), used by list rows.</summary>
    public static string PillClass(string? status) => status switch
    {
        "Confirmed" or "Finished" => "s-fin",
        "Pending" or "DNF" => "s-dnf",
        "Cancelled" or "DSQ" => "s-dsq",
        _ => "s-past"
    };

    /// <summary>1 -> "1st", 2 -> "2nd", 3 -> "3rd", 11 -> "11th", 47 -> "47th".</summary>
    public static string Ordinal(int number)
    {
        var lastTwo = Math.Abs(number) % 100;
        var suffix = lastTwo is >= 11 and <= 13
            ? "th"
            : (Math.Abs(number) % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return number.ToString(Inv) + suffix;
    }

    /// <summary>"47th out of 312 finishers".</summary>
    public static string PositionOutOf(int position, int totalFinishers) =>
        $"{Ordinal(position)} out of {totalFinishers.ToString(Inv)} finisher{(totalFinishers == 1 ? "" : "s")}";

    /// <summary>1:45:32, or an en dash when there is no time.</summary>
    public static string FinishTime(TimeSpan? time) =>
        time is null ? "\u2013" : $"{(int)time.Value.TotalHours}:{time.Value.Minutes:00}:{time.Value.Seconds:00}";

    /// <summary>The same h:mm:ss text, for filling an input box (empty when there is no time).</summary>
    public static string FinishTimeInput(TimeSpan? time) => time is null ? string.Empty : FinishTime(time);

    public static string Money(decimal amount) => "R " + amount.ToString("N2", Inv);

    public static string Km(decimal km) => km.ToString("0.##", Inv) + " km";

    public static string Date(DateTime date) => date.ToString("d MMM yyyy", Inv);

    public static string DateTimeLong(DateTime date) => date.ToString("dddd, d MMMM yyyy 'at' HH:mm", Inv);

    public static string Day(DateTime date) => date.ToString("dd", Inv);

    public static string MonthShort(DateTime date) => date.ToString("MMM", Inv).ToUpperInvariant();

    /// <summary>Value for an &lt;input type="datetime-local"&gt;.</summary>
    public static string DateTimeInput(DateTime? date) => date?.ToString("yyyy-MM-dd'T'HH:mm", Inv) ?? string.Empty;

    public static string EventTypeIcon(string? type) => type switch
    {
        "Cycle" => "bi-bicycle",
        "Walk" => "bi-person-walking",
        _ => "bi-lightning-charge-fill"
    };

    public static bool HasBanner(string? bannerUrl) => !string.IsNullOrWhiteSpace(bannerUrl);

    /// <summary>Emoji shown on an event card that has no banner image.</summary>
    public static string EventEmoji(string? type) => type switch
    {
        "Cycle" => "\U0001F6B4\u200D\u2640\uFE0F",
        "Walk" => "\U0001F6B6\u200D\u2640\uFE0F",
        _ => "\U0001F3C3\u200D\u2640\uFE0F"
    };

    /// <summary>pink / lavender / rose gradient, chosen from the event id so each event keeps its colour.</summary>
    public static string GradientClass(int eventId) => (Math.Abs(eventId) % 3) switch
    {
        0 => "pink",
        1 => "lavender",
        _ => "rose"
    };

    /// <summary>Medal class for a finishing position: gold, silver, bronze or the default pink.</summary>
    public static string PositionClass(int? position) => position switch
    {
        1 => "pos-gold",
        2 => "pos-silver",
        3 => "pos-bronze",
        _ => "pos-default"
    };

    /// <summary>"Great", "Good" ... to a status-pill colour for the weather page.</summary>
    public static string ConditionClass(string? rating) => rating switch
    {
        "Excellent" => "s-fin",
        "Good" => "s-upcoming",
        "Fair" => "s-dnf",
        "Challenging" => "s-dsq",
        _ => "s-past"
    };

    /// <summary>"today", "tomorrow", "in 12 days", or "9 months away" for far-off events.</summary>
    public static string DaysUntil(DateTime date)
    {
        var days = (int)Math.Ceiling((date.Date - DateTime.Today).TotalDays);
        return days switch
        {
            <= 0 => "today",
            1 => "tomorrow",
            < 60 => $"in {days} days",
            _ => $"in {days / 30} months"
        };
    }

    public static string Temp(double celsius) => Math.Round(celsius).ToString("0", Inv) + "\u00B0C";

    public static string Plural(int count, string singular, string? plural = null) =>
        count == 1 ? $"{count} {singular}" : $"{count} {plural ?? singular + "s"}";
}
