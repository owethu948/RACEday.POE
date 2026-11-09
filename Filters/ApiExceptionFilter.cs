using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Last line of defence for failures that reach the top of an action: the user always gets a friendly page
/// and the details go to the log - never a stack trace or a raw API message.
/// </summary>
public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;
    private readonly IModelMetadataProvider _metadata;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, IModelMetadataProvider metadata)
    {
        _logger = logger;
        _metadata = metadata;
    }

    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case ApiException { StatusCode: 401 }:
                // The API forgot us (session expired or the API restarted): sign out locally and start again.
                context.HttpContext.Session.Clear();
                SessionState.SetMessage(context.HttpContext, "Error", "Your session has expired. Please log in again.");
                context.Result = new RedirectToActionResult("Login", "Account", null);
                break;

            case ApiException { StatusCode: 403 }:
                SessionState.SetMessage(context.HttpContext, "Error", "You do not have access to that.");
                context.Result = new RedirectToActionResult("Index", "Home", null);
                break;

            case ApiException { StatusCode: 404 }:
                context.Result = ErrorView(context, 404, "We couldn't find that page",
                    "It may have been removed, or the link might be wrong.");
                break;

            case ApiException { StatusCode: 503 }:
                _logger.LogWarning(context.Exception, "RaceDay API unavailable");
                context.Result = ErrorView(context, 503, "We're warming up",
                    "We can't reach the RaceDay service right now. Please try again in a minute.");
                break;

            case ApiException ex:
                _logger.LogWarning(ex, "RaceDay API returned {Status}", ex.StatusCode);
                context.Result = ErrorView(context, 502, "Something went wrong",
                    "The RaceDay service couldn't complete that request. Please try again in a moment.");
                break;

            default:
                return;   // genuine bugs fall through to the global exception handler
        }

        context.ExceptionHandled = true;
    }

    private ViewResult ErrorView(ExceptionContext context, int statusCode, string heading, string message)
    {
        var model = new ErrorViewModel { StatusCode = statusCode, Heading = heading, Message = message };
        return new ViewResult
        {
            ViewName = "Error",
            StatusCode = statusCode,
            ViewData = new ViewDataDictionary<ErrorViewModel>(_metadata, context.ModelState) { Model = model }
        };
    }
}
