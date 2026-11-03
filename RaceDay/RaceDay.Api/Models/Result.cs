namespace RaceDay.Api.Models;

/// <summary>The finish time and position for one enrolment (0..1 per enrolment).</summary>
public class Result
{
    public int ResultId { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? FinishPosition { get; set; }

    public int EnrolmentId { get; set; }
    public Enrolment Enrolment { get; set; } = null!;
}