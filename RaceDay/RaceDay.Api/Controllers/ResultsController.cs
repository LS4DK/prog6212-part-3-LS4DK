using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Organisers capture results; Participants view their own race history.</summary>
[ApiController]
[Route("api")]
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db)
    {
        _db = db;
    }

    private IQueryable<ResultResponse> ToResponse(IQueryable<Result> query) =>
        query.Select(r => new ResultResponse
        {
            ResultId = r.ResultId,
            EnrolmentId = r.EnrolmentId,
            EventId = r.Enrolment.EventId,
            EventName = r.Enrolment.Event.Name,
            EventDate = r.Enrolment.Event.Date,
            CategoryName = r.Enrolment.Category.Name,
            ParticipantName = r.Enrolment.Participant.Name,
            FinishTime = r.FinishTime,
            FinishPosition = r.FinishPosition,
            // e.g. "47th out of 312 finishers"
            TotalFinishers = _db.Results.Count(x => x.Enrolment.EventId == r.Enrolment.EventId)
        });

    /// <summary>Captures the finish time and position for an enrolment. Only the event's Organiser may do this.</summary>
    /// <param name="enrolmentId">The enrolment ID.</param>
    /// <param name="request">Finish time (hh:mm:ss) and finishing position.</param>
    /// <response code="201">Result recorded.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">The enrolment does not exist.</response>
    /// <response code="409">A result has already been captured for this enrolment.</response>
    [HttpPost("enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CaptureResult(int enrolmentId, [FromBody] ResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Event)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
        if (enrolment == null)
            return NotFound(new { message = "Enrolment not found." });

        if (enrolment.Event.OrganiserId != HttpContext.GetCurrentUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only capture results for your own events." });

        if (enrolment.Result != null)
            return Conflict(new { message = "A result has already been captured for this enrolment." });

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishTime = request.FinishTime!.Value,
            FinishPosition = request.FinishPosition!.Value
        };

        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var response = await ToResponse(_db.Results.Where(r => r.ResultId == result.ResultId)).FirstAsync();
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>Corrects a previously captured result. Only the event's Organiser may do this.</summary>
    /// <param name="enrolmentId">The enrolment ID.</param>
    /// <param name="request">The corrected finish time and position.</param>
    /// <response code="200">Result updated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">The enrolment does not exist, or has no result yet.</response>
    [HttpPut("enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateResult(int enrolmentId, [FromBody] ResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Event)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);
        if (enrolment == null)
            return NotFound(new { message = "Enrolment not found." });

        if (enrolment.Event.OrganiserId != HttpContext.GetCurrentUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only change results for your own events." });

        if (enrolment.Result == null)
            return NotFound(new { message = "No result has been captured for this enrolment yet." });

        enrolment.Result.FinishTime = request.FinishTime!.Value;
        enrolment.Result.FinishPosition = request.FinishPosition!.Value;
        await _db.SaveChangesAsync();

        var response = await ToResponse(_db.Results.Where(r => r.ResultId == enrolment.Result.ResultId)).FirstAsync();
        return Ok(response);
    }

    /// <summary>Returns the logged-in Participant's personal race history.</summary>
    /// <response code="200">An array of results across all completed events, latest first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in, but not as a Participant.</response>
    [HttpGet("users/me/results")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyResults()
    {
        var participantId = HttpContext.GetCurrentUserId();
        var results = await ToResponse(
            _db.Results.Where(r => r.Enrolment.ParticipantId == participantId)
                       .OrderByDescending(r => r.Enrolment.Event.Date))
            .ToListAsync();

        return Ok(results);
    }
}