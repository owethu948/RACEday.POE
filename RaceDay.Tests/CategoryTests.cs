using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Age / distance categories: Organisers define them, both roles can view them.</summary>
public class CategoryTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public CategoryTests(RaceDayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AddCategory_OwningOrganiser_Returns201()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var ev = await TestData.CreateEventAsync(organiser.Client);

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            TestData.NewCategory(maxParticipants: 50, name: "Under 20"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.Equal("Under 20", created!.Name);
        Assert.Equal(ev.EventId, created.EventId);
        Assert.Equal(0, created.EnrolledCount);
    }

    [Fact]
    public async Task GetCategories_Anonymous_ReturnsTheEventsCategories()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var ev = await TestData.CreateEventAsync(organiser.Client);
        await TestData.AddCategoryAsync(organiser.Client, ev.EventId, TestData.NewCategory(name: "10km"));
        await TestData.AddCategoryAsync(organiser.Client, ev.EventId, TestData.NewCategory(name: "21km"));

        var response = await _factory.CreateClient().GetAsync($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();
        Assert.Equal(new[] { "10km", "21km" }, categories!.Select(c => c.Name));
    }

    [Fact]
    public async Task GetCategories_UnknownEvent_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/999999/categories");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_Participant_Returns403()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var ev = await TestData.CreateEventAsync(organiser.Client);

        var response = await participant.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", TestData.NewCategory());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_AnotherOrganisersEvent_Returns403()
    {
        var owner = await TestData.SignInOrganiserAsync(_factory);
        var other = await TestData.SignInOrganiserAsync(_factory);
        var ev = await TestData.CreateEventAsync(owner.Client);

        var response = await other.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", TestData.NewCategory());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_Anonymous_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/events/1/categories", TestData.NewCategory());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_UnknownEvent_Returns404()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);

        var response = await organiser.Client.PostAsJsonAsync("/api/events/999999/categories", TestData.NewCategory());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_InvalidBody_Returns400()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var ev = await TestData.CreateEventAsync(organiser.Client);
        var bad = TestData.NewCategory(maxParticipants: 0);

        var response = await organiser.Client.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", bad);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_Owner_Returns200()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var (_, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);

        var response = await organiser.Client.PutAsJsonAsync($"/api/categories/{category.CategoryId}",
            TestData.NewCategory(maxParticipants: 25, name: "Senior"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.Equal("Senior", updated!.Name);
        Assert.Equal(25, updated.MaxParticipants);
    }

    [Fact]
    public async Task UpdateCategory_AnotherOrganiser_Returns403()
    {
        var owner = await TestData.SignInOrganiserAsync(_factory);
        var other = await TestData.SignInOrganiserAsync(_factory);
        var (_, category) = await TestData.CreateEventWithCategoryAsync(owner.Client);

        var response = await other.Client.PutAsJsonAsync($"/api/categories/{category.CategoryId}", TestData.NewCategory());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_Owner_Returns204()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var (ev, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);

        var response = await organiser.Client.DeleteAsync($"/api/categories/{category.CategoryId}");
        var remaining = await _factory.CreateClient().GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(remaining!);
    }

    [Fact]
    public async Task DeleteCategory_Participant_Returns403()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var (_, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);

        var response = await participant.Client.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
