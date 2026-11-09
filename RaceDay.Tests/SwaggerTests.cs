using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>The Swagger page is how the API is demonstrated, so a broken swagger.json must fail the build.</summary>
public class SwaggerTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public SwaggerTests(RaceDayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SwaggerDocument_IsGenerated_IncludingTheUploadEndpoints()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"swagger.json returned {(int)response.StatusCode}: {body}");
        Assert.Contains("/api/files/banner", body);
        Assert.Contains("/api/files/profile-picture", body);
        Assert.Contains("/api/events", body);
    }
}
