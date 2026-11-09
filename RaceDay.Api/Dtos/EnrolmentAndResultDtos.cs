using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }

    /// <summary>Entry fee (rand) of the category this enrolment is for.</summary>
    public decimal EntryFee { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public DateTime EnrolmentDate { get; set; }

    /// <summary>"Confirmed" or "Cancelled".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>True once an Organiser has captured a result for this enrolment.</summary>
    public bool HasResult { get; set; }
}

/// <summary>Body for POST /api/enrolments/{id}/result and PUT /api/results/{id}.</summary>
public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss, e.g. "01:45:32". Required when Status is Finished.</summary>
    public TimeSpan? FinishTime { get; set; }

    /// <summary>Finishing position (1 = first). Required when Status is Finished.</summary>
    [Range(1, int.MaxValue)]
    public int? Position { get; set; }

    /// <summary>One of "Finished", "DNF" (did not finish) or "DSQ" (disqualified).</summary>
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class ResultResponse
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = string.Empty;
}

public static class EnrolmentResultMappings
{
    public static readonly Expression<Func<Enrolment, EnrolmentResponse>> EnrolmentProjection = en => new EnrolmentResponse
    {
        EnrolmentId = en.EnrolmentId,
        ParticipantId = en.ParticipantId,
        ParticipantName = en.Participant.Profile != null ? en.Participant.Profile.FullName : en.Participant.Email,
        CategoryId = en.CategoryId,
        CategoryName = en.Category.Name,
        DistanceKm = en.Category.DistanceKm,
        EntryFee = en.Category.EntryFee,
        EventId = en.Category.EventId,
        EventName = en.Category.Event.Name,
        EventDate = en.Category.Event.EventDate,
        EnrolmentDate = en.EnrolmentDate,
        Status = en.Status,
        HasResult = en.Result != null
    };

    public static readonly Expression<Func<Result, ResultResponse>> ResultProjection = r => new ResultResponse
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        ParticipantId = r.Enrolment.ParticipantId,
        ParticipantName = r.Enrolment.Participant.Profile != null ? r.Enrolment.Participant.Profile.FullName : r.Enrolment.Participant.Email,
        EventId = r.Enrolment.Category.EventId,
        EventName = r.Enrolment.Category.Event.Name,
        EventDate = r.Enrolment.Category.Event.EventDate,
        CategoryId = r.Enrolment.CategoryId,
        CategoryName = r.Enrolment.Category.Name,
        FinishTime = r.FinishTime,
        Position = r.Position,
        Status = r.Status
    };
}
