using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Provides organiser dashboard information and enrolment lists for events owned by the organiser.
/// </summary>
[ApiController]
[Route("api/organiser")]
public class OrganiserController : ControllerBase
{
    private readonly RaceDayDbContext _context;

    public OrganiserController(RaceDayDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves the logged-in organiser's dashboard and their events.
    /// </summary>
    /// <returns>The organiser's ID, role, and events ordered by event date, each with its enrolment count.</returns>
    /// <response code="200">The dashboard was retrieved successfully.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not an organiser.</response>
    [HttpGet("dashboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDashboard()
    {
        if (!SessionAuthorization.IsAuthenticated(HttpContext))
        {
            return Unauthorized(new
            {
                message = "Please log in to access this endpoint."
            });
        }

        if (!SessionAuthorization.HasRole(HttpContext, "Organiser"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Only Organisers can access this endpoint."
            });
        }

        var userId = HttpContext.Session.GetInt32("UserId");

        if (!userId.HasValue)
        {
            return Unauthorized(new
            {
                message = "Your session is invalid. Please log in again."
            });
        }

        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.OrganiserId == userId.Value)
            .OrderBy(e => e.EventDate)
            .Select(e => new
            {
                e.EventId,
                e.EventName,
                e.Description,
                e.EventDate,
                e.Location,
                e.DistanceKm,
                e.EventType,
                EnrolmentCount = _context.Enrolments.Count(
                    enrolment => enrolment.EventId == e.EventId)
            })
            .ToListAsync();

        return Ok(new
        {
            message = "Organiser dashboard accessed successfully.",
            userId = userId.Value,
            role = "Organiser",
            events
        });
    }

    /// <summary>
    /// Retrieves enrolments for an event owned by the logged-in organiser.
    /// </summary>
    /// <param name="eventId">The ID of the event whose enrolments are requested.</param>
    /// <returns>The event's enrolments, including participant and category details.</returns>
    /// <response code="200">The enrolments were retrieved successfully.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not an organiser or does not own the event.</response>
    /// <response code="404">The event does not exist.</response>
    /// <remarks>
    /// Available through both GET /api/events/{eventId}/enrolments and
    /// GET /api/organiser/events/{eventId}/enrolments.
    /// </remarks>
    [HttpGet("/api/events/{eventId:int}/enrolments")]
    [HttpGet("events/{eventId:int}/enrolments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventEnrolments(int eventId)
    {
        if (!SessionAuthorization.IsAuthenticated(HttpContext))
        {
            return Unauthorized(new
            {
                message = "Please log in to access this endpoint."
            });
        }

        if (!SessionAuthorization.HasRole(HttpContext, "Organiser"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Only Organisers can view event enrolments."
            });
        }

        var organiserId = HttpContext.Session.GetInt32("UserId");

        if (!organiserId.HasValue)
        {
            return Unauthorized(new
            {
                message = "Your session is invalid. Please log in again."
            });
        }

        var eventItem = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId);

        if (eventItem == null)
        {
            return NotFound(new
            {
                message = "Event not found."
            });
        }

        if (eventItem.OrganiserId != organiserId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can view enrolments only for your own events."
            });
        }

        var enrolments = await _context.Enrolments
            .AsNoTracking()
            .Where(e => e.EventId == eventId)
            .OrderBy(e => e.EnrolledAt)
            .Select(e => new
            {
                e.EnrolmentId,
                e.ParticipantId,

                ParticipantName = e.Participant != null
                    ? e.Participant.FirstName + " " +
                      e.Participant.LastName
                    : "Unknown Participant",

                e.EventId,

                EventDate = e.Event != null
                    ? (DateTime?)e.Event.EventDate
                    : null,

                e.CategoryId,

                CategoryName = e.Category != null
                    ? e.Category.CategoryName
                    : "Unknown Category",

                e.EnrolmentStatus,
                e.EnrolledAt
            })
            .ToListAsync();

        return Ok(enrolments);
    }
}