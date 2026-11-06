using System.Net;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Everything an Organiser does to their own events: details, banner, categories, enrolments and results.</summary>
[RequireRole(RoleNames.Organiser)]
public class OrganiserEventsController : Controller
{
    private readonly ApiClient _api;
    private readonly UserSession _session;

    public OrganiserEventsController(ApiClient api, UserSession session)
    {
        _api = api;
        _session = session;
    }

    // ---------- Event list ----------

    public async Task<IActionResult> Index()
    {
        var result = await _api.GetAsync<List<EventDto>>("api/events/mine");
        if (result.Unauthorized)
            return this.SessionExpired();

        // Upcoming events first (soonest first), then past events (most recent first)
        var all = result.Data ?? new List<EventDto>();
        var upcoming = all.Where(e => e.Date.Date >= DateTime.Today).OrderBy(e => e.Date);
        var past = all.Where(e => e.Date.Date < DateTime.Today).OrderByDescending(e => e.Date);

        return View(new OrganiserEventListViewModel
        {
            Events = upcoming.Concat(past).ToList(),
            Error = result.Success ? null : result.Error
        });
    }

    // ---------- Create ----------

    [HttpGet]
    public IActionResult Create() => View("Form", new EventFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventFormViewModel model, IFormFile? banner)
    {
        if (!ModelState.IsValid)
            return View("Form", model);

        var created = await _api.PostAsync<EventDto>("api/events", ToRequest(model));
        if (created.Unauthorized)
            return this.SessionExpired();

        if (!created.Success || created.Data == null)
        {
            ModelState.AddModelError(string.Empty, created.Error ?? "We couldn't create the event. Please try again.");
            return View("Form", model);
        }

        var bannerProblem = await UploadBannerIfAnyAsync(created.Data.EventId, banner);

        TempData["Success"] = $"\"{created.Data.Name}\" has been created. Now add some categories so people can enter.";
        if (bannerProblem != null)
            TempData["Error"] = bannerProblem;

        return RedirectToAction(nameof(Categories), new { id = created.Data.EventId });
    }

    // ---------- Edit ----------

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (ev, failure) = await LoadOwnEventAsync(id);
        if (failure != null)
            return failure;

        return View("Form", new EventFormViewModel
        {
            EventId = ev!.EventId,
            Name = ev.Name,
            Description = ev.Description,
            Date = ev.Date,
            Location = ev.Location,
            Distance = ev.Distance,
            EventType = ev.EventType,
            Event = ev,
            CurrentBannerUrl = ev.BannerImageUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model, IFormFile? banner)
    {
        var (ev, failure) = await LoadOwnEventAsync(id);
        if (failure != null)
            return failure;

        model.EventId = id;
        model.Event = ev;
        model.CurrentBannerUrl = ev!.BannerImageUrl;

        if (!ModelState.IsValid)
            return View("Form", model);

        var updated = await _api.PutAsync<EventDto>($"api/events/{id}", ToRequest(model));
        if (updated.Unauthorized)
            return this.SessionExpired();

        if (!updated.Success)
        {
            ModelState.AddModelError(string.Empty, updated.Error ?? "We couldn't save your changes. Please try again.");
            return View("Form", model);
        }

        var bannerProblem = await UploadBannerIfAnyAsync(id, banner);

        TempData["Success"] = "Your changes have been saved.";
        if (bannerProblem != null)
            TempData["Error"] = bannerProblem;

        return RedirectToAction(nameof(Index));
    }

    // ---------- Delete ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _api.DeleteAsync($"api/events/{id}");
        if (result.Unauthorized)
            return this.SessionExpired();

        if (result.Success)
            TempData["Success"] = "The event has been deleted.";
        else
            TempData["Error"] = result.Error ?? "We couldn't delete that event. Please try again.";

        return RedirectToAction(nameof(Index));
    }

    // ---------- Categories ----------

    public async Task<IActionResult> Categories(int id)
    {
        var (ev, failure) = await LoadOwnEventAsync(id);
        if (failure != null)
            return failure;

        return View(ev);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(int id, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Please enter a category name.";
            return RedirectToAction(nameof(Categories), new { id });
        }

        var result = await _api.PostAsync<CategoryDto>($"api/events/{id}/categories", new { name = name.Trim() });
        if (result.Unauthorized)
            return this.SessionExpired();

        if (result.Success)
            TempData["Success"] = $"Category \"{name.Trim()}\" added.";
        else
            TempData["Error"] = result.Error ?? "We couldn't add that category. Please try again.";

        return RedirectToAction(nameof(Categories), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, int categoryId)
    {
        var result = await _api.DeleteAsync($"api/categories/{categoryId}");
        if (result.Unauthorized)
            return this.SessionExpired();

        if (result.Success)
            TempData["Success"] = "Category removed.";
        else
            TempData["Error"] = result.Error ?? "We couldn't remove that category. Please try again.";

        return RedirectToAction(nameof(Categories), new { id });
    }

    // ---------- Enrolments ----------

    public async Task<IActionResult> Enrolments(int id)
    {
        var (model, failure) = await LoadManageModelAsync(id);
        if (failure != null)
            return failure;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEnrolmentStatus(int id, int enrolmentId, EnrolmentStatus status)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please choose a valid status.";
            return RedirectToAction(nameof(Enrolments), new { id });
        }

        var result = await _api.PutAsync<EnrolmentDto>($"api/enrolments/{enrolmentId}/status", new { status });
        if (result.Unauthorized)
            return this.SessionExpired();

        if (result.Success)
            TempData["Success"] = $"{result.Data?.ParticipantName}'s enrolment is now {status}.";
        else
            TempData["Error"] = result.Error ?? "We couldn't change that status. Please try again.";

        return RedirectToAction(nameof(Enrolments), new { id });
    }

    // ---------- Results ----------

    public async Task<IActionResult> Results(int id)
    {
        var (model, failure) = await LoadManageModelAsync(id);
        if (failure != null)
            return failure;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveResult(int id, int enrolmentId, string? finishTime, int? finishPosition, bool hasResult)
    {
        if (!TimeParser.TryParseFinishTime(finishTime, out var time))
        {
            TempData["Error"] = "Please enter the finish time as h:mm:ss (for example 1:47:32) or m:ss (for example 47:05).";
            return RedirectToAction(nameof(Results), new { id });
        }

        if (finishPosition is null or < 1)
        {
            TempData["Error"] = "Please enter the finishing position as a whole number, 1 or higher.";
            return RedirectToAction(nameof(Results), new { id });
        }

        var url = $"api/enrolments/{enrolmentId}/result";
        var body = new { finishTime = time, finishPosition = finishPosition.Value };

        // New result: POST. Existing result: PUT. If the page was out of date, switch to the other one.
        var result = hasResult
            ? await _api.PutAsync<ResultDto>(url, body)
            : await _api.PostAsync<ResultDto>(url, body);

        if (!hasResult && result.StatusCode == HttpStatusCode.Conflict)
            result = await _api.PutAsync<ResultDto>(url, body);
        else if (hasResult && result.NotFound)
            result = await _api.PostAsync<ResultDto>(url, body);

        if (result.Unauthorized)
            return this.SessionExpired();

        if (result.Success)
            TempData["Success"] = $"Result saved for {result.Data?.ParticipantName}. It now shows on their My Results page.";
        else
            TempData["Error"] = result.Error ?? "We couldn't save that result. Please try again.";

        return RedirectToAction(nameof(Results), new { id });
    }

    // ---------- Helpers ----------

    // The body the API expects for creating or updating an event
    private static object ToRequest(EventFormViewModel model) => new
    {
        name = model.Name.Trim(),
        description = model.Description?.Trim(),
        date = model.Date,
        location = model.Location.Trim(),
        distance = model.Distance,
        eventType = model.EventType
    };

    // Loads an event and checks it belongs to the logged-in Organiser (the API checks again on every change)
    private async Task<(EventDto? Event, IActionResult? Failure)> LoadOwnEventAsync(int id)
    {
        var result = await _api.GetAsync<EventDto>($"api/events/{id}");
        if (result.Unauthorized)
            return (null, this.SessionExpired());

        if (!result.Success || result.Data == null)
        {
            TempData["Error"] = result.NotFound ? "That event could not be found." : result.Error;
            return (null, RedirectToAction(nameof(Index)));
        }

        if (result.Data.OrganiserId != _session.UserId)
        {
            TempData["Error"] = "You can only manage your own events.";
            return (null, RedirectToAction(nameof(Index)));
        }

        return (result.Data, null);
    }

    private async Task<(EventManageViewModel? Model, IActionResult? Failure)> LoadManageModelAsync(int id)
    {
        var (ev, failure) = await LoadOwnEventAsync(id);
        if (failure != null)
            return (null, failure);

        var enrolments = await _api.GetAsync<List<EnrolmentDto>>($"api/events/{id}/enrolments");
        if (enrolments.Unauthorized)
            return (null, this.SessionExpired());

        return (new EventManageViewModel
        {
            Event = ev!,
            Enrolments = enrolments.Data ?? new List<EnrolmentDto>(),
            Error = enrolments.Success ? null : enrolments.Error
        }, null);
    }

    // Returns a message if the banner could not be saved (the event itself was already saved)
    private async Task<string?> UploadBannerIfAnyAsync(int eventId, IFormFile? banner)
    {
        if (banner == null || banner.Length == 0)
            return null;

        var upload = await _api.UploadAsync<EventDto>($"api/events/{eventId}/banner", banner);
        return upload.Success
            ? null
            : $"The event was saved, but the banner couldn't be uploaded: {upload.Error} You can try again by editing the event.";
    }
}