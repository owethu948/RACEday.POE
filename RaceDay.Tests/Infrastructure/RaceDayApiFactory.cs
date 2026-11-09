using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Tests.Infrastructure;

/// <summary>
/// Boots the real RaceDay API in memory for each test class. SQL Server is swapped for an
/// in-memory SQLite database (same EF Core model), so tests are fast, isolated and need no
/// installed database - which also lets them run unchanged on GitHub Actions.
/// </summary>
public class RaceDayApiFactory : WebApplicationFactory<Program>
{
    public const string OrganiserCode = "TEST-ORGANISER-CODE";

    // An in-memory SQLite database lives only as long as its connection, so keep one open.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:OrganiserRegistrationCode"] = OrganiserCode,
                ["Database:SeedDemoData"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove every registration tied to RaceDayDbContext's options (this also removes the
            // SQL Server configuration that AddDbContext registered in Program.cs).
            var toRemove = services
                .Where(d => d.ServiceType.IsGenericType &&
                            d.ServiceType.GenericTypeArguments.Contains(typeof(RaceDayDbContext)))
                .ToList();
            foreach (var descriptor in toRemove) services.Remove(descriptor);

            services.AddDbContext<RaceDayDbContext>(options => options.UseSqlite(_connection));

            // Never talk to real Azure from a test: swap in an in-memory fake that records what was uploaded.
            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService, FakeBlobStorageService>();

            // ...and never call the real weather provider either.
            services.RemoveAll<IWeatherService>();
            services.AddSingleton<IWeatherService, FakeWeatherService>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
