using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace RaceDay.Web.Services;

public class ApiFallbackOptions
{
    public List<Uri> BaseAddresses { get; set; } = new();
}

public class ApiFallbackHandler : DelegatingHandler
{
    private readonly List<Uri> _baseAddresses;
    private readonly ILogger<ApiFallbackHandler> _logger;

    public ApiFallbackHandler(ApiFallbackOptions options, ILogger<ApiFallbackHandler> logger)
    {
        _baseAddresses = options.BaseAddresses;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_baseAddresses.Count == 0)
        {
            throw new InvalidOperationException("No API base addresses configured.");
        }

        Exception? lastException = null;
        var relativePath = request.RequestUri?.PathAndQuery ?? "/";

        foreach (var baseAddress in _baseAddresses)
        {
            var attemptUri = new Uri(baseAddress, relativePath);
            using var attemptRequest = await CloneHttpRequestMessageAsync(request, attemptUri, cancellationToken);

            try
            {
                _logger.LogInformation("Attempting to connect to API at: {Uri}", attemptUri);

                // If this succeeds, it returns the response and breaks out of the loop immediately.
                var response = await base.SendAsync(attemptRequest, cancellationToken);

                _logger.LogInformation("Successfully connected to API at: {Uri}", attemptUri);
                return response;
            }
            catch (HttpRequestException ex)
            {
                // Pass the exception as the first parameter to fix the "expensive evaluation" warning
                _logger.LogWarning(ex, "Failed to connect to API at {Uri}. Trying next port...", attemptUri);
                lastException = ex;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Timeout connecting to API at {Uri}. Trying next port...", attemptUri);
                lastException = ex;
            }
        }

        // This only throws if ALL ports failed.
        _logger.LogError(lastException, "All configured API URLs failed. The API is completely offline.");
        throw new HttpRequestException($"All configured API URLs failed. Tried: {string.Join(", ", _baseAddresses)}. The API might be offline.", lastException);
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req, Uri newUri, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(req.Method, newUri);

        if (req.Content != null)
        {
            var contentBytes = await req.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in req.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in req.Headers)
        {
            if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
                continue;

            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}

/// <summary>
/// Adds the signed-in user's API session cookie to every outgoing API request, so the API sees the same
/// person (and role) that the browser is signed in as.
/// </summary>
public class ApiSessionHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;

    public ApiSessionHandler(IHttpContextAccessor accessor) => _accessor = accessor;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = _accessor.HttpContext;
        if (context?.Features.Get<ISessionFeature>() is not null)
        {
            // Note: Ensure SessionKeys.ApiCookie exists in your project. 
            // It was referenced in your original file.
            var cookie = context.Session.GetString(SessionKeys.ApiCookie);
            if (!string.IsNullOrEmpty(cookie))
            {
                request.Headers.Remove("Cookie");
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}