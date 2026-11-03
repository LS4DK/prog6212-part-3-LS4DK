using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Auth;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

public class EnrolmentTests : ApiTestBase
{
    [Fact]
    public async Task Participant_CanEnrol_AndTheEnrolmentIsRecorded()
    {
        var (_, ev, category) = await SetUpRaceAsync();
        var participantId = SeedUser("sipho@test.com", RoleNames.Participant, "Sipho Ndlovu");
        var participant = await LoginAsync("sipho@test.com");

        var response = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments",
            new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EnrolmentResponse>(Json);
        Assert.Equal(EnrolmentStatus.Confirmed, body!.Status);
        Assert.Equal("Sipho Ndlovu", body.ParticipantName);

        // The enrolment really was stored, linking this participant, event and category
        var stored = UseDb(db => db.Enrolments.Single());
        Assert.Equal(participantId, stored.ParticipantId);
        Assert.Equal(ev.EventId, stored.EventId);
        Assert.Equal(category.CategoryId, stored.CategoryId);
    }

    [Fact]
    public async Task Enrolling_TwiceInTheSameEvent_Returns409()
    {
        var (_, ev, category) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();
        await EnrolAsync(participant, ev.EventId, category.CategoryId);

        var response = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments",
            new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, UseDb(db => db.Enrolments.Count()));
    }

    [Fact]
    public async Task Enrolling_WithACategoryFromAnotherEvent_Returns400()
    {
        var (organiser, eventA, _) = await SetUpRaceAsync();
        var eventB = await CreateEventAsync(organiser, "Winelands Tour");
        var categoryOfB = await CreateCategoryAsync(organiser, eventB.EventId, "94km");
        var participant = await ParticipantAsync();

        var response = await participant.PostAsJsonAsync($"/api/events/{eventA.EventId}/enrolments",
            new { categoryId = categoryOfB.CategoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrolling_WithoutAChosenCategory_Returns400()
    {
        var (_, ev, _) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();

        var response = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/enrolments", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrolling_InAnEventThatDoesNotExist_Returns404()
    {
        var participant = await ParticipantAsync();

        var response = await participant.PostAsJsonAsync("/api/events/999/enrolments", new { categoryId = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MyEnrolments_ShowsOnlyTheLoggedInParticipantsEnrolments()
    {
        var (_, ev, category) = await SetUpRaceAsync();
        var sipho = await ParticipantAsync("sipho@test.com", "Sipho");
        var lindiwe = await ParticipantAsync("lindiwe@test.com", "Lindiwe");
        await EnrolAsync(sipho, ev.EventId, category.CategoryId);
        await EnrolAsync(lindiwe, ev.EventId, category.CategoryId);

        var mine = await sipho.GetFromJsonAsync<List<EnrolmentResponse>>("/api/users/me/enrolments", Json);

        var enrolment = Assert.Single(mine!);
        Assert.Equal("Sipho", enrolment.ParticipantName);
    }

    [Fact]
    public async Task Organiser_CanSeeEveryoneEnrolledInTheirEvent()
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var sipho = await ParticipantAsync("sipho@test.com", "Sipho");
        var lindiwe = await ParticipantAsync("lindiwe@test.com", "Lindiwe");
        await EnrolAsync(sipho, ev.EventId, category.CategoryId);
        await EnrolAsync(lindiwe, ev.EventId, category.CategoryId);

        var enrolments = await organiser.GetFromJsonAsync<List<EnrolmentResponse>>($"/api/events/{ev.EventId}/enrolments", Json);

        Assert.Equal(2, enrolments!.Count);
        Assert.All(enrolments, e => Assert.Equal(category.Name, e.CategoryName));
    }

    [Fact]
    public async Task Organiser_CannotSeeEnrolmentsForAnotherOrganisersEvent_Returns403()
    {
        var (_, ev, _) = await SetUpRaceAsync("owner@test.com");
        var otherOrganiser = await OrganiserAsync("other@test.com");

        var response = await otherOrganiser.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EventEnrolments_ForAnEventThatDoesNotExist_Returns404()
    {
        var organiser = await OrganiserAsync();

        var response = await organiser.GetAsync("/api/events/999/enrolments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}