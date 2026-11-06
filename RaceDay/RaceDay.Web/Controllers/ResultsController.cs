using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>A Participant's personal race history. Results are captured by the Organiser after the event.</summary>
[RequireRole(RoleNames.Participant)]
public class ResultsController : Controller
{
    private readonly ApiClient _api;

    public ResultsController(ApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index()
    {
        var result = await _api.GetAsync<List<ResultDto>>("api/users/me/results");
        if (result.Unauthorized)
            return this.SessionExpired();

        return View(new ResultsViewModel
        {
            Results = result.Data ?? new List<ResultDto>(),
            Error = result.Success ? null : result.Error
        });
    }
}