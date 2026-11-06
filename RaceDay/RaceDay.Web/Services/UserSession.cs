using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services;

/// <summary>
/// Who is logged in on the website. Kept in the MVC app's own server-side session:
/// the user's ID, name and role, plus the API's session cookie (sent along with every API call).
/// </summary>
public class UserSession
{
    private const string ApiCookieKey = "ApiCookie";
    private const string UserIdKey = "UserId";
    private const string NameKey = "Name";
    private const string RoleKey = "Role";

    private readonly IHttpContextAccessor _accessor;

    public UserSession(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ISession Session => _accessor.HttpContext!.Session;

    public string? ApiCookie => Session.GetString(ApiCookieKey);
    public string? Role => Session.GetString(RoleKey);
    public string Name => Session.GetString(NameKey) ?? string.Empty;
    public int UserId => Session.GetInt32(UserIdKey) ?? 0;

    public bool IsLoggedIn => !string.IsNullOrEmpty(ApiCookie) && !string.IsNullOrEmpty(Role);
    public bool IsOrganiser => IsLoggedIn && Role == RoleNames.Organiser;
    public bool IsParticipant => IsLoggedIn && Role == RoleNames.Participant;

    public void SetApiCookie(string value) => Session.SetString(ApiCookieKey, value);

    /// <summary>Call after the API has accepted the login (the API cookie is already stored).</summary>
    public void SignIn(UserDto user)
    {
        Session.SetInt32(UserIdKey, user.UserId);
        Session.SetString(NameKey, user.Name);
        Session.SetString(RoleKey, user.Role);
    }

    public void UpdateName(string name) => Session.SetString(NameKey, name);

    public void SignOut() => Session.Clear();
}