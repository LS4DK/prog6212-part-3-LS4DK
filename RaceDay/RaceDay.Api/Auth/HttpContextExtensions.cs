using Microsoft.AspNetCore.Http;
using RaceDay.Api.Services;

namespace RaceDay.Api.Auth;

/// <summary>Small helpers for reading who is logged in. Only call these on [SessionAuthorize] endpoints.</summary>
public static class HttpContextExtensions
{
    public static int GetCurrentUserId(this HttpContext context) =>
        context.Session.GetInt32(SessionKeys.UserId)
        ?? throw new InvalidOperationException("No user in session.");

    public static string GetCurrentRole(this HttpContext context) =>
        context.Session.GetString(SessionKeys.Role)
        ?? throw new InvalidOperationException("No role in session.");
}