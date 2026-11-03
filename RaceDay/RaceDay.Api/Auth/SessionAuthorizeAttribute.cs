using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Api.Services;

namespace RaceDay.Api.Auth;

/// <summary>
/// Protects a controller or action using the server-side session created at login.
/// [SessionAuthorize]                      -> any logged-in user
/// [SessionAuthorize(RoleNames.Organiser)] -> Organisers only
/// Not logged in -> 401 Unauthorized. Logged in with the wrong role -> 403 Forbidden.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _allowedRoles;

    public SessionAuthorizeAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        // No session at all: the caller is not logged in
        if (userId == null || role == null)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "You must be logged in to do that." });
            return;
        }

        // Logged in, but this endpoint is restricted to other roles
        if (_allowedRoles.Length > 0 && !_allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(new { message = "Your role is not allowed to do that." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}