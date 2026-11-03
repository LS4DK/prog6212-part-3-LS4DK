using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

public class CategoryTests : ApiTestBase
{
    [Fact]
    public async Task Organiser_CanAddACategoryToTheirEvent()
    {
        var organiser = await OrganiserAsync();
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new { name = "10km" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await response.Content.ReadFromJsonAsync<CategoryResponse>(Json);
        Assert.Equal("10km", category!.Name);
        Assert.Equal(ev.EventId, category.EventId);
    }

    [Fact]
    public async Task AddingTheSameCategoryNameTwice_Returns409()
    {
        var organiser = await OrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        await CreateCategoryAsync(organiser, ev.EventId, "10km");

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new { name = "10km" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Categories_CanBeViewedWithoutLoggingIn()
    {
        var organiser = await OrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        await CreateCategoryAsync(organiser, ev.EventId, "10km");
        await CreateCategoryAsync(organiser, ev.EventId, "21km");
        var visitor = NewClient();

        var categories = await visitor.GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{ev.EventId}/categories", Json);

        Assert.Equal(2, categories!.Count);
    }

    [Fact]
    public async Task Categories_ForAnEventThatDoesNotExist_Returns404()
    {
        var visitor = NewClient();

        var response = await visitor.GetAsync("/api/events/999/categories");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotAddACategoryToAnotherOrganisersEvent_Returns403()
    {
        var owner = await OrganiserAsync("owner@test.com");
        var ev = await CreateEventAsync(owner);
        var otherOrganiser = await OrganiserAsync("other@test.com");

        var response = await otherOrganiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new { name = "Sneaky" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteAnUnusedCategory()
    {
        var organiser = await OrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);

        var response = await organiser.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_ThatHasEnrolments_Returns409()
    {
        var (organiser, ev, category) = await SetUpRaceAsync();
        var participant = await ParticipantAsync();
        await EnrolAsync(participant, ev.EventId, category.CategoryId);

        var response = await organiser.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}