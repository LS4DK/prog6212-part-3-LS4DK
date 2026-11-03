using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests.Infrastructure;

/// <summary>
/// Base class for all API tests. Every test gets its own copy of the API running in memory
/// with its own empty database, so tests can't affect each other.
/// </summary>
public abstract class ApiTestBase : IDisposable
{
    protected const string Password = "Password123";

    // Hashed once and reused for every seeded user, which keeps the tests fast
    private static readonly string PasswordHash = new PasswordService(1_000).Hash(Password);

    // Used to read API responses back into objects (enums arrive as names such as "Run")
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected RaceDayFactory Factory { get; } = new();

    public void Dispose()
    {
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---------- Clients ----------

    /// <summary>A new client with its own cookie jar, which means its own session. Not logged in.</summary>
    protected HttpClient NewClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    /// <summary>Logs in through the real login endpoint and returns a client that holds the session cookie.</summary>
    protected async Task<HttpClient> LoginAsync(string email)
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    protected async Task<HttpClient> OrganiserAsync(string email = "organiser@test.com", string name = "Test Organiser")
    {
        SeedUser(email, RoleNames.Organiser, name);
        return await LoginAsync(email);
    }

    protected async Task<HttpClient> ParticipantAsync(string email = "participant@test.com", string name = "Test Participant")
    {
        SeedUser(email, RoleNames.Participant, name);
        return await LoginAsync(email);
    }

    // ---------- Direct database access ----------

    protected T UseDb<T>(Func<RaceDayDbContext, T> action)
    {
        using var scope = Factory.Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<RaceDayDbContext>());
    }

    /// <summary>Adds a user straight into the database (faster than registering through the API).</summary>
    protected int SeedUser(string email, string role, string name = "Test User") =>
        UseDb(db =>
        {
            var roleId = db.Roles.Single(r => r.RoleName == role).RoleId;
            var user = new User { Name = name, Email = email, PasswordHash = PasswordHash, RoleId = roleId };
            db.Users.Add(user);
            db.SaveChanges();
            return user.UserId;
        });

    // ---------- Shortcuts for setting up test data through the API ----------

    protected async Task<EventResponse> CreateEventAsync(HttpClient organiser, string name = "Gqeberha Bay Run", DateTime? date = null)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name,
            description = "Test event",
            date = date ?? DateTime.Today.AddDays(30),
            location = "Gqeberha",
            distance = 21.1,
            eventType = "Run"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>(Json))!;
    }

    protected async Task<CategoryResponse> CreateCategoryAsync(HttpClient organiser, int eventId, string name = "21km")
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new { name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>(Json))!;
    }

    protected async Task<EnrolmentResponse> EnrolAsync(HttpClient participant, int eventId, int categoryId)
    {
        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrolmentResponse>(Json))!;
    }

    protected async Task<HttpResponseMessage> CaptureResultAsync(HttpClient organiser, int enrolmentId, string finishTime, int finishPosition) =>
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", new { finishTime, finishPosition });

    /// <summary>A logged-in Organiser who owns one event with one category, ready for participants to enter.</summary>
    protected async Task<(HttpClient Organiser, EventResponse Event, CategoryResponse Category)> SetUpRaceAsync(
        string organiserEmail = "organiser@test.com")
    {
        var organiser = await OrganiserAsync(organiserEmail);
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        return (organiser, ev, category);
    }
}