namespace RaceDay.Api.Models;

/// <summary>An age or distance category (e.g. "10km", "Under 20") belonging to one event.</summary>
public class Category
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}