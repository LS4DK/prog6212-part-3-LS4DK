using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>View and update your own details and profile picture. Open to both roles.</summary>
[RequireRole]
public class ProfileController : Controller
{
    private readonly ApiClient _api;
    private readonly UserSession _session;

    public ProfileController(ApiClient api, UserSession session)
    {
        _api = api;
        _session = session;
    }

    public async Task<IActionResult> Index()
    {
        var page = await LoadPageAsync(null);
        if (page == null)
            return LoadFailed();

        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update([Bind(Prefix = "Form")] UpdateProfileViewModel form)
    {
        if (!ModelState.IsValid)
        {
            var invalidPage = await LoadPageAsync(form);
            return invalidPage == null ? LoadFailed() : View("Index", invalidPage);
        }

        var result = await _api.PutAsync<UserDto>("api/users/me", new { name = form.Name, email = form.Email });
        if (result.Unauthorized)
            return this.SessionExpired();

        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't save your changes. Please try again.");
            var failedPage = await LoadPageAsync(form);
            return failedPage == null ? LoadFailed() : View("Index", failedPage);
        }

        _session.UpdateName(result.Data.Name);
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        if (picture == null || picture.Length == 0)
        {
            TempData["Error"] = "Please choose an image to upload.";
            return RedirectToAction(nameof(Index));
        }

        // The image goes to the API, which validates it and stores it in Azure Blob Storage
        var result = await _api.UploadAsync<UserDto>("api/users/me/picture", picture);
        if (result.Unauthorized)
            return this.SessionExpired();

        if (!result.Success)
        {
            TempData["Error"] = result.Error ?? "We couldn't upload that picture. Please try again.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Your profile picture has been updated.";
        return RedirectToAction(nameof(Index));
    }

    // Loads the profile from the API. Returns null if that fails.
    private async Task<ProfilePageViewModel?> LoadPageAsync(UpdateProfileViewModel? form)
    {
        var me = await _api.GetAsync<UserDto>("api/users/me");
        if (!me.Success || me.Data == null)
            return null;

        return new ProfilePageViewModel
        {
            User = me.Data,
            Form = form ?? new UpdateProfileViewModel { Name = me.Data.Name, Email = me.Data.Email }
        };
    }

    private IActionResult LoadFailed()
    {
        // The API clears our session when it says 401, so IsLoggedIn tells us which problem it was
        if (!_session.IsLoggedIn)
            return this.SessionExpired();

        TempData["Error"] = "We couldn't load your profile right now. Please try again.";
        return RedirectToAction("Index", "Home");
    }
}