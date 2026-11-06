using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Web.Services;

public static class ControllerExtensions
{
    /// <summary>Use when an API call comes back 401: sends the user to the login page with a message.</summary>
    public static IActionResult SessionExpired(this Controller controller)
    {
        controller.TempData["Error"] = "Your session has expired. Please log in again.";
        return controller.RedirectToAction("Login", "Account");
    }
}