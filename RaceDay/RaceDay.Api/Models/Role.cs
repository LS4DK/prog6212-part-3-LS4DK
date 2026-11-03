namespace RaceDay.Api.Models;

/// <summary>Lookup table holding the two roles: Organiser and Participant.</summary>
public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}