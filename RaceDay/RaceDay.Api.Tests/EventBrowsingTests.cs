using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

public class EventBrowsingTests : ApiTestBase
{
    private static async Task<EventResponse> PostEventAsync(HttpClient organiser, string name, string location, string eventType, DateTime date)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name,
            description = "Test event",
            date,
            location,
            distance = 10.0,
            eventType
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>(Json))!;
    }

    // Three upcoming events: a run in 10 days, a cycle in 20 days, a walk in 40 days
    private async Task<HttpClient> SeedThreeEventsAsync()
    {
        var organiser = await OrganiserAsync();
        await PostEventAsync(organiser, "Gqeberha Bay Run", "Gqeberha", "Run", DateTime.Today.AddDays(10));
        await PostEventAsync(organiser, "Winelands Cycle Tour", "Stellenbosch", "Cycle", DateTime.Today.AddDays(20));
        await PostEventAsync(organiser, "Joburg Charity Walk", "Johannesburg", "Walk", DateTime.Today.AddDays(40));
        return NewClient();
    }

    [Fact]
    public async Task Search_MatchesTheEventName_IgnoringCase()
    {
        var visitor = await SeedThreeEventsAsync();

        var results = await visitor.GetFromJsonAsync<List<EventResponse>>("/api/events?search=BAY", Json);

        var match = Assert.Single(results!);
        Assert.Equal("Gqeberha Bay Run", match.Name);
    }

    [Fact]
    public async Task Search_AlsoMatchesTheLocation()
    {
        var visitor = await SeedThreeEventsAsync();

        var results = await visitor.GetFromJsonAsync<List<EventResponse>>("/api/events?search=stellenbosch", Json);

        var match = Assert.Single(results!);
        Assert.Equal("Winelands Cycle Tour", match.Name);
    }

    [Fact]
    public async Task EventTypeFilter_ReturnsOnlyThatType()
    {
        var visitor = await SeedThreeEventsAsync();

        var results = await visitor.GetFromJsonAsync<List<EventResponse>>("/api/events?eventType=Cycle", Json);

        var match = Assert.Single(results!);
        Assert.Equal(EventType.Cycle, match.EventType);
    }

    [Fact]
    public async Task DateRangeFilter_ReturnsOnlyEventsInsideTheRange()
    {
        var visitor = await SeedThreeEventsAsync();
        var from = DateTime.Today.AddDays(15).ToString("yyyy-MM-dd");
        var to = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");

        var results = await visitor.GetFromJsonAsync<List<EventResponse>>($"/api/events?fromDate={from}&toDate={to}", Json);

        var match = Assert.Single(results!);
        Assert.Equal("Winelands Cycle Tour", match.Name);
    }

    [Fact]
    public async Task Filters_CanBeCombined()
    {
        var visitor = await SeedThreeEventsAsync();

        var results = await visitor.GetFromJsonAsync<List<EventResponse>>("/api/events?eventType=Walk&search=charity", Json);

        var match = Assert.Single(results!);
        Assert.Equal("Joburg Charity Walk", match.Name);
    }

    [Fact]
    public async Task Filters_WithNoMatches_ReturnAnEmptyList()
    {
        var visitor = await SeedThreeEventsAsync();

        var results = await visitor.GetFromJsonAsync<List<EventResponse>>("/api/events?search=nothing-like-this", Json);

        Assert.Empty(results!);
    }

    [Fact]
    public async Task MyEvents_ListsOnlyTheLoggedInOrganisersEvents_IncludingPastOnes_WithEnrolmentCounts()
    {
        var (organiserA, eventA, categoryA) = await SetUpRaceAsync("a@test.com");
        var pastEvent = await CreateEventAsync(organiserA, "Old Race", DateTime.Today.AddDays(-10));
        var organiserB = await OrganiserAsync("b@test.com");
        await CreateEventAsync(organiserB, "Not Mine");
        var participant = await ParticipantAsync();
        await EnrolAsync(participant, eventA.EventId, categoryA.CategoryId);

        var mine = await organiserA.GetFromJsonAsync<List<EventResponse>>("/api/events/mine", Json);

        Assert.Equal(2, mine!.Count);
        Assert.DoesNotContain(mine, e => e.Name == "Not Mine");
        Assert.Contains(mine, e => e.EventId == pastEvent.EventId);
        Assert.Equal(1, mine.Single(e => e.EventId == eventA.EventId).EnrolmentCount);
    }
}