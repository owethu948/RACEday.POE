using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Base class: flash messages and the role-aware "go home" redirect.</summary>
public abstract class AppController : Controller
{
    protected void SetSuccess(string message) => TempData["Success"] = message;
    protected void SetError(string message) => TempData["Error"] = message;

    /// <summary>Organisers land on their dashboard, everyone else on the public home page.</summary>
    protected IActionResult RedirectToHome(string? role) =>
        string.Equals(role, Roles.Organiser, StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");
}
