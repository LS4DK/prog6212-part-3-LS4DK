using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class EventRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DateTime? Date { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    /// <summary>Distance in kilometres.</summary>
    [Range(0.01, 9999.99)]
    public decimal Distance { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    [EnumDataType(typeof(EventType))]
    public EventType EventType { get; set; }
}

public class EventResponse
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
    public string OrganiserName { get; set; } = string.Empty;
    public int EnrolmentCount { get; set; }
    public List<CategoryResponse> Categories { get; set; } = new();
}