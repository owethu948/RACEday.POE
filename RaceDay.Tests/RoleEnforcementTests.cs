using System.Net;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>
/// Proves the role rules across the whole API surface: no session = 401, wrong role = 403.
/// The ids in the URLs do not need to exist - authorisation is checked before the action runs.
/// </summary>
public class RoleEnforcementTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public RoleEnforcementTests(RaceDayApiFactory factory) => _factory = factory;

    public static TheoryData<string, string> OrganiserOnlyEndpoints => new()
    {
        { "POST", "/api/events" },
        { "PUT", "/api/events/1" },
        { "DELETE", "/api/events/1" },
        { "POST", "/api/events/1/categories" },
        { "PUT", "/api/categories/1" },
        { "DELETE", "/api/categories/1" },
        { "GET", "/api/events/1/enrolments" },
        { "POST", "/api/enrolments/1/result" },
        { "PUT", "/api/results/1" },
        { "GET", "/api/users/1" }
    };

    public static TheoryData<string, string> ParticipantOnlyEndpoints => new()
    {
        { "POST", "/api/categories/1/enrolments" },
        { "GET", "/api/users/me/enrolments" },
        { "GET", "/api/users/me/results" }
    };

    public static TheoryData<string, string> AnyLoggedInUserEndpoints => new()
    {
        { "GET", "/api/users/me" },
        { "PUT", "/api/users/me" },
        { "POST", "/api/auth/logout" },
        { "DELETE", "/api/enrolments/1" }
    };

    public static TheoryData<string, string> PublicEndpoints => new()
    {
        { "GET", "/api/events" },
        { "GET", "/api/events/1" },
        { "GET", "/api/events/1/categories" },
        { "GET", "/api/events/1/results" }
    };

    // ------------------------------------------------------------ unauthenticated -> 401
    [Theory]
    [MemberData(nameof(OrganiserOnlyEndpoints))]
    [MemberData(nameof(ParticipantOnlyEndpoints))]
    [MemberData(nameof(AnyLoggedInUserEndpoints))]
    public async Task ProtectedEndpoints_WithoutLogin_Return401(string method, string url)
    {
        var response = await TestData.SendAsync(_factory.CreateClient(), method, url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ----------------------------------------------------------- participant -> 403
    [Theory]
    [MemberData(nameof(OrganiserOnlyEndpoints))]
    public async Task OrganiserOnlyEndpoints_RejectParticipants_With403(string method, string url)
    {
        var participant = await TestData.SignInParticipantAsync(_factory);

        var response = await TestData.SendAsync(participant.Client, method, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------- organiser -> 403
    [Theory]
    [MemberData(nameof(ParticipantOnlyEndpoints))]
    public async Task ParticipantOnlyEndpoints_RejectOrganisers_With403(string method, string url)
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);

        var response = await TestData.SendAsync(organiser.Client, method, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -------------------------------------------------------- correct role gets through
    [Theory]
    [MemberData(nameof(AnyLoggedInUserEndpoints))]
    public async Task AnyLoggedInUserEndpoints_AreNotBlockedByRoleRules(string method, string url)
    {
        var participant = await TestData.SignInParticipantAsync(_factory);
        var organiser = await TestData.SignInOrganiserAsync(_factory);

        var asParticipant = await TestData.SendAsync(participant.Client, method, url);
        var asOrganiser = await TestData.SendAsync(organiser.Client, method, url);

        Assert.NotEqual(HttpStatusCode.Unauthorized, asParticipant.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, asParticipant.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, asOrganiser.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, asOrganiser.StatusCode);
    }

    [Theory]
    [MemberData(nameof(PublicEndpoints))]
    public async Task PublicEndpoints_DoNotRequireLogin(string method, string url)
    {
        var response = await TestData.SendAsync(_factory.CreateClient(), method, url);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
