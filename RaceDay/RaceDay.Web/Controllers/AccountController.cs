using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : Controller
{
    private readonly ApiClient _api;
    private readonly UserSession _session;

    public AccountController(ApiClient api, UserSession session)
    {
        _api = api;
        _session = session;
    }

    // ---------- Log in ----------

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_session.IsLoggedIn)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        _session.SignOut(); // start clean, in case an old session is lying around

        var result = await _api.PostAsync<UserDto>("api/auth/login", new { email = model.Email, password = model.Password });
        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't log you in. Please try again.");
            return View(model);
        }

        _session.SignIn(result.Data);
        TempData["Success"] = $"Welcome back, {result.Data.Name}!";

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    // ---------- Register ----------

    [HttpGet]
    public IActionResult Register()
    {
        if (_session.IsLoggedIn)
            return RedirectToAction("Index", "Home");

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        _session.SignOut();

        var register = await _api.PostAsync<UserDto>("api/auth/register",
            new { name = model.Name, email = model.Email, password = model.Password, role = model.Role });

        if (!register.Success)
        {
            ModelState.AddModelError(string.Empty, register.Error ?? "We couldn't create your account. Please try again.");
            return View(model);
        }

        // Log the new user straight in
        var login = await _api.PostAsync<UserDto>("api/auth/login", new { email = model.Email, password = model.Password });
        if (!login.Success || login.Data == null)
        {
            TempData["Success"] = "Your account was created. Please log in.";
            return RedirectToAction(nameof(Login));
        }

        _session.SignIn(login.Data);
        TempData["Success"] = $"Welcome to RaceDay, {login.Data.Name}!";
        return RedirectToAction("Index", "Home");
    }

    // ---------- Log out ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        if (_session.IsLoggedIn)
            await _api.PostAsync<object>("api/auth/logout");

        _session.SignOut();
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}