namespace RaceDay.Api.Models;

/// <summary>Maps to dbo.Enrolments - links a Participant to a Category.</summary>
public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.Now;
    public string Status { get; set; } = EnrolmentStatuses.Confirmed;

    public AppUser Participant { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Result? Result { get; set; }
}
