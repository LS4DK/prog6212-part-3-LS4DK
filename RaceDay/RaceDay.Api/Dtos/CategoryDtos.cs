using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class CategoryRequest
{
    /// <summary>Category name, e.g. "10km" or "Under 20".</summary>
    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;
}

public class CategoryResponse
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
}