using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

public class ResultTests : ApiTestBase
{
    [Fact]
    public async Task Organiser_CanCaptureAResult()
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();
        var enrolment = await EnrolAsync(participant, ev.EventId, category.CategoryId);

        var response = await CaptureResultAsync(organiser, enrolment.EnrolmentId, "01:47:32", 47);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ResultResponse>(Json);
        Assert.Equal(new TimeSpan(1, 47, 32), body!.FinishTime);
        Assert.Equal(47, body.FinishPosition);
    }

    [Fact]
    public async Task CapturingAResultTwice_Returns409()
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();
        var enrolment = await EnrolAsync(participant, ev.EventId, category.CategoryId);
        await CaptureResultAsync(organiser, enrolment.EnrolmentId, "01:47:32", 47);

        var response = await CaptureResultAsync(organiser, enrolment.EnrolmentId, "01:50:00", 50);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CapturingAResult_WithAnInvalidPosition_Returns400()
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();
        var enrolment = await EnrolAsync(participant, ev.EventId, category.CategoryId);

        var response = await CaptureResultAsync(organiser, enrolment.EnrolmentId, "01:47:32", 0);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CapturingAResult_ForAnEnrolmentThatDoesNotExist_Returns404()
    {
        var organiser = await OrganiserAsync();

        var response = await CaptureResultAsync(organiser, 999, "01:47:32", 47);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCaptureAResultForAnotherOrganisersEvent_Returns403()
    {
        var (_, ev, category) = await SetUpRaceAsync("owner@test.com");
        var participant = await ParticipantAsync();
        var enrolment = await EnrolAsync(participant, ev.EventId, category.CategoryId);
        var otherOrganiser = await OrganiserAsync("other@test.com");

        var response = await CaptureResultAsync(otherOrganiser, enrolment.EnrolmentId, "01:47:32", 47);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCorrectAResult()
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();
        var enrolment = await EnrolAsync(participant, ev.EventId, category.CategoryId);
        await CaptureResultAsync(organiser, enrolment.EnrolmentId, "01:47:32", 47);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "01:45:10", finishPosition = 41 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ResultResponse>(Json);
        Assert.Equal(41, body!.FinishPosition);
    }

    [Fact]
    public async Task CorrectingAResult_ThatWasNeverCaptured_Returns404()
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();
        var enrolment = await EnrolAsync(participant, ev.EventId, category.CategoryId);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "01:45:10", finishPosition = 41 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_SeesOnlyTheirOwnResults_WithTheNumberOfFinishers()
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var sipho = await ParticipantAsync("sipho@test.com", "Sipho");
        var lindiwe = await ParticipantAsync("lindiwe@test.com", "Lindiwe");
        var siphoEnrolment = await EnrolAsync(sipho, ev.EventId, category.CategoryId);
        var lindiweEnrolment = await EnrolAsync(lindiwe, ev.EventId, category.CategoryId);
        await CaptureResultAsync(organiser, siphoEnrolment.EnrolmentId, "01:47:32", 47);
        await CaptureResultAsync(organiser, lindiweEnrolment.EnrolmentId, "01:30:05", 12);

        var results = await sipho.GetFromJsonAsync<List<ResultResponse>>("/api/users/me/results", Json);

        var mine = Assert.Single(results!);
        Assert.Equal(47, mine.FinishPosition);
        Assert.Equal(new TimeSpan(1, 47, 32), mine.FinishTime);
        Assert.Equal(2, mine.TotalFinishers); // "47th out of 2 finishers"
    }
}