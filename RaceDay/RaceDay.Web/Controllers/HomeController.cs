using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : Controller
{
    private readonly UserSession _session;

    public HomeController(UserSession session)
    {
        _session = session;
    }

    public IActionResult Index()
    {
        // Participants land straight on the list of upcoming events
        if (_session.IsParticipant)
            return RedirectToAction("Index", "Events");

        return View();
    }

    // Shown when something unexpected goes wrong
    public IActionResult Error() => View();

    // Shown for missing pages (404) and other HTTP errors
    public IActionResult HttpError(int code = 500) => View(code);
}