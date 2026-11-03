namespace RaceDay.Api.Dtos;

/// <summary>What the API returns about a user. The password hash is deliberately not included.</summary>
public class AuthResponse
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}