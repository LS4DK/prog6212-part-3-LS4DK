using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>The logged-in user's own profile. Open to both roles, but only when logged in.</summary>
[ApiController]
[Route("api/users")]
[SessionAuthorize]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IImageStorage _images;
    private readonly ILogger<UsersController> _logger;

    public UsersController(RaceDayDbContext db, IImageStorage images, ILogger<UsersController> logger)
    {
        _db = db;
        _images = images;
        _logger = logger;
    }

    private static AuthResponse ToResponse(User user) => new()
    {
        UserId = user.UserId,
        Name = user.Name,
        Email = user.Email,
        Role = user.Role.RoleName,
        ProfilePictureUrl = user.ProfilePictureUrl
    };

    /// <summary>Returns the logged-in user's own profile.</summary>
    /// <response code="200">The profile of the current user.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = HttpContext.GetCurrentUserId();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);

        // Session exists but the account is gone: treat as logged out
        if (user == null)
        {
            HttpContext.Session.Clear();
            return Unauthorized(new { message = "You must be logged in to do that." });
        }

        return Ok(ToResponse(user));
    }

    /// <summary>Updates the logged-in user's own name and email.</summary>
    /// <param name="request">The new name and email.</param>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="409">That email address belongs to another account.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = HttpContext.GetCurrentUserId();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
        {
            HttpContext.Session.Clear();
            return Unauthorized(new { message = "You must be logged in to do that." });
        }

        var email = request.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == email && u.UserId != userId))
            return Conflict(new { message = "That email address is already in use." });

        user.Name = request.Name.Trim();
        user.Email = email;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "That email address is already in use." });
        }

        return Ok(ToResponse(user));
    }

    /// <summary>Uploads a profile picture (JPG, PNG or WebP, up to 5 MB) to Azure Blob Storage and saves its URL.</summary>
    /// <param name="file">The image file.</param>
    /// <response code="200">The updated profile, including the picture URL.</response>
    /// <response code="400">The file is missing, too large, or not a valid image.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="503">Image storage is unavailable.</response>
    [HttpPost("me/picture")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadMyPicture(IFormFile file, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null)
        {
            HttpContext.Session.Clear();
            return Unauthorized(new { message = "You must be logged in to do that." });
        }

        var error = ImageFileValidator.Validate(file, out var extension, out var contentType);
        if (error != null)
            return BadRequest(new { message = error });

        try
        {
            await using var stream = file.OpenReadStream();
            user.ProfilePictureUrl = await _images.UploadAsync(
                $"profiles/{userId}", $"{Guid.NewGuid():N}{extension}", contentType, stream, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Azure.RequestFailedException)
        {
            _logger.LogError(ex, "Profile picture upload failed for user {UserId}", userId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Image storage is unavailable. Please try again later." });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(user));
    }
}