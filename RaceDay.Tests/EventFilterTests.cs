using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Filters and counts added for the Part 3 Organiser dashboard and the participant home page.</summary>
public class EventFilterTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public EventFilterTests(RaceDayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetEvents_ByOrganiserId_ReturnsOnlyThatOrganisersEvents()
    {
        var first = await TestData.SignInOrganiserAsync(_factory);
        var second = await TestData.SignInOrganiserAsync(_factory);
        var mine = await TestData.CreateEventAsync(first.Client);
        await TestData.CreateEventAsync(second.Client);

        var page = await _factory.CreateClient()
            .GetFromJsonAsync<PagedResponse<EventResponse>>($"/api/events?organiserId={first.UserId}");

        Assert.Equal(mine.EventId, page!.Items.Single().EventId);
        Assert.All(page.Items, e => Assert.Equal(first.UserId, e.OrganiserId));
    }

    [Fact]
    public async Task GetEvents_ByEventType_FiltersAndRejectsUnknownTypes()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        await TestData.CreateEventAsync(organiser.Client, TestData.NewEvent(type: "Cycle"));
        await TestData.CreateEventAsync(organiser.Client, TestData.NewEvent(type: "Walk"));
        var client = _factory.CreateClient();

        var cycles = await client.GetFromJsonAsync<PagedResponse<EventResponse>>(
            $"/api/events?eventType=cycle&organiserId={organiser.UserId}");
        Assert.Equal("Cycle", cycles!.Items.Single().EventType);

        var bad = await client.GetAsync("/api/events?eventType=Swim");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task GetEvents_IncludesConfirmedEnrolmentCount_NotCancelledOnes()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var (ev, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);
        var stays = await TestData.SignInParticipantAsync(_factory);
        var leaves = await TestData.SignInParticipantAsync(_factory);
        await TestData.EnrolAsync(stays.Client, category.CategoryId);
        var cancelled = await TestData.EnrolAsync(leaves.Client, category.CategoryId);
        (await leaves.Client.DeleteAsync($"/api/enrolments/{cancelled.EnrolmentId}")).EnsureSuccessStatusCode();

        var page = await _factory.CreateClient()
            .GetFromJsonAsync<PagedResponse<EventResponse>>($"/api/events?organiserId={organiser.UserId}");

        Assert.Equal(1, page!.Items.Single(e => e.EventId == ev.EventId).EnrolmentCount);
    }
}
