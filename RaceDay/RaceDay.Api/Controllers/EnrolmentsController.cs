using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Participants enter events; Organisers see who entered their events.</summary>
[ApiController]
[Route("api")]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db)
    {
        _db = db;
    }

    private static IQueryable<EnrolmentResponse> ToResponse(IQueryable<Enrolment> query) =>
        query.Select(en => new EnrolmentResponse
        {
            EnrolmentId = en.EnrolmentId,
            EnrolmentDate = en.EnrolmentDate,
            Status = en.Status,
            ParticipantId = en.ParticipantId,
            ParticipantName = en.Participant.Name,
            EventId = en.EventId,
            EventName = en.Event.Name,
            EventDate = en.Event.Date,
            CategoryId = en.CategoryId,
            CategoryName = en.Category.Name,
            HasResult = en.Result != null
        });

    /// <summary>Enrols the logged-in Participant in an event under a chosen category.</summary>
    /// <param name="eventId">The event ID.</param>
    /// <param name="request">The chosen category ID.</param>
    /// <response code="201">Enrolment recorded.</response>
    /// <response code="400">The category does not belong to this event.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in, but not as a Participant.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="409">Already enrolled in this event.</response>
    [HttpPost("events/{eventId:int}/enrolments")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int eventId, [FromBody] EnrolRequest request)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFound(new { message = "Event not found." });

        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.EventId == eventId);
        if (category == null)
            return BadRequest(new { message = "That category does not belong to this event." });

        var participantId = HttpContext.GetCurrentUserId();
        if (await _db.Enrolments.AnyAsync(e => e.ParticipantId == participantId && e.EventId == eventId))
            return Conflict(new { message = "You are already enrolled in this event." });

        var enrolment = new Enrolment
        {
            ParticipantId = participantId,
            EventId = eventId,
            CategoryId = category.CategoryId,
            EnrolmentDate = DateTime.Now,
            Status = EnrolmentStatus.Confirmed
        };

        try
        {
            _db.Enrolments.Add(enrolment);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Safety net: the unique index on (ParticipantId, EventId) caught a duplicate
            return Conflict(new { message = "You are already enrolled in this event." });
        }

        var response = await ToResponse(_db.Enrolments.Where(e => e.EnrolmentId == enrolment.EnrolmentId)).FirstAsync();
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>Lists every event the logged-in Participant has enrolled in.</summary>
    /// <response code="200">An array of enrolments, latest event first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in, but not as a Participant.</response>
    [HttpGet("users/me/enrolments")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEnrolments()
    {
        var participantId = HttpContext.GetCurrentUserId();
        var enrolments = await ToResponse(
            _db.Enrolments.Where(e => e.ParticipantId == participantId).OrderByDescending(e => e.Event.Date))
            .ToListAsync();

        return Ok(enrolments);
    }

    /// <summary>Lists everyone enrolled in an event. Only the event's Organiser may do this.</summary>
    /// <param name="eventId">The event ID.</param>
    /// <response code="200">An array of enrolments with participant and category.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("events/{eventId:int}/enrolments")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventEnrolments(int eventId)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev == null)
            return NotFound(new { message = "Event not found." });

        if (ev.OrganiserId != HttpContext.GetCurrentUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only view enrolments for your own events." });

        var enrolments = await ToResponse(
            _db.Enrolments.Where(e => e.EventId == eventId).OrderBy(e => e.Participant.Name))
            .ToListAsync();

        return Ok(enrolments);
    }
}