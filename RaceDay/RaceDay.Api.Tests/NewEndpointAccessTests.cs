using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

/// <summary>401 for callers who are not logged in, 403 for the wrong role, on the Part 3 endpoints.</summary>
public class NewEndpointAccessTests : ApiTestBase
{
    private static HttpRequestMessage Build(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (url.EndsWith("/banner") || url.EndsWith("/picture"))
            request.Content = TestImages.ValidPng();
        else if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(new { status = "Pending" });
        return request;
    }

    [Theory]
    [InlineData("GET", "/api/events/mine")]
    [InlineData("POST", "/api/events/1/banner")]
    [InlineData("POST", "/api/users/me/picture")]
    [InlineData("PUT", "/api/enrolments/1/status")]
    public async Task NewProtectedEndpoint_WhenNotLoggedIn_Returns401(string method, string url)
    {
        var visitor = NewClient();

        var response = await visitor.SendAsync(Build(method, url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/events/mine")]
    [InlineData("POST", "/api/events/1/banner")]
    [InlineData("PUT", "/api/enrolments/1/status")]
    public async Task NewOrganiserOnlyEndpoint_CalledByAParticipant_Returns403(string method, string url)
    {
        var participant = await ParticipantAsync();

        var response = await participant.SendAsync(Build(method, url));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}