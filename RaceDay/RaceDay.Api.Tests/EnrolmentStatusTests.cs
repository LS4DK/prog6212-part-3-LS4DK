using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

public class EnrolmentStatusTests : ApiTestBase
{
    [Fact]
    public async Task Organiser_CanChangeAnEnrolmentStatus_AndTheParticipantSeesIt()
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();
        var enrolment = await EnrolAsync(participant, ev.EventId, category.CategoryId);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { status = "Pending" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EnrolmentResponse>(Json);
        Assert.Equal(EnrolmentStatus.Pending, body!.Status);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentResponse>>("/api/users/me/enrolments", Json);
        Assert.Equal(EnrolmentStatus.Pending, Assert.Single(mine!).Status);
    }

    [Theory]
    [InlineData("Maybe")]
    [InlineData("")]
    public async Task ChangingStatus_ToSomethingInvalid_Returns400(string status)
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();
        var enrolment = await EnrolAsync(participant, ev.EventId, category.CategoryId);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { status });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangingStatus_WithNoStatusAtAll_Returns400()
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();
        var enrolment = await EnrolAsync(participant, ev.EventId, category.CategoryId);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotChangeStatusOnAnotherOrganisersEvent_Returns403()
    {
        var (_, ev, category) = await SetUpRaceAsync("owner@test.com");
        var participant = await ParticipantAsync();
        var enrolment = await EnrolAsync(participant, ev.EventId, category.CategoryId);
        var otherOrganiser = await OrganiserAsync("other@test.com");

        var response = await otherOrganiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { status = "Cancelled" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangingStatus_OfAnEnrolmentThatDoesNotExist_Returns404()
    {
        var organiser = await OrganiserAsync();

        var response = await organiser.PutAsJsonAsync("/api/enrolments/999/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EventEnrolments_ShowTheCapturedResult()
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();
        var enrolment = await EnrolAsync(participant, ev.EventId, category.CategoryId);
        await CaptureResultAsync(organiser, enrolment.EnrolmentId, "01:47:32", 47);

        var list = await organiser.GetFromJsonAsync<List<EnrolmentResponse>>($"/api/events/{ev.EventId}/enrolments", Json);

        var row = Assert.Single(list!);
        Assert.True(row.HasResult);
        Assert.Equal(47, row.FinishPosition);
        Assert.Equal(new TimeSpan(1, 47, 32), row.FinishTime);
    }
}