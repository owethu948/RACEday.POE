using RaceDay.Web.Helpers;

namespace RaceDay.Web.Tests;

public class DisplayTests
{
    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(47, "47th")]
    [InlineData(101, "101st")]
    [InlineData(111, "111th")]
    public void Ordinal_FormatsPositions(int number, string expected) =>
        Assert.Equal(expected, Display.Ordinal(number));

    [Fact]
    public void PositionOutOf_MatchesTheFormatInTheBrief()
    {
        Assert.Equal("47th out of 312 finishers", Display.PositionOutOf(47, 312));
        Assert.Equal("1st out of 1 finisher", Display.PositionOutOf(1, 1));
    }

    [Theory]
    [InlineData("Confirmed", "bg-success")]
    [InlineData("Pending", "bg-warning text-dark")]
    [InlineData("Cancelled", "bg-danger")]
    [InlineData("Finished", "bg-success")]
    [InlineData("DNF", "bg-warning text-dark")]
    [InlineData("DSQ", "bg-danger")]
    [InlineData("Something else", "bg-secondary")]
    public void BadgeClass_UsesOneColourPerStatus(string status, string expected) =>
        Assert.Equal(expected, Display.BadgeClass(status));

    [Fact]
    public void FinishTime_ShowsHoursMinutesSeconds()
    {
        Assert.Equal("1:45:32", Display.FinishTime(new TimeSpan(1, 45, 32)));
        Assert.Equal("0:09:05", Display.FinishTime(new TimeSpan(0, 9, 5)));
        Assert.Equal("\u2013", Display.FinishTime(null));
        Assert.Equal("", Display.FinishTimeInput(null));
    }

    [Fact]
    public void Money_UsesRandsAndInvariantSeparators()
    {
        Assert.Equal("R 250.00", Display.Money(250m));
        Assert.Equal("R 1,234.50", Display.Money(1234.5m));
    }

    [Fact]
    public void Banner_UsesTheAzureImageWhenThereIsOne()
    {
        Assert.True(Display.HasBanner("https://acct.blob.core.windows.net/raceday-images/banners/x.png"));
        Assert.False(Display.HasBanner(null));
        Assert.False(Display.HasBanner("  "));
    }

    [Fact]
    public void Gradient_IsStablePerEvent_AndCyclesThroughThreeColours()
    {
        Assert.Equal(Display.GradientClass(7), Display.GradientClass(7));
        Assert.Equal(3, Enumerable.Range(1, 3).Select(Display.GradientClass).Distinct().Count());
    }

    [Theory]
    [InlineData(1, "pos-gold")]
    [InlineData(2, "pos-silver")]
    [InlineData(3, "pos-bronze")]
    [InlineData(47, "pos-default")]
    public void PositionClass_GivesTopThreeMedals(int position, string expected) =>
        Assert.Equal(expected, Display.PositionClass(position));

    [Theory]
    [InlineData("Confirmed", "s-fin")]
    [InlineData("Pending", "s-dnf")]
    [InlineData("Cancelled", "s-dsq")]
    public void PillClass_MatchesTheStatusColours(string status, string expected) =>
        Assert.Equal(expected, Display.PillClass(status));

    [Fact]
    public void DaysUntil_SpeaksPlainEnglish()
    {
        Assert.Equal("today", Display.DaysUntil(DateTime.Today));
        Assert.Equal("tomorrow", Display.DaysUntil(DateTime.Today.AddDays(1)));
        Assert.Equal("in 12 days", Display.DaysUntil(DateTime.Today.AddDays(12)));
        Assert.Equal("in 6 months", Display.DaysUntil(DateTime.Today.AddDays(190)));
    }

    [Fact]
    public void Temp_RoundsToWholeDegrees() => Assert.Equal("18\u00B0C", Display.Temp(17.6));
}
