using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models;

public class EventListViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public HashSet<int> EnrolledEventIds { get; set; } = new();
    public string? Search { get; set; }
    public EventType? SelectedType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Error { get; set; }

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search) || SelectedType.HasValue || FromDate.HasValue || ToDate.HasValue;
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = new();
    public EnrolmentDto? MyEnrolment { get; set; }
}

public class EnrolmentsViewModel
{
    public List<EnrolmentDto> Enrolments { get; set; } = new();
    public string? Error { get; set; }
}

public class ResultsViewModel
{
    public List<ResultDto> Results { get; set; } = new();
    public string? Error { get; set; }
}

public class UpdateProfileViewModel
{
    [Required(ErrorMessage = "Please enter your name.")]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;
}

public class ProfilePageViewModel
{
    public UserDto User { get; set; } = new();
    public UpdateProfileViewModel Form { get; set; } = new();
}