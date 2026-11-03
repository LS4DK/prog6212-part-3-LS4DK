using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Auth;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

public class EventTests : ApiTestBase
{
    // ---------- Creating ----------

    [Fact]
    public async Task Organiser_CanCreateAnEvent_AndIsRecordedAsItsOwner()
    {
        var organiserId = SeedUser("organiser@test.com", RoleNames.Organiser);
        var organiser = await LoginAsync("organiser@test.com");

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "Gqeberha Bay Run",
            description = "Coastal run along Humewood",
            date = DateTime.Today.AddDays(30),
            location = "Gqeberha",
            distance = 21.1,
            eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var ev = await response.Content.ReadFromJsonAsync<EventResponse>(Json);
        Assert.Equal("Gqeberha Bay Run", ev!.Name);
        Assert.Equal(EventType.Run, ev.EventType);
        Assert.Equal(organiserId, ev.OrganiserId);
    }

    [Theory]
    [InlineData("", 21.1, "Run")]          // no name
    [InlineData("Bay Run", 0.0, "Run")]    // distance must be above zero
    [InlineData("Bay Run", 21.1, "Swim")]  // not one of Run, Walk, Cycle
    public async Task CreateEvent_WithInvalidData_Returns400(string name, double distance, string eventType)
    {
        var organiser = await OrganiserAsync();

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name,
            description = "x",
            date = DateTime.Today.AddDays(30),
            location = "Gqeberha",
            distance,
            eventType
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Viewing (public) ----------

    [Fact]
    public async Task Events_CanBeViewedWithoutLoggingIn()
    {
        var organiser = await OrganiserAsync();
        var created = await CreateEventAsync(organiser);
        var visitor = NewClient(); // never logged in

        var list = await visitor.GetFromJsonAsync<List<EventResponse>>("/api/events", Json);
        var single = await visitor.GetAsync($"/api/events/{created.EventId}");

        Assert.Contains(list!, e => e.EventId == created.EventId);
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
    }

    [Fact]
    public async Task GetEvent_WithAnIdThatDoesNotExist_Returns404()
    {
        var visitor = NewClient();

        var response = await visitor.GetAsync("/api/events/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EventList_HidesPastEventsUnlessAskedFor()
    {
        var organiser = await OrganiserAsync();
        var past = await CreateEventAsync(organiser, "Old Race", DateTime.Today.AddDays(-10));
        var visitor = NewClient();

        var upcomingOnly = await visitor.GetFromJsonAsync<List<EventResponse>>("/api/events", Json);
        var everything = await visitor.GetFromJsonAsync<List<EventResponse>>("/api/events?includePast=true", Json);

        Assert.DoesNotContain(upcomingOnly!, e => e.EventId == past.EventId);
        Assert.Contains(everything!, e => e.EventId == past.EventId);
    }

    // ---------- Updating ----------

    [Fact]
    public async Task Organiser_CanUpdateTheirOwnEvent()
    {
        var organiser = await OrganiserAsync();
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PutAsJsonAsync($"/api/events/{ev.EventId}", new
        {
            name = "Renamed Run",
            description = "Updated",
            date = DateTime.Today.AddDays(40),
            location = "Humewood",
            distance = 10.0,
            eventType = "Walk"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventResponse>(Json);
        Assert.Equal("Renamed Run", updated!.Name);
        Assert.Equal("Humewood", updated.Location);
        Assert.Equal(EventType.Walk, updated.EventType);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var owner = await OrganiserAsync("owner@test.com");
        var ev = await CreateEventAsync(owner);
        var otherOrganiser = await OrganiserAsync("other@test.com");

        var response = await otherOrganiser.PutAsJsonAsync($"/api/events/{ev.EventId}", new
        {
            name = "Hijacked",
            description = "x",
            date = DateTime.Today.AddDays(40),
            location = "Somewhere",
            distance = 5.0,
            eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_ThatDoesNotExist_Returns404()
    {
        var organiser = await OrganiserAsync();

        var response = await organiser.PutAsJsonAsync("/api/events/999", new
        {
            name = "Ghost",
            description = "x",
            date = DateTime.Today.AddDays(40),
            location = "Nowhere",
            distance = 5.0,
            eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Deleting ----------

    [Fact]
    public async Task Organiser_CanDeleteTheirOwnEvent()
    {
        var organiser = await OrganiserAsync();
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.DeleteAsync($"/api/events/{ev.EventId}");
        var lookup = await organiser.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, lookup.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_Returns403()
    {
        var owner = await OrganiserAsync("owner@test.com");
        var ev = await CreateEventAsync(owner);
        var otherOrganiser = await OrganiserAsync("other@test.com");

        var response = await otherOrganiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_ThatAlreadyHasEnrolments_Returns409()
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();
        await EnrolAsync(participant, ev.EventId, category.CategoryId);

        var response = await organiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}