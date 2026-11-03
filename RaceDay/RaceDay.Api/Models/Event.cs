namespace RaceDay.Api.Models;

public enum EventType
{
    Run,
    Walk,
    Cycle
}

/// <summary>A race, walk or cycle event created by an Organiser.</summary>
public class Event
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal Distance { get; set; }
    public EventType EventType { get; set; }
    public string? BannerImageUrl { get; set; }

    public int OrganiserId { get; set; }
    public User Organiser { get; set; } = null!;

    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}