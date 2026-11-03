using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Categories belong to an event. Anyone can view them; only the owning Organiser changes them.</summary>
[ApiController]
[Route("api")]
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Lists the categories available for an event.</summary>
    /// <param name="eventId">The event ID.</param>
    /// <response code="200">An array of categories.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategories(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFound(new { message = "Event not found." });

        var categories = await _db.Categories
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse { CategoryId = c.CategoryId, EventId = c.EventId, Name = c.Name })
            .ToListAsync();

        return Ok(categories);
    }

    /// <summary>Adds an age or distance category to an event. Only the owning Organiser may do this.</summary>
    /// <param name="eventId">The event ID.</param>
    /// <param name="request">The category name.</param>
    /// <response code="201">Category created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="409">The event already has a category with that name.</response>
    [HttpPost("events/{eventId:int}/categories")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory(int eventId, [FromBody] CategoryRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev == null)
            return NotFound(new { message = "Event not found." });

        if (ev.OrganiserId != HttpContext.GetCurrentUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only add categories to your own events." });

        var name = request.Name.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventId == eventId && c.Name == name))
            return Conflict(new { message = "This event already has a category with that name." });

        var category = new Category { EventId = eventId, Name = name };

        try
        {
            _db.Categories.Add(category);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "This event already has a category with that name." });
        }

        var response = new CategoryResponse
        {
            CategoryId = category.CategoryId,
            EventId = category.EventId,
            Name = category.Name
        };
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>Removes a category from an event. Only the owning Organiser may do this.</summary>
    /// <param name="id">The category ID.</param>
    /// <response code="204">Category deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns the event.</response>
    /// <response code="404">No category with that ID.</response>
    /// <response code="409">The category has active enrolments.</response>
    [HttpDelete("categories/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category == null)
            return NotFound(new { message = "Category not found." });

        if (category.Event.OrganiserId != HttpContext.GetCurrentUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only change categories on your own events." });

        if (await _db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return Conflict(new { message = "This category has enrolments and cannot be deleted." });

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}