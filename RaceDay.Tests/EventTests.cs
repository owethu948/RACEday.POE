using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Event management: Organisers create/update/delete; everyone can view.</summary>
public class EventTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public EventTests(RaceDayApiFactory factory) => _factory = factory;

    // -------------------------------------------------------------- viewing
    [Fact]
    public async Task GetEvents_Anonymous_Returns200()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_Participant_CanViewEvents()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var ev = await TestData.CreateEventAsync(organiser.Client);
        var participant = await TestData.SignInParticipantAsync(_factory);

        var response = await participant.Client.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<EventDetailResponse>();
        Assert.Equal(ev.Name, detail!.Name);
    }

    [Fact]
    public async Task GetEvent_UnknownId_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_SearchFiltersByName()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var token = Guid.NewGuid().ToString("N");
        await TestData.CreateEventAsync(organiser.Client, TestData.NewEvent(name: $"Findme {token}"));
        await TestData.CreateEventAsync(organiser.Client, TestData.NewEvent(name: "Something else entirely"));

        var response = await _factory.CreateClient().GetAsync($"/api/events?search={token}");
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<EventResponse>>();

        Assert.Equal(1, page!.TotalCount);
        Assert.Contains(token, page.Items.Single().Name);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task GetEvents_InvalidPaging_Returns400(string query)
    {
        var response = await _factory.CreateClient().GetAsync($"/api/events?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------- creating
    [Theory]
    [InlineData("Run")]
    [InlineData("Walk")]
    [InlineData("Cycle")]
    public async Task CreateEvent_Organiser_Returns201_ForEveryEventType(string eventType)
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);

        var response = await organiser.Client.PostAsJsonAsync("/api/events", TestData.NewEvent(type: eventType));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Equal(eventType, created!.EventType);
        Assert.Equal(organiser.UserId, created.OrganiserId);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task CreateEvent_IsPersisted_AndCanBeReadBack()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var request = TestData.NewEvent(name: "Persisted Event");

        var created = await TestData.CreateEventAsync(organiser.Client, request);
        var fetched = await _factory.CreateClient().GetFromJsonAsync<EventDetailResponse>($"/api/events/{created.EventId}");

        Assert.Equal("Persisted Event", fetched!.Name);
        Assert.Equal(request.Location, fetched.Location);
        Assert.Equal(request.DistanceKm, fetched.DistanceKm);
        Assert.Equal(request.Description, fetched.Description);
    }

    [Fact]
    public async Task CreateEvent_Anonymous_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/events", TestData.NewEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_Participant_Returns403()
    {
        var participant = await TestData.SignInParticipantAsync(_factory);

        var response = await participant.Client.PostAsJsonAsync("/api/events", TestData.NewEvent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_InvalidEventType_Returns400()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);

        var response = await organiser.Client.PostAsJsonAsync("/api/events", TestData.NewEvent(type: "Swim"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_MissingName_Returns400()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var request = TestData.NewEvent();
        request.Name = "";

        var response = await organiser.Client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_ZeroDistance_Returns400()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var request = TestData.NewEvent();
        request.DistanceKm = 0m;

        var response = await organiser.Client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------- updating
    [Fact]
    public async Task UpdateEvent_Owner_Returns200AndChangesAreSaved()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var ev = await TestData.CreateEventAsync(organiser.Client);
        var changes = TestData.NewEvent(name: "Renamed Event", type: "Cycle");
        changes.DistanceKm = 56m;

        var response = await organiser.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", changes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await _factory.CreateClient().GetFromJsonAsync<EventDetailResponse>($"/api/events/{ev.EventId}");
        Assert.Equal("Renamed Event", fetched!.Name);
        Assert.Equal("Cycle", fetched.EventType);
        Assert.Equal(56m, fetched.DistanceKm);
    }

    [Fact]
    public async Task UpdateEvent_AnotherOrganiser_Returns403()
    {
        var owner = await TestData.SignInOrganiserAsync(_factory);
        var other = await TestData.SignInOrganiserAsync(_factory);
        var ev = await TestData.CreateEventAsync(owner.Client);

        var response = await other.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", TestData.NewEvent(name: "Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var fetched = await _factory.CreateClient().GetFromJsonAsync<EventDetailResponse>($"/api/events/{ev.EventId}");
        Assert.Equal(ev.Name, fetched!.Name);
    }

    [Fact]
    public async Task UpdateEvent_Participant_Returns403()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var ev = await TestData.CreateEventAsync(organiser.Client);

        var response = await participant.Client.PutAsJsonAsync($"/api/events/{ev.EventId}", TestData.NewEvent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_UnknownId_Returns404()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);

        var response = await organiser.Client.PutAsJsonAsync("/api/events/999999", TestData.NewEvent());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------- deleting
    [Fact]
    public async Task DeleteEvent_Owner_Returns204_AndTheEventIsGone()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var ev = await TestData.CreateEventAsync(organiser.Client);

        var delete = await organiser.Client.DeleteAsync($"/api/events/{ev.EventId}");
        var afterwards = await _factory.CreateClient().GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AnotherOrganiser_Returns403_AndTheEventSurvives()
    {
        var owner = await TestData.SignInOrganiserAsync(_factory);
        var other = await TestData.SignInOrganiserAsync(_factory);
        var ev = await TestData.CreateEventAsync(owner.Client);

        var response = await other.Client.DeleteAsync($"/api/events/{ev.EventId}");
        var stillThere = await _factory.CreateClient().GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_Participant_Returns403()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var ev = await TestData.CreateEventAsync(organiser.Client);

        var response = await participant.Client.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_Anonymous_Returns401()
    {
        var response = await _factory.CreateClient().DeleteAsync("/api/events/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_RemovesItsCategories()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var (ev, _) = await TestData.CreateEventWithCategoryAsync(organiser.Client);

        await organiser.Client.DeleteAsync($"/api/events/{ev.EventId}");
        var categories = await _factory.CreateClient().GetAsync($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.NotFound, categories.StatusCode);
    }
}
