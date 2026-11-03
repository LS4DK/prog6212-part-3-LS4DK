namespace RaceDay.Api.Models;

public enum EnrolmentStatus
{
    Pending,
    Confirmed,
    Cancelled
}

/// <summary>
/// Junction entity: links a Participant to an Event under a chosen Category.
/// This resolves the many-to-many between Users and Events.
/// </summary>
public class Enrolment
{
    public int EnrolmentId { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.Now;
    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Confirmed;

    public int ParticipantId { get; set; }
    public User Participant { get; set; } = null!;

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    // Optional: only exists once the Organiser captures the result
    public Result? Result { get; set; }
}