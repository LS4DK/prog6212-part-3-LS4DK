namespace RaceDay.Api.Models;

/// <summary>An account. The Role decides whether this person is an Organiser or a Participant.</summary>
public class User
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    // Only populated when this user is an Organiser
    public ICollection<Event> EventsOrganised { get; set; } = new List<Event>();

    // Only populated when this user is a Participant
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}