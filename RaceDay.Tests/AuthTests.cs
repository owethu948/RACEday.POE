using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>User registration, login, logout, session behaviour and profile access.</summary>
public class AuthTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public AuthTests(RaceDayApiFactory factory) => _factory = factory;

    // ------------------------------------------------------------ registration
    [Fact]
    public async Task Register_Participant_Returns201WithRoleAndNoPassword()
    {
        var client = _factory.CreateClient();

        var response = await TestData.RegisterAsync(client, Roles.Participant);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(Roles.Participant, auth!.Role);
        Assert.True(auth.UserId > 0);
    }

    [Fact]
    public async Task Register_Organiser_WithValidCode_Returns201()
    {
        var client = _factory.CreateClient();

        var response = await TestData.RegisterAsync(client, Roles.Organiser, organiserCode: RaceDayApiFactory.OrganiserCode);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(Roles.Organiser, auth!.Role);
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_Returns403()
    {
        var response = await TestData.RegisterAsync(_factory.CreateClient(), Roles.Organiser);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithWrongCode_Returns403()
    {
        var response = await TestData.RegisterAsync(_factory.CreateClient(), Roles.Organiser, organiserCode: "NOT-THE-CODE");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409_EvenWhenCaseDiffers()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail("dup");
        (await TestData.RegisterAsync(client, Roles.Participant, email)).EnsureSuccessStatusCode();

        var second = await TestData.RegisterAsync(client, Roles.Participant, email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_UnknownRole_Returns400()
    {
        var response = await TestData.RegisterAsync(_factory.CreateClient(), "Admin");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordTooShort_Returns400()
    {
        var response = await TestData.RegisterAsync(_factory.CreateClient(), Roles.Participant, password: "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var response = await TestData.RegisterAsync(_factory.CreateClient(), Roles.Participant, email: "not-an-email");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresOnlyAHashOfThePassword()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail("hash");
        (await TestData.RegisterAsync(client, Roles.Participant, email, password: "MySecret#2026")).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);

        Assert.NotEqual("MySecret#2026", user.PasswordHash);
        Assert.DoesNotContain("MySecret#2026", user.PasswordHash);
        Assert.StartsWith("PBKDF2-SHA256$", user.PasswordHash);
    }

    // ------------------------------------------------------------------- login
    [Theory]
    [InlineData(Roles.Participant)]
    [InlineData(Roles.Organiser)]
    public async Task Login_ValidCredentials_Returns200WithTheUsersRole(string role)
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail("login");
        var code = role == Roles.Organiser ? RaceDayApiFactory.OrganiserCode : null;
        (await TestData.RegisterAsync(client, role, email, organiserCode: code)).EnsureSuccessStatusCode();

        var response = await TestData.LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(role, auth!.Role);
        Assert.Equal(email, auth.Email);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail("wrongpw");
        (await TestData.RegisterAsync(client, Roles.Participant, email)).EnsureSuccessStatusCode();

        var response = await TestData.LoginAsync(client, email, "TotallyWrong1!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await TestData.LoginAsync(_factory.CreateClient(), TestData.UniqueEmail("ghost"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_FailedAttempt_DoesNotCreateASession()
    {
        var client = _factory.CreateClient();
        var email = TestData.UniqueEmail("nosession");
        (await TestData.RegisterAsync(client, Roles.Participant, email)).EnsureSuccessStatusCode();
        await TestData.LoginAsync(client, email, "TotallyWrong1!");

        var me = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    // ----------------------------------------------------------------- session
    [Fact]
    public async Task ProtectedEndpoint_WithoutLogin_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_AfterLogin_UsesTheSessionToIdentifyTheUser()
    {
        var user = await TestData.SignInParticipantAsync(_factory);

        var response = await user.Client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.Equal(user.UserId, profile!.UserId);
        Assert.Equal(Roles.Participant, profile.Role);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var user = await TestData.SignInParticipantAsync(_factory);

        var logout = await user.Client.PostAsync("/api/auth/logout", content: null);
        var afterwards = await user.Client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutLogin_Returns401()
    {
        var response = await _factory.CreateClient().PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TwoUsers_HaveSeparateSessions()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);

        var organiserMe = await (await organiser.Client.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<ProfileResponse>();
        var participantMe = await (await participant.Client.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<ProfileResponse>();

        Assert.Equal(Roles.Organiser, organiserMe!.Role);
        Assert.Equal(Roles.Participant, participantMe!.Role);
        Assert.NotEqual(organiserMe.UserId, participantMe.UserId);
    }

    // ----------------------------------------------------------------- profile
    [Theory]
    [InlineData(Roles.Participant)]
    [InlineData(Roles.Organiser)]
    public async Task Profile_BothRoles_CanViewAndUpdateTheirOwnProfile(string role)
    {
        var user = await TestData.SignInAsync(_factory, role);
        var update = new UpdateProfileRequest
        {
            FullName = "Updated Name",
            PhoneNumber = "0821234567",
            DateOfBirth = new DateOnly(1995, 5, 5),
            EmergencyContact = "Mom - 0827654321"
        };

        var put = await user.Client.PutAsJsonAsync("/api/users/me", update);
        var get = await user.Client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var profile = await get.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.Equal("Updated Name", profile!.FullName);
        Assert.Equal("0821234567", profile.PhoneNumber);
        Assert.Equal(new DateOnly(1995, 5, 5), profile.DateOfBirth);
        Assert.Equal(role, profile.Role);
    }

    [Fact]
    public async Task Profile_Update_WithoutFullName_Returns400()
    {
        var user = await TestData.SignInParticipantAsync(_factory);

        var response = await user.Client.PutAsJsonAsync("/api/users/me", new UpdateProfileRequest { FullName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Profile_Update_WithoutLogin_Returns401()
    {
        var response = await _factory.CreateClient().PutAsJsonAsync("/api/users/me", new UpdateProfileRequest { FullName = "Hacker" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
