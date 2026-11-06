using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Participants browse and filter events, view the details and enrol.</summary>
[RequireRole(RoleNames.Participant)]
public class EventsController : Controller
{
    private readonly ApiClient _api;

    public EventsController(ApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index(string? search, EventType? eventType, DateTime? fromDate, DateTime? toDate)
    {
        // The filters are passed straight on to the API as a query string
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            parts.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (eventType.HasValue)
            parts.Add($"eventType={eventType.Value}");
        if (fromDate.HasValue)
            parts.Add($"fromDate={fromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (toDate.HasValue)
            parts.Add($"toDate={toDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");

        var url = "api/events" + (parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty);

        var events = await _api.GetAsync<List<EventDto>>(url);
        if (events.Unauthorized)
            return this.SessionExpired();

        var model = new EventListViewModel
        {
            Search = search,
            SelectedType = eventType,
            FromDate = fromDate,
            ToDate = toDate
        };

        if (events.Success)
            model.Events = events.Data ?? new List<EventDto>();
        else
            model.Error = events.Error;

        // Used to show an "Enrolled" badge on events the Participant has already entered
        var mine = await _api.GetAsync<List<EnrolmentDto>>("api/users/me/enrolments");
        if (mine.Success && mine.Data != null)
            model.EnrolledEventIds = mine.Data.Select(e => e.EventId).ToHashSet();

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var ev = await _api.GetAsync<EventDto>($"api/events/{id}");
        if (ev.Unauthorized)
            return this.SessionExpired();

        if (!ev.Success || ev.Data == null)
        {
            TempData["Error"] = ev.NotFound ? "That event could not be found." : ev.Error;
            return RedirectToAction(nameof(Index));
        }

        var mine = await _api.GetAsync<List<EnrolmentDto>>("api/users/me/enrolments");
        if (mine.Unauthorized)
            return this.SessionExpired();

        return View(new EventDetailsViewModel
        {
            Event = ev.Data,
            MyEnrolment = mine.Data?.FirstOrDefault(e => e.EventId == id)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enrol(int id, int categoryId)
    {
        if (categoryId <= 0)
        {
            TempData["Error"] = "Please choose a category to enter.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await _api.PostAsync<EnrolmentDto>($"api/events/{id}/enrolments", new { categoryId });
        if (result.Unauthorized)
            return this.SessionExpired();

        if (!result.Success)
        {
            TempData["Error"] = result.Error ?? "We couldn't enter you in that event. Please try again.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = $"You're in! You've entered {result.Data?.EventName} in the {result.Data?.CategoryName} category.";
        return RedirectToAction("Index", "Enrolments");
    }
}