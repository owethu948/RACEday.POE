using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Web.Filters;
using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------------ MVC
builder.Services.AddControllersWithViews(options =>
{
    // Turns API failures (expired session, 403, 404, API offline) into friendly pages instead of stack traces.
    options.Filters.Add<ApiExceptionFilter>();
    // Every POST must carry the anti-forgery token the forms already include.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // Optional form fields (e.g. view-model properties that are not posted back) must not become "required".
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

// ------------------------------------------------------------------ Session (user id, role, display name)
// The web app keeps its own server-side session. It also stores the API's session cookie so each
// browser user is "logged in" to the API as themselves (see ApiSessionHandler).
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Web.Session";   // different name from the API's ".RaceDay.Session"
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ISessionUser, SessionUser>();

// ------------------------------------------------------------------ Typed HttpClient for the RaceDay API
// Supports multiple API base URLs so the client can fall back to an alternate endpoint (e.g. a
// secondary container, an HTTP fallback when HTTPS is misconfigured, or a local dev instance).
var apiBaseUrls = builder.Configuration.GetSection("ApiBaseUrls").Get<string[]>();

if (apiBaseUrls == null || apiBaseUrls.Length == 0)
{
    // Fallback to single URL for backward compatibility
    var singleUrl = builder.Configuration["ApiBaseUrl"];
    if (!string.IsNullOrWhiteSpace(singleUrl))
    {
        apiBaseUrls = new[] { singleUrl };
    }
    else
    {
        throw new InvalidOperationException(
            "Configuration value 'ApiBaseUrls' (array) or 'ApiBaseUrl' (string) is missing. " +
            "Example: \"ApiBaseUrls\": [\"https://localhost:7271/\", \"http://localhost:5146/\"]. " +
            "(Docker: -e ApiBaseUrl=http://host.docker.internal:5146/).");
    }
}

var apiBaseUris = apiBaseUrls
    .Where(url => !string.IsNullOrWhiteSpace(url))
    .Select(url => new Uri(url.TrimEnd('/') + "/"))
    .ToList();

if (apiBaseUris.Count == 0)
{
    throw new InvalidOperationException("Configuration value 'ApiBaseUrls' does not contain any valid URLs.");
}

builder.Services.AddTransient<ApiSessionHandler>();
builder.Services.AddSingleton(new ApiFallbackOptions { BaseAddresses = apiBaseUris });
builder.Services.AddTransient<ApiFallbackHandler>();

builder.Services
    .AddHttpClient<IRaceDayApiClient, RaceDayApiClient>(client =>
    {
        // Set the primary address. The fallback handler will try the others if this fails.
        client.BaseAddress = apiBaseUris[0];
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    // UseCookies = false is essential: the handler is pooled and shared by all users, so a built-in cookie
    // container would leak one user's API session to another. We attach each user's cookie ourselves.
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        // Bypass SSL validation in local development to avoid issues with self-signed dev certificates
        if (builder.Environment.IsDevelopment())
        {
            handler.SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
            };
        }

        return handler;
    })
    .AddHttpMessageHandler<ApiSessionHandler>()
    .AddHttpMessageHandler<ApiFallbackHandler>(); // Add the fallback handler at the end

var app = builder.Build();

// One culture everywhere so "21.1" posted by a browser binds the same on a Windows en-ZA PC (decimal
// comma) and inside a Linux container.
var culture = CultureInfo.InvariantCulture;
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture]
});

// Keep the friendly application error flow enabled in Development too. The API client maps
// connection failures to ApiException(503), while this middleware handles any other unhandled
// application exception without exposing a stack trace to the browser.
app.UseExceptionHandler("/Home/Error");

app.UseStatusCodePagesWithReExecute("/Home/HttpStatus", "?code={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseSession();          // must come before anything that reads the session (filters, controllers)

// Tiny endpoint for container health checks and the E2E tests' "wait until ready" loop.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();

app.MapControllers();      // attribute-routed controllers (Organiser/...)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>Makes the entry point visible to the integration tests (WebApplicationFactory).</summary>
public partial class Program { }