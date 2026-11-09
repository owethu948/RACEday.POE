using Microsoft.OpenApi;
using RaceDay.Api.Auth;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RaceDay.Api.Swagger;

/// <summary>
/// Adds an "Access" line to every endpoint's description in the Swagger UI (public / any logged-in
/// user / a specific role) and documents the 401 and 403 responses of protected endpoints.
/// </summary>
public class SessionAuthOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attributes = context.MethodInfo.GetCustomAttributes(true).OfType<SessionAuthorizeAttribute>().ToList();
        if (context.MethodInfo.DeclaringType != null)
        {
            attributes.AddRange(context.MethodInfo.DeclaringType.GetCustomAttributes(true).OfType<SessionAuthorizeAttribute>());
        }

        string access;
        if (attributes.Count == 0)
        {
            access = "**Access:** Public - no login required.";
        }
        else
        {
            var roles = attributes.Select(a => a.Roles).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
            access = roles is null
                ? "**Access:** Any logged-in user (Organiser or Participant). Log in first via `POST /api/auth/login`."
                : $"**Access:** {roles} only. Log in first via `POST /api/auth/login`.";

            operation.Responses ??= new OpenApiResponses();
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Not logged in (no valid session)." });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Logged in, but your role or ownership does not permit this action." });
        }

        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? access
            : access + "\n\n" + operation.Description;
    }
}
