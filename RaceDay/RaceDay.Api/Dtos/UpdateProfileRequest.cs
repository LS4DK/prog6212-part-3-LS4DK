using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class UpdateProfileRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;
}