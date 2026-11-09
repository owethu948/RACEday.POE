namespace RaceDay.Api.Models;

/// <summary>Maps to dbo.Results - 1:1 with an Enrolment.</summary>
public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = ResultStatuses.Finished;

    public Enrolment Enrolment { get; set; } = null!;
}
