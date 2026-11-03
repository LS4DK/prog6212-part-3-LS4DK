using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Browsing events (public) and managing them (Organisers, own events only).</summary>
[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db)
    {
        _db = db;
    }

    // Shapes the database rows into what the API returns, in a single query
    private static IQueryable<EventResponse> ToResponse(IQueryable<Event> query) =>
        query.Select(e => new EventResponse
        {
            EventId = e.EventId,
            Name = e.Name,
            Description = e.Description,
            Date = e.Date,
            Location = e.Location,
            Distance = e.Distance,
            EventType = e.EventType,
            BannerImageUrl = e.BannerImageUrl,
            OrganiserId = e.OrganiserId,
            OrganiserName = e.Organiser.Name,
            EnrolmentCount = e.Enrolments.Count,
            Categories = e.Categories
                .OrderBy(c => c.Name)
                .Select(c => new CategoryResponse { CategoryId = c.CategoryId, EventId = c.EventId, Name = c.Name })
                .ToList()
        });

    /// <summary>Lists events, soonest first. Upcoming events only unless includePast is true.</summary>
    /// <param name="includePast">Set to true to include events that have already happened.</param>
    /// <response code="200">An array of events.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents([FromQuery] bool includePast = false)
    {
        var query = _db.Events.AsQueryable();
        if (!includePast)
            query = query.Where(e => e.Date >= DateTime.Today);

        var events = await ToResponse(query.OrderBy(e => e.Date)).ToListAsync();
        return Ok(events);
    }

    /// <summary>Returns the full detail of one event, including its categories.</summary>
    /// <param name="id">The event ID.</param>
    /// <response code="200">The event.</response>
    /// <response code="404">No event with that ID.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int id)
    {
        var ev = await ToResponse(_db.Events.Where(e => e.EventId == id)).FirstOrDefaultAsync();
        if (ev == null)
            return NotFound(new { message = "Event not found." });

        return Ok(ev);
    }

    /// <summary>Creates an event owned by the logged-in Organiser.</summary>
    /// <param name="request">Name, description, date, location, distance and event type.</param>
    /// <response code="201">Event created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in, but not as an Organiser.</response>
    [HttpPost]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEvent([FromBody] EventRequest request)
    {
        var ev = new Event
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Date = request.Date!.Value,
            Location = request.Location.Trim(),
            Distance = request.Distance,
            EventType = request.EventType,
            OrganiserId = HttpContext.GetCurrentUserId()
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        var response = await ToResponse(_db.Events.Where(e => e.EventId == ev.EventId)).FirstAsync();
        return CreatedAtAction(nameof(GetEvent), new { id = ev.EventId }, response);
    }

    /// <summary>Updates an event. Only the Organiser who created it may do this.</summary>
    /// <param name="id">The event ID.</param>
    /// <param name="request">The new event details.</param>
    /// <response code="200">Event updated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">No event with that ID.</response>
    [HttpPut("{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(int id, [FromBody] EventRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev == null)
            return NotFound(new { message = "Event not found." });

        if (ev.OrganiserId != HttpContext.GetCurrentUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only change your own events." });

        ev.Name = request.Name.Trim();
        ev.Description = request.Description?.Trim();
        ev.Date = request.Date!.Value;
        ev.Location = request.Location.Trim();
        ev.Distance = request.Distance;
        ev.EventType = request.EventType;
        await _db.SaveChangesAsync();

        var response = await ToResponse(_db.Events.Where(e => e.EventId == id)).FirstAsync();
        return Ok(response);
    }

    /// <summary>Deletes an event. Only its Organiser may do this, and only if nobody has enrolled.</summary>
    /// <param name="id">The event ID.</param>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">No event with that ID.</response>
    /// <response code="409">The event already has enrolments.</response>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _db.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev == null)
            return NotFound(new { message = "Event not found." });

        if (ev.OrganiserId != HttpContext.GetCurrentUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only delete your own events." });

        // Results and enrolments are kept as history, so an event that has them can't be removed
        if (await _db.Enrolments.AnyAsync(e => e.EventId == id))
            return Conflict(new { message = "This event already has enrolments and cannot be deleted." });

        _db.Categories.RemoveRange(ev.Categories);
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}