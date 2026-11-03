using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Auth;
using RaceDay.Api.Dtos;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

public class ImageUploadTests : ApiTestBase
{
    // ---------- Event banners ----------

    [Fact]
    public async Task Organiser_CanUploadABanner_AndItsUrlIsSavedOnTheEvent()
    {
        var (organiser, ev, _) = await SetUpRaceAsync();

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", TestImages.ValidPng("banner.png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EventResponse>(Json);
        Assert.StartsWith($"https://fake-storage.test/events/{ev.EventId}/", body!.BannerImageUrl);
        Assert.EndsWith(".png", body.BannerImageUrl);
        Assert.Equal(body.BannerImageUrl, UseDb(db => db.Events.Single().BannerImageUrl));

        // Anyone can see the banner on the event detail
        var visitor = NewClient();
        var detail = await visitor.GetFromJsonAsync<EventResponse>($"/api/events/{ev.EventId}", Json);
        Assert.Equal(body.BannerImageUrl, detail!.BannerImageUrl);
    }

    [Fact]
    public async Task Organiser_CannotUploadABannerForAnotherOrganisersEvent_Returns403()
    {
        var (_, ev, _) = await SetUpRaceAsync("owner@test.com");
        var otherOrganiser = await OrganiserAsync("other@test.com");

        var response = await otherOrganiser.PostAsync($"/api/events/{ev.EventId}/banner", TestImages.ValidPng());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadingABanner_ForAnEventThatDoesNotExist_Returns404()
    {
        var organiser = await OrganiserAsync();

        var response = await organiser.PostAsync("/api/events/999/banner", TestImages.ValidPng());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadingABanner_ThatIsNotAnImageType_Returns400()
    {
        var (organiser, ev, _) = await SetUpRaceAsync();

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner",
            TestImages.Form(TestImages.PngBytes(), "notes.txt", "text/plain"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadingABanner_WhoseContentIsNotReallyAnImage_Returns400()
    {
        var (organiser, ev, _) = await SetUpRaceAsync();
        var fakeImage = System.Text.Encoding.UTF8.GetBytes("This is just text pretending to be a picture.");

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner",
            TestImages.Form(fakeImage, "fake.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadingAnEmptyFile_Returns400()
    {
        var (organiser, ev, _) = await SetUpRaceAsync();

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner",
            TestImages.Form(Array.Empty<byte>(), "empty.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadingATooLargeImage_Returns400()
    {
        var (organiser, ev, _) = await SetUpRaceAsync();
        var huge = new byte[5 * 1024 * 1024 + 1];
        TestImages.PngBytes().CopyTo(huge, 0);

        var response = await organiser.PostAsync($"/api/events/{ev.EventId}/banner", TestImages.Form(huge, "huge.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Profile pictures ----------

    [Fact]
    public async Task Participant_CanUploadAProfilePicture_AndItShowsOnTheirProfile()
    {
        var participantId = SeedUser("sipho@test.com", RoleNames.Participant, "Sipho");
        var participant = await LoginAsync("sipho@test.com");

        var response = await participant.PostAsync("/api/users/me/picture", TestImages.ValidPng("me.png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.StartsWith($"https://fake-storage.test/profiles/{participantId}/", body!.ProfilePictureUrl);

        var profile = await participant.GetFromJsonAsync<AuthResponse>("/api/users/me", Json);
        Assert.Equal(body.ProfilePictureUrl, profile!.ProfilePictureUrl);
    }

    [Fact]
    public async Task OrganiserToo_CanUploadAProfilePicture()
    {
        var organiser = await OrganiserAsync();

        var response = await organiser.PostAsync("/api/users/me/picture", TestImages.ValidPng("me.png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProfilePicture_WithAnInvalidFile_Returns400()
    {
        var participant = await ParticipantAsync();

        var response = await participant.PostAsync("/api/users/me/picture",
            TestImages.Form(TestImages.PngBytes(), "me.exe", "application/octet-stream"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}