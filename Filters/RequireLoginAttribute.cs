using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Blocks anonymous visitors from a page. Role is read from the web session (not from the API) and the
/// visitor is sent to the login page, then returned to where they were heading.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireLoginAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!SessionState.IsSignedIn(context.HttpContext))
        {
            var request = context.HttpContext.Request;
            var returnUrl = HttpMethods.IsGet(request.Method) ? request.Path + request.QueryString : null;

            SessionState.SetMessage(context.HttpContext, "Error", "Please log in to continue.");
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
        }
    }
}

/// <summary>
/// Restricts a controller or action to one role: "Organiser" pages are invisible to Participants and vice
/// versa. Anonymous visitors go to login; signed-in users with the wrong role go home with a message.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : RequireLoginAttribute
{
    private readonly string _requiredRole;

    public RequireRoleAttribute(string role) => _requiredRole = role;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);
        if (context.Result is not null) return;   // already redirected to login

        var role = context.HttpContext.Session.GetString(SessionKeys.Role);
        if (!string.Equals(role, _requiredRole, StringComparison.OrdinalIgnoreCase))
        {
            SessionState.SetMessage(context.HttpContext, "Error", "You do not have access to that page.");
            context.Result = new RedirectToActionResult("Index", "Home", null);
        }
    }
}

/// <summary>Small helpers for filters, which cannot use a controller's TempData property.</summary>
internal static class SessionState
{
    public static bool IsSignedIn(HttpContext context) =>
        context.Session.GetInt32(SessionKeys.UserId) is > 0 &&
        !string.IsNullOrEmpty(context.Session.GetString(SessionKeys.Role));

    public static void SetMessage(HttpContext context, string key, string message)
    {
        var factory = context.RequestServices.GetService<ITempDataDictionaryFactory>();
        if (factory is null) return;

        var tempData = factory.GetTempData(context);
        tempData[key] = message;
        tempData.Save();
    }
}
