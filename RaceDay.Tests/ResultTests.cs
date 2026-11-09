using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Organisers capture finish times and positions; Participants view their own results.</summary>
public class ResultTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public ResultTests(RaceDayApiFactory factory) => _factory = factory;

    private static ResultRequest Finished(int position = 5) => new()
    {
        FinishTime = new TimeSpan(1, 45, 32),
        Position = position,
        Status = ResultStatuses.Finished
    };

    private async Task<(TestUser Organiser, TestUser Participant, EnrolmentResponse Enrolment)> SetUpEnrolmentAsync()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var participant = await TestData.SignInParticipantAsync(_factory);
        var (_, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);
        var enrolment = await TestData.EnrolAsync(participant.Client, category.CategoryId);
        return (organiser, participant, enrolment);
    }

    [Fact]
    public async Task CaptureResult_OwningOrganiser_Returns201WithTimeAndPosition()
    {
        var (organiser, _, enrolment) = await SetUpEnrolmentAsync();

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished(position: 7));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ResultResponse>();
        Assert.Equal(new TimeSpan(1, 45, 32), result!.FinishTime);
        Assert.Equal(7, result.Position);
        Assert.Equal(ResultStatuses.Finished, result.Status);
    }

    [Fact]
    public async Task Participant_CanViewTheirOwnResult()
    {
        var (organiser, participant, enrolment) = await SetUpEnrolmentAsync();
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished(position: 3));

        var results = await participant.Client.GetFromJsonAsync<List<ResultResponse>>("/api/users/me/results");

        var mine = Assert.Single(results!);
        Assert.Equal(3, mine.Position);
        Assert.Equal(participant.UserId, mine.ParticipantId);
    }

    [Fact]
    public async Task Participant_CannotSeeAnotherParticipantsResult()
    {
        var (organiser, _, enrolment) = await SetUpEnrolmentAsync();
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished());
        var other = await TestData.SignInParticipantAsync(_factory);

        var results = await other.Client.GetFromJsonAsync<List<ResultResponse>>("/api/users/me/results");

        Assert.Empty(results!);
    }

    [Fact]
    public async Task CaptureResult_Participant_Returns403()
    {
        var (_, participant, enrolment) = await SetUpEnrolmentAsync();

        var response = await participant.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_AnotherOrganiser_Returns403()
    {
        var (_, _, enrolment) = await SetUpEnrolmentAsync();
        var other = await TestData.SignInOrganiserAsync(_factory);

        var response = await other.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_FinishedWithoutPosition_Returns400()
    {
        var (organiser, _, enrolment) = await SetUpEnrolmentAsync();
        var request = new ResultRequest { FinishTime = new TimeSpan(2, 0, 0), Position = null, Status = ResultStatuses.Finished };

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_DnfNeedsNoTimeOrPosition()
    {
        var (organiser, _, enrolment) = await SetUpEnrolmentAsync();
        var request = new ResultRequest { Status = ResultStatuses.DNF };

        var response = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_Twice_Returns409()
    {
        var (organiser, _, enrolment) = await SetUpEnrolmentAsync();
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished());

        var second = await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_UnknownEnrolment_Returns404()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);

        var response = await organiser.Client.PostAsJsonAsync("/api/enrolments/999999/result", Finished());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateResult_OwningOrganiser_CorrectsTheResult()
    {
        var (organiser, _, enrolment) = await SetUpEnrolmentAsync();
        var created = await (await organiser.Client.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", Finished(position: 9)))
            .Content.ReadFromJsonAsync<ResultResponse>();

        var response = await organiser.Client.PutAsJsonAsync($"/api/results/{created!.ResultId}", Finished(position: 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ResultResponse>();
        Assert.Equal(2, updated!.Position);
    }

    [Fact]
    public async Task GetMyResults_Organiser_Returns403()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);

        var response = await organiser.Client.GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetEventResults_IsPublic_AndOrderedByPosition()
    {
        var organiser = await TestData.SignInOrganiserAsync(_factory);
        var p1 = await TestData.SignInParticipantAsync(_factory);
        var p2 = await TestData.SignInParticipantAsync(_factory);
        var (ev, category) = await TestData.CreateEventWithCategoryAsync(organiser.Client);
        var e1 = await TestData.EnrolAsync(p1.Client, category.CategoryId);
        var e2 = await TestData.EnrolAsync(p2.Client, category.CategoryId);
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{e1.EnrolmentId}/result", Finished(position: 2));
        await organiser.Client.PostAsJsonAsync($"/api/enrolments/{e2.EnrolmentId}/result", Finished(position: 1));

        var response = await _factory.CreateClient().GetAsync($"/api/events/{ev.EventId}/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<ResultResponse>>();
        Assert.Equal(new int?[] { 1, 2 }, results!.Select(r => r.Position));
    }
}
