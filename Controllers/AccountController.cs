using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : AppController
{
    private readonly IRaceDayApiClient _api;
    private readonly ISessionUser _user;

    public AccountController(IRaceDayApiClient api, ISessionUser user)
    {
        _api = api;
        _user = user;
    }

    // ------------------------------------------------------------------ login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_user.IsAuthenticated) return RedirectToHome(_user.Role);
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        // Never carry an old API session into a new login.
        HttpContext.Session.Remove(SessionKeys.ApiCookie);

        try
        {
            var result = await _api.LoginAsync(model.Email.Trim(), model.Password, ct);
            SessionWriter.SignIn(HttpContext.Session, result);

            SetSuccess($"Welcome back, {FirstName(result.User.FullName)}!");
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return LocalRedirect(model.ReturnUrl);

            return RedirectToHome(result.User.Role);
        }
        catch (ApiException ex) when (ex.StatusCode is 400 or 401)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Password = string.Empty;
            return View(model);
        }
    }

    // ------------------------------------------------------------------ register
    [HttpGet]
    public IActionResult Register()
    {
        if (_user.IsAuthenticated) return RedirectToHome(_user.Role);
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            await _api.RegisterAsync(new RegisterPayload
            {
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                Password = model.Password,
                Role = model.Role,
                OrganiserCode = string.IsNullOrWhiteSpace(model.OrganiserCode) ? null : model.OrganiserCode.Trim()
            }, ct);

            // Registering also signs the new user in, so they can start straight away.
            HttpContext.Session.Remove(SessionKeys.ApiCookie);
            var login = await _api.LoginAsync(model.Email.Trim(), model.Password, ct);
            SessionWriter.SignIn(HttpContext.Session, login);

            SetSuccess($"Welcome to RaceDay, {FirstName(login.User.FullName)}!");
            return RedirectToHome(login.User.Role);
        }
        catch (ApiException ex) when (!ex.IsAuthFailure || ex.StatusCode == 403)
        {
            // 400 validation, 403 wrong organiser code, 409 email already used ...
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Password = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View(model);
        }
    }

    // ------------------------------------------------------------------ logout
    [HttpPost]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        try
        {
            await _api.LogoutAsync(ct);
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
        {
            // The API session may already be gone; we still end the local one below.
        }

        HttpContext.Session.Clear();
        SetSuccess("You have been logged out. See you at the start line!");
        return RedirectToAction("Index", "Home");
    }

    // ------------------------------------------------------------------ profile
    [HttpGet, RequireLogin]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var profile = await _api.GetProfileAsync(ct);
        return View(ToViewModel(profile));
    }

    [HttpPost, RequireLogin]
    public async Task<IActionResult> Profile(ProfileViewModel model, CancellationToken ct)
    {
        // Email, role and the current picture always come from the API, never from posted form fields.
        var current = await _api.GetProfileAsync(ct);
        model.Email = current.Email;
        model.Role = current.Role;
        model.ProfilePictureUrl = current.ProfilePictureUrl;

        if (model.DateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth can't be in the future.");

        var uploadProblem = ImageUploadValidator.Validate(model.NewPicture);
        if (uploadProblem is not null)
            ModelState.AddModelError(nameof(model.NewPicture), uploadProblem);

        if (!ModelState.IsValid) return View(model);

        try
        {
            var pictureUrl = model.RemovePicture ? null : current.ProfilePictureUrl;
            if (model.NewPicture is { Length: > 0 })
                pictureUrl = await _api.UploadImageAsync(model.NewPicture, ImageKind.ProfilePicture, ct);

            var saved = await _api.UpdateProfileAsync(new UpdateProfilePayload
            {
                FullName = model.FullName.Trim(),
                PhoneNumber = Clean(model.PhoneNumber),
                DateOfBirth = model.DateOfBirth,
                EmergencyContact = Clean(model.EmergencyContact),
                ProfilePictureUrl = pictureUrl
            }, ct);

            SessionWriter.SetDisplayName(HttpContext.Session, saved.FullName);
            SetSuccess("Your profile has been updated.");
            return RedirectToAction(nameof(Profile));
        }
        catch (ApiException ex) when (!ex.IsAuthFailure)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    private static ProfileViewModel ToViewModel(ProfileDto p) => new()
    {
        Email = p.Email,
        Role = p.Role,
        FullName = p.FullName,
        PhoneNumber = p.PhoneNumber,
        DateOfBirth = p.DateOfBirth,
        EmergencyContact = p.EmergencyContact,
        ProfilePictureUrl = p.ProfilePictureUrl
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FirstName(string fullName) =>
        fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? fullName;
}
