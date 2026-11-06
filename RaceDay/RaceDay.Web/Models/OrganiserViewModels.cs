using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models;

public class DashboardViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public string? Error { get; set; }

    public List<EventDto> Upcoming =>
        Events.Where(e => e.Date.Date >= DateTime.Today).OrderBy(e => e.Date).ToList();

    public int TotalEnrolments => Events.Sum(e => e.EnrolmentCount);
}

public class OrganiserEventListViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public string? Error { get; set; }
}

public class EventFormViewModel
{
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please enter the event name.")]
    [StringLength(150)]
    [Display(Name = "Event name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "The description can be at most 2000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose the date and start time.")]
    [Display(Name = "Date and start time")]
    public DateTime? Date { get; set; }

    [Required(ErrorMessage = "Please enter the location.")]
    [StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the distance in kilometres.")]
    [Range(0.01, 9999.99, ErrorMessage = "The distance must be between 0.01 and 9999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? Distance { get; set; }

    [Required(ErrorMessage = "Please choose the event type.")]
    [Display(Name = "Event type")]
    public EventType? EventType { get; set; }

    // Filled in by the controller when editing (not posted by the form)
    public EventDto? Event { get; set; }
    public string? CurrentBannerUrl { get; set; }

    public bool IsEdit => EventId > 0;
}

/// <summary>An event together with everyone enrolled in it (used by the enrolments and results pages).</summary>
public class EventManageViewModel
{
    public EventDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();
    public string? Error { get; set; }
}