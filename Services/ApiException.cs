using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RaceDay.Web.Services;

/// <summary>An error response from the RaceDay API, reduced to a status code and a message safe to show.</summary>
public class ApiException : Exception
{
    public ApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public ApiException(int statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    /// <summary>401 / 403: the user must sign in again, or may not do this. Handled globally.</summary>
    public bool IsAuthFailure => StatusCode is 401 or 403;

    internal static async Task<ApiException> FromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        string? message = null;

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(RaceDayApiClient.JsonOptions, cancellationToken);
            if (problem is not null)
            {
                message = !string.IsNullOrWhiteSpace(problem.Detail)
                    ? problem.Detail
                    : problem.Errors is { Count: > 0 }
                        ? string.Join(" ", problem.Errors.Values.SelectMany(v => v))
                        : problem.Title;
            }
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException or InvalidOperationException)
        {
            // Body was empty or not JSON - fall through to the generic message.
        }

        message = string.IsNullOrWhiteSpace(message) ? DefaultMessage(status) : message;
        return new ApiException(status, message);
    }

    private static string DefaultMessage(int status) => status switch
    {
        400 => "Some of the information was not valid. Please check it and try again.",
        401 => "Please log in to continue.",
        403 => "You do not have permission to do that.",
        404 => "We could not find what you were looking for.",
        409 => "That conflicts with existing information.",
        413 => "That file is too large.",
        503 => "A service we depend on is temporarily unavailable. Please try again shortly.",
        _ => "Something went wrong while talking to the RaceDay service. Please try again."
    };

    // Shape of ASP.NET Core ProblemDetails / ValidationProblemDetails.
    private sealed class ProblemBody
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }

        [JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; set; }
    }
}
