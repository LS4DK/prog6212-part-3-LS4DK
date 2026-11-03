using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout. Login starts a server-side session holding the user's ID and role.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly PasswordService _passwords;

    public AuthController(RaceDayDbContext db, PasswordService passwords)
    {
        _db = db;
        _passwords = passwords;
    }

    /// <summary>Creates a new account as either an Organiser or a Participant.</summary>
    /// <param name="request">Name, email, password and the chosen role ("Organiser" or "Participant").</param>
    /// <response code="201">Account created.</response>
    /// <response code="400">Missing or invalid fields, or the role is not Organiser/Participant.</response>
    /// <response code="409">That email address is already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var roleName = request.Role.Trim().ToLower();
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName.ToLower() == roleName);
        if (role == null)
            return BadRequest(new { message = "Role must be either 'Organiser' or 'Participant'." });

        var email = request.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "An account with that email already exists." });

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = _passwords.Hash(request.Password),
            RoleId = role.RoleId
        };

        try
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Safety net: the unique index on Email caught a duplicate that slipped past the check above
            return Conflict(new { message = "An account with that email already exists." });
        }

        var response = new AuthResponse
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Role = role.RoleName
        };
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>Checks the credentials and starts a session that remembers the user's ID and role.</summary>
    /// <param name="request">Email and password.</param>
    /// <response code="200">Session started; returns the user's ID and role.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLower();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);

        // Same message whether the email or the password is wrong, so attackers can't tell which emails exist
        if (user == null || !_passwords.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.RoleName);

        return Ok(new AuthResponse
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.RoleName
        });
    }

    /// <summary>Ends the current session.</summary>
    /// <response code="204">Session cleared.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }
}