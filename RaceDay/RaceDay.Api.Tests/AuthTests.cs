using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

public class AuthTests : ApiTestBase
{
    // ---------- Registration ----------

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task Register_WithValidDetails_Returns201AndTheChosenRole(string role)
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { name = "Thandi Mokoena", email = "thandi@test.com", password = Password, role });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.Equal(role, body!.Role);
        Assert.Equal("thandi@test.com", body.Email);
    }

    [Fact]
    public async Task Register_StoresAHashedPassword_NotThePlainText()
    {
        var client = NewClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new { name = "Thandi Mokoena", email = "thandi@test.com", password = Password, role = "Organiser" });

        var stored = UseDb(db => db.Users.Single(u => u.Email == "thandi@test.com").PasswordHash);

        Assert.NotEqual(Password, stored);
        Assert.DoesNotContain(Password, stored);
        Assert.True(new PasswordService().Verify(Password, stored));
    }

    [Fact]
    public async Task Register_WithAnEmailThatIsAlreadyUsed_Returns409()
    {
        SeedUser("thandi@test.com", "Organiser");
        var client = NewClient();

        // Same address typed in capitals: emails are compared case-insensitively
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { name = "Someone Else", email = "THANDI@test.com", password = Password, role = "Participant" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithARoleThatDoesNotExist_Returns400()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { name = "Thandi", email = "thandi@test.com", password = Password, role = "Admin" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("", "a@test.com", "Password123", "Organiser")]        // no name
    [InlineData("Thandi", "not-an-email", "Password123", "Organiser")] // invalid email
    [InlineData("Thandi", "a@test.com", "short", "Organiser")]         // password too short
    [InlineData("Thandi", "a@test.com", "Password123", "")]            // no role
    public async Task Register_WithMissingOrInvalidFields_Returns400(string name, string email, string password, string role)
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { name, email, password, role });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200AndTheRole()
    {
        SeedUser("sipho@test.com", "Participant", "Sipho Ndlovu");
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "sipho@test.com", password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.Equal("Participant", body!.Role);
        Assert.Equal("Sipho Ndlovu", body.Name);
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_Returns401()
    {
        SeedUser("sipho@test.com", "Participant");
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "sipho@test.com", password = "WrongPass1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithAnEmailThatIsNotRegistered_Returns401()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@test.com", password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Session ----------

    [Fact]
    public async Task Profile_WithoutLoggingIn_Returns401()
    {
        var client = NewClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_AfterLoggingIn_ReturnsTheLoggedInUser()
    {
        var client = await ParticipantAsync("sipho@test.com", "Sipho Ndlovu");

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.Equal("sipho@test.com", body!.Email);
        Assert.Equal("Participant", body.Role);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var client = await ParticipantAsync();

        var logout = await client.PostAsync("/api/auth/logout", null);
        var profileAfterLogout = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, profileAfterLogout.StatusCode);
    }

    // ---------- Updating the profile ----------

    [Fact]
    public async Task UpdateProfile_ChangesTheNameAndEmail()
    {
        var client = await ParticipantAsync("sipho@test.com", "Sipho Ndlovu");

        var response = await client.PutAsJsonAsync("/api/users/me", new { name = "Sipho N", email = "sipho.new@test.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.Equal("Sipho N", body!.Name);
        Assert.Equal("sipho.new@test.com", body.Email);
    }

    [Fact]
    public async Task UpdateProfile_ToAnEmailAnotherUserHas_Returns409()
    {
        SeedUser("taken@test.com", "Organiser");
        var client = await ParticipantAsync("sipho@test.com");

        var response = await client.PutAsJsonAsync("/api/users/me", new { name = "Sipho", email = "taken@test.com" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}