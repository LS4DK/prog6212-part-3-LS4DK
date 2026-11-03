using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss, e.g. "01:47:32".</summary>
    [Required]
    public TimeSpan? FinishTime { get; set; }

    /// <summary>Finishing position, e.g. 47.</summary>
    [Required, Range(1, 1000000)]
    public int? FinishPosition { get; set; }
}

public class ResultResponse
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ParticipantName { get; set; } = string.Empty;
    public TimeSpan? FinishTime { get; set; }
    public int? FinishPosition { get; set; }
    public int TotalFinishers { get; set; }
}