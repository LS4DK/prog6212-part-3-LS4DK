using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

/// <summary>
/// Checks that protected endpoints reject callers who are not logged in (401)
/// and callers who are logged in with the wrong role (403).
/// </summary>
public class RoleAccessTests : ApiTestBase
{
    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(new { });
        return await client.SendAsync(request);
    }

    // ---------- Not logged in: 401 ----------

    [Theory]
    [InlineData("GET", "/api/users/me")]
    [InlineData("PUT", "/api/users/me")]
    [InlineData("POST", "/api/events")]
    [InlineData("PUT", "/api/events/1")]
    [InlineData("DELETE", "/api/events/1")]
    [InlineData("POST", "/api/events/1/categories")]
    [InlineData("DELETE", "/api/categories/1")]
    [InlineData("POST", "/api/events/1/enrolments")]
    [InlineData("GET", "/api/users/me/enrolments")]
    [InlineData("GET", "/api/events/1/enrolments")]
    [InlineData("POST", "/api/enrolments/1/result")]
    [InlineData("PUT", "/api/enrolments/1/result")]
    [InlineData("GET", "/api/users/me/results")]
    public async Task ProtectedEndpoint_WhenNotLoggedIn_Returns401(string method, string url)
    {
        var visitor = NewClient();

        var response = await SendAsync(visitor, method, url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Organiser-only endpoints, called by a Participant: 403 ----------

    [Theory]
    [InlineData("POST", "/api/events")]
    [InlineData("PUT", "/api/events/1")]
    [InlineData("DELETE", "/api/events/1")]
    [InlineData("POST", "/api/events/1/categories")]
    [InlineData("DELETE", "/api/categories/1")]
    [InlineData("GET", "/api/events/1/enrolments")]
    [InlineData("POST", "/api/enrolments/1/result")]
    [InlineData("PUT", "/api/enrolments/1/result")]
    public async Task OrganiserOnlyEndpoint_CalledByAParticipant_Returns403(string method, string url)
    {
        var participant = await ParticipantAsync();

        var response = await SendAsync(participant, method, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- Participant-only endpoints, called by an Organiser: 403 ----------

    [Theory]
    [InlineData("POST", "/api/events/1/enrolments")]
    [InlineData("GET", "/api/users/me/enrolments")]
    [InlineData("GET", "/api/users/me/results")]
    public async Task ParticipantOnlyEndpoint_CalledByAnOrganiser_Returns403(string method, string url)
    {
        var organiser = await OrganiserAsync();

        var response = await SendAsync(organiser, method, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- The same endpoints work for the right role ----------

    [Fact]
    public async Task Participant_CanStillBrowseEventsAndCategories()
    {
        var (_, ev, _) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();

        var events = await participant.GetAsync("/api/events");
        var categories = await participant.GetAsync($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.OK, events.StatusCode);
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
    }

    [Fact]
    public async Task EachRole_SeesItsOwnRoleOnTheProfile()
    {
        var organiser = await OrganiserAsync();
        var participant = await ParticipantAsync();

        var organiserProfile = await organiser.GetFromJsonAsync<AuthResponse>("/api/users/me", Json);
        var participantProfile = await participant.GetFromJsonAsync<AuthResponse>("/api/users/me", Json);

        Assert.Equal("Organiser", organiserProfile!.Role);
        Assert.Equal("Participant", participantProfile!.Role);
    }
}