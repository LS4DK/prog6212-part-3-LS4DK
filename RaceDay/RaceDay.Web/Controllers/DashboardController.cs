using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>The Organiser's overview: their events, enrolments per event and upcoming dates.</summary>
[RequireRole(RoleNames.Organiser)]
public class DashboardController : Controller
{
    private readonly ApiClient _api;

    public DashboardController(ApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index()
    {
        var result = await _api.GetAsync<List<EventDto>>("api/events/mine");
        if (result.Unauthorized)
            return this.SessionExpired();

        return View(new DashboardViewModel
        {
            Events = result.Data ?? new List<EventDto>(),
            Error = result.Success ? null : result.Error
        });
    }
}