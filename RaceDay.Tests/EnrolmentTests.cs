using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Participants entering events by choosing a category, and Organisers viewing the entries.</summary>
public class EnrolmentTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public EnrolmentTests(RaceDayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Enrol_Participant_Returns201AndLinksParticipantEventAndCategory()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var (ev, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);

        var response = await participant.Client.PostAsync($"/api/categories/{category.CategoryId}/enrolments", content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await response.Content.ReadFromJsonAsync<EnrolmentResponse>();
        Assert.Equal(participant.UserId, enrolment!.ParticipantId);
        Assert.Equal(category.CategoryId, enrolment.CategoryId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal(EnrolmentStatuses.Confirmed, enrolment.Status);
    }

    [Fact]
    public async Task Enrol_IsRecordedInTheDatabase()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var (_, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);

        var enrolment = await TestData.EnrolAsync(participant.Client, category.CategoryId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var row = await db.Enrolments.SingleAsync(e => e.EnrolmentId == enrolment.EnrolmentId);
        Assert.Equal(participant.UserId, row.ParticipantId);
        Assert.Equal(category.CategoryId, row.CategoryId);
        Assert.Equal(EnrolmentStatuses.Confirmed, row.Status);
    }

    [Fact]
    public async Task Enrol_AppearsInTheParticipantsOwnEnrolmentList()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var (ev, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);
        await TestData.EnrolAsync(participant.Client, category.CategoryId);

        var mine = await participant.Client.GetFromJsonAsync<List<EnrolmentResponse>>("/api/users/me/enrolments");

        var item = Assert.Single(mine!);
        Assert.Equal(ev.EventId, item.EventId);
        Assert.Equal(category.CategoryId, item.CategoryId);
    }

    [Fact]
    public async Task Enrol_IncreasesTheCategorysEnrolledCount()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var (ev, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);
        await TestData.EnrolAsync(participant.Client, category.CategoryId);

        var categories = await _factory.CreateClient().GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{ev.EventId}/categories");

        Assert.Equal(1, categories!.Single().EnrolledCount);
    }

    [Fact]
    public async Task Enrol_SameCategoryTwice_Returns409()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var (_, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);
        await TestData.EnrolAsync(participant.Client, category.CategoryId);

        var second = await participant.Client.PostAsync($"/api/categories/{category.CategoryId}/enrolments", content: null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_FullCategory_Returns409()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var first = await TestData.SignInParticipantAsync(_factory);
        var second = await TestData.SignInParticipantAsync(_factory);
        var (_, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client, maxParticipants: 1);
        await TestData.EnrolAsync(first.Client, category.CategoryId);

        var response = await second.Client.PostAsync($"/api/categories/{category.CategoryId}/enrolments", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_EventInThePast_Returns400()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var ev = await TestData.CreateEventAsync(organiser.Client, TestData.NewEvent(date: DateTime.Now.AddDays(-10)));
        var category = await TestData.AddCategoryAsync(organiser.Client, ev.EventId);

        var response = await participant.Client.PostAsync($"/api/categories/{category.CategoryId}/enrolments", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownCategory_Returns404()
    {
        var participant = await TestData.SignInParticipantAsync(_factory);

        var response = await participant.Client.PostAsync("/api/categories/999999/enrolments", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_Anonymous_Returns401()
    {
        var response = await _factory.CreateClient().PostAsync("/api/categories/1/enrolments", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_Organiser_Returns403()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var (_, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);

        var response = await organiser.Client.PostAsync($"/api/categories/{category.CategoryId}/enrolments", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------- organiser views
    [Fact]
    public async Task GetEventEnrolments_OwningOrganiser_SeesEveryEntrant()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var p1 = await TestData.SignInParticipantAsync(_factory);
        var p2 = await TestData.SignInParticipantAsync(_factory);
        var (ev, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);
        await TestData.EnrolAsync(p1.Client, category.CategoryId);
        await TestData.EnrolAsync(p2.Client, category.CategoryId);

        var response = await organiser.Client.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<EnrolmentResponse>>();
        Assert.Equal(new[] { p1.UserId, p2.UserId }.OrderBy(x => x), list!.Select(e => e.ParticipantId).OrderBy(x => x));
    }

    [Fact]
    public async Task GetEventEnrolments_AnotherOrganiser_Returns403()
    {
        var owner = await TestData.SignInOrganiserAsync(_factory);
        var other = await TestData.SignInOrganiserAsync(_factory);
        var (ev, _) = await TestData.CreateEventWithCategoryAsync(owner.Client);

        var response = await other.Client.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetEventEnrolments_Participant_Returns403()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var (ev, _) = await TestData.CreateEventWithCategoryAsync(organiser.Client);

        var response = await participant.Client.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMyEnrolments_OnlyReturnsTheLoggedInParticipantsEntries()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var mine = await TestData.SignInParticipantAsync(_factory);
        var theirs = await TestData.SignInParticipantAsync(_factory);
        var (_, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);
        await TestData.EnrolAsync(theirs.Client, category.CategoryId);

        var list = await mine.Client.GetFromJsonAsync<List<EnrolmentResponse>>("/api/users/me/enrolments");

        Assert.Empty(list!);
    }

    // ----------------------------------------------------- cancelling
    [Fact]
    public async Task Cancel_OwnEnrolment_Returns204_AndCanReEnrolAfterwards()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var (_, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);
        var enrolment = await TestData.EnrolAsync(participant.Client, category.CategoryId);

        var cancel = await participant.Client.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");
        var reEnrol = await participant.Client.PostAsync($"/api/categories/{category.CategoryId}/enrolments", content: null);

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Created, reEnrol.StatusCode);
    }

    [Fact]
    public async Task Cancel_SomeoneElsesEnrolment_Returns403()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var owner = await TestData.SignInParticipantAsync(_factory);
        var stranger = await TestData.SignInParticipantAsync(_factory);
        var (_, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);
        var enrolment = await TestData.EnrolAsync(owner.Client, category.CategoryId);

        var response = await stranger.Client.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_ByTheEventsOrganiser_Returns204()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var (_, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);
        var enrolment = await TestData.EnrolAsync(participant.Client, category.CategoryId);

        var response = await organiser.Client.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
