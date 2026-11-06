using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>A Participant's own enrolments and their status.</summary>
[RequireRole(RoleNames.Participant)]
public class EnrolmentsController : Controller
{
    private readonly ApiClient _api;

    public EnrolmentsController(ApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index()
    {
        var result = await _api.GetAsync<List<EnrolmentDto>>("api/users/me/enrolments");
        if (result.Unauthorized)
            return this.SessionExpired();

        return View(new EnrolmentsViewModel
        {
            Enrolments = result.Data ?? new List<EnrolmentDto>(),
            Error = result.Success ? null : result.Error
        });
    }
}