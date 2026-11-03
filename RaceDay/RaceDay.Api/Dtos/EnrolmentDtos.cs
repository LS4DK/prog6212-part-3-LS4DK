using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class EnrolRequest
{
    /// <summary>The category the participant is entering, which must belong to the event.</summary>
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}

public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public EnrolmentStatus Status { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool HasResult { get; set; }
}