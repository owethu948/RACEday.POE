using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using RaceDay.Api.Data;
using RaceDay.Api.Services;
using RaceDay.Api.Swagger;
using Microsoft.AspNetCore;
using System.Net.Sockets;
/// <summary>Makes the entry point visible to the integration tests (WebApplicationFactory).</summary>

public partial class Program {
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ---------------------------------------------------------------- Database (EF Core, Code-First, SQL Server)
        builder.Services.AddDbContext<RaceDayDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddSingleton<IPasswordService, PasswordService>();

        // ---------------------------------------------------------------- Azure Blob Storage (images)
        builder.Services.Configure<AzureBlobStorageOptions>(builder.Configuration.GetSection(AzureBlobStorageOptions.SectionName));
        builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

        // ---------------------------------------------------------------- Weather (Open-Meteo, no API key needed)
        builder.Services.AddHttpClient<IWeatherService, OpenMeteoWeatherService>(client => client.Timeout = TimeSpan.FromSeconds(10));

        // ---------------------------------------------------------------- Session management
        // The user's id and role are kept in a server-side session; the browser only holds the session cookie.
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.Cookie.Name = ".RaceDay.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.IdleTimeout = TimeSpan.FromMinutes(30);
        });


        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();

        // ---------------------------------------------------------------- Swagger
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "RaceDay API",
                Version = "v1",
                Description =
                    "REST API behind the RaceDay event management system for road running, walking and cycling events.\n\n" +
                    "**How to try it:** 1) `POST /api/auth/register` to create an Organiser or Participant account, " +
                    "2) `POST /api/auth/login` - this starts a server-side session and the browser keeps the session cookie, " +
                    "3) call any endpoint. Protected endpoints answer **401** if you are not logged in and **403** if your role (or ownership) does not permit the action.\n\n" +
                    "Seeded demo accounts (password `Password123!`): `thabo.organiser@raceday.co.za`, `lindiwe.organiser@raceday.co.za`, " +
                    "`sipho.runner@raceday.co.za`, `anke.cyclist@raceday.co.za`."
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            options.OperationFilter<SessionAuthOperationFilter>();
            options.OperationFilter<FileUploadOperationFilter>();

        });

        var app = builder.Build();

        // ---------------------------------------------------------------- Create / update the database and seed demo data
        // Only for SQL Server (the automated tests swap in an in-memory SQLite database and manage it themselves).
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
            if (db.Database.IsSqlServer())
            {
                if (db.Database.GetMigrations().Any())
                {
                    db.Database.Migrate();
                }
                else
                {
                    // No migration generated yet - build the schema straight from the model.
                    db.Database.EnsureCreated();
                }

                // Databases created by Part 2 need the Part 3 image columns (no-op when they already exist).
                await SchemaUpgrades.ApplyAsync(db);

                if (app.Configuration.GetValue<bool>("Database:SeedDemoData"))
                {
                    var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
                    await DbSeeder.SeedAsync(db, passwords);
                }
            }
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler();
        }

        // Default (OpenAPI 3) document. Do NOT force OpenApi2_0 here: the Swagger 2.0 writer cannot describe the
        // multipart image-upload endpoints (/api/files/*) and answers 500 for swagger.json.
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
            options.DocumentTitle = "RaceDay API";
        });

        app.UseSession();
        app.MapControllers();

        app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

        app.Run();
    }
}

/// <summary>Makes the entry point visible to the integration tests (WebApplicationFactory).</summary>
public partial class Program { }