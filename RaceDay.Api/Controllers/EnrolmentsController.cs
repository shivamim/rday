using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Provides participant enrolment management and authorised enrolment viewing.
/// </summary>
[ApiController]
[Route("api/enrolments")]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayDbContext _context;

    public EnrolmentsController(RaceDayDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Enrols the logged-in participant in an event using the selected category.
    /// </summary>
    /// <param name="eventId">The ID of the event to enter.</param>
    /// <param name="request">The category ID selected for the enrolment.</param>
    /// <returns>The newly created enrolment details.</returns>
    /// <response code="201">The enrolment was created successfully.</response>
    /// <response code="400">The event has already taken place or the category does not belong to the event.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not a participant.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="409">The participant is already enrolled or a duplicate enrolment could not be saved.</response>
    [HttpPost("/api/events/{eventId:int}/enrolments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateEventEnrolment(
        int eventId,
        [FromBody] CreateEventEnrolmentRequest request)
    {
        return await CreateEnrolmentCore(eventId, request.CategoryId);
    }

    /// <summary>
    /// Creates an enrolment using the event ID and category ID in the request body.
    /// </summary>
    /// <param name="request">The event ID and category ID for the enrolment.</param>
    /// <returns>The newly created enrolment details.</returns>
    /// <response code="201">The enrolment was created successfully.</response>
    /// <response code="400">The event has already taken place or the category does not belong to the event.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not a participant.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="409">The participant is already enrolled or a duplicate enrolment could not be saved.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateEnrolment(
        [FromBody] CreateEnrolmentRequest request)
    {
        return await CreateEnrolmentCore(
            request.EventId,
            request.CategoryId);
    }

    private async Task<IActionResult> CreateEnrolmentCore(
        int eventId,
        int categoryId)
    {
        if (!SessionAuthorization.IsAuthenticated(HttpContext))
        {
            return Unauthorized(new
            {
                message = "Please log in to access this endpoint."
            });
        }

        if (!SessionAuthorization.HasRole(HttpContext, "Participant"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Only Participants can enrol in events."
            });
        }

        var participantId = HttpContext.Session.GetInt32("UserId");

        if (!participantId.HasValue)
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
            return NotFound(new { message = "Event not found." });
        }

        if (eventItem.EventDate.Date < DateTime.Today)
        {
            return BadRequest(new
            {
                message = "You cannot enrol in an event that has already taken place."
            });
        }

        var category = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.CategoryId == categoryId &&
                c.EventId == eventId);

        if (category == null)
        {
            return BadRequest(new
            {
                message = "The selected category does not belong to this event."
            });
        }

        var alreadyEnrolled = await _context.Enrolments.AnyAsync(e =>
            e.ParticipantId == participantId.Value &&
            e.EventId == eventId);

        if (alreadyEnrolled)
        {
            return Conflict(new
            {
                message = "You are already enrolled in this event."
            });
        }

        var enrolment = new Enrolment
        {
            ParticipantId = participantId.Value,
            EventId = eventId,
            CategoryId = categoryId,
            EnrolmentStatus = "Pending",
            EnrolledAt = DateTime.Now
        };

        _context.Enrolments.Add(enrolment);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The database unique constraint is the final protection
            // against duplicate enrolments.
            return Conflict(new
            {
                message = "The enrolment could not be saved. You may already be enrolled in this event."
            });
        }

        return Created(
            $"/api/enrolments/{enrolment.EnrolmentId}",
            new
            {
                enrolment.EnrolmentId,
                enrolment.ParticipantId,
                enrolment.EventId,
                enrolment.CategoryId,
                enrolment.EnrolmentStatus,
                enrolment.EnrolledAt
            });
    }

    /// <summary>
    /// Updates the status of an enrolment (Pending or Confirmed).
    /// Only the organiser who owns the event may do this.
    /// </summary>
    /// <param name="eventId">The ID of the event that contains the enrolment.</param>
    /// <param name="enrolmentId">The ID of the enrolment to update.</param>
    /// <param name="request">The new status: Pending or Confirmed.</param>
    /// <returns>The updated enrolment status.</returns>
    /// <response code="200">The enrolment status was updated.</response>
    /// <response code="400">The status is not Pending or Confirmed.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not an organiser or does not own the event.</response>
    /// <response code="404">The event or the enrolment does not exist.</response>
    [HttpPut("/api/events/{eventId:int}/enrolments/{enrolmentId:int}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEnrolmentStatus(
        int eventId,
        int enrolmentId,
        [FromBody] UpdateEnrolmentStatusRequest request)
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
                message = "Only Organisers can update enrolment statuses."
            });
        }

        var organiserId = HttpContext.Session.GetInt32("UserId");

        var eventItem = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId);

        if (eventItem == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        if (eventItem.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can update enrolments only for your own events."
            });
        }

        var enrolment = await _context.Enrolments
            .FirstOrDefaultAsync(e =>
                e.EnrolmentId == enrolmentId &&
                e.EventId == eventId);

        if (enrolment == null)
        {
            return NotFound(new
            {
                message = "Enrolment not found for this event."
            });
        }

        var status = request.EnrolmentStatus?.Trim();

        if (status != "Pending" && status != "Confirmed")
        {
            return BadRequest(new
            {
                message = "Enrolment status must be Pending or Confirmed."
            });
        }

        enrolment.EnrolmentStatus = status;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            enrolment.EnrolmentId,
            enrolment.EventId,
            enrolment.ParticipantId,
            enrolment.EnrolmentStatus,
            message = "Enrolment status updated successfully."
        });
    }

    /// <summary>
    /// Retrieves an enrolment for its participant or the organiser who owns its event.
    /// </summary>
    /// <param name="enrolmentId">The ID of the enrolment to retrieve.</param>
    /// <returns>The enrolment details, including its event and category names.</returns>
    /// <response code="200">The enrolment was retrieved successfully.</response>
    /// <response code="401">The user is not logged in.</response>
    /// <response code="403">The user does not have permission to view this enrolment.</response>
    /// <response code="404">The enrolment does not exist.</response>
    [HttpGet("{enrolmentId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrolment(int enrolmentId)
    {
        if (!SessionAuthorization.IsAuthenticated(HttpContext))
        {
            return Unauthorized(new
            {
                message = "Please log in to access this endpoint."
            });
        }

        var userId = HttpContext.Session.GetInt32("UserId");

        var enrolment = await _context.Enrolments
            .AsNoTracking()
            .Where(e => e.EnrolmentId == enrolmentId)
            .Select(e => new
            {
                e.EnrolmentId,
                e.ParticipantId,
                e.EventId,
                e.CategoryId,
                e.EnrolmentStatus,
                e.EnrolledAt,
                EventName = e.Event != null
                    ? e.Event.EventName
                    : "Unknown Event",
                EventDate = e.Event != null
                    ? (DateTime?)e.Event.EventDate
                    : null,
                CategoryName = e.Category != null
                    ? e.Category.CategoryName
                    : "Unknown Category"
            })
            .FirstOrDefaultAsync();

        if (enrolment == null)
        {
            return NotFound(new
            {
                message = "Enrolment not found."
            });
        }

        if (SessionAuthorization.HasRole(HttpContext, "Participant"))
        {
            if (enrolment.ParticipantId != userId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "You can view only your own enrolments."
                });
            }
        }
        else if (SessionAuthorization.HasRole(HttpContext, "Organiser"))
        {
            var ownsEvent = await _context.Events
                .AnyAsync(e =>
                    e.EventId == enrolment.EventId &&
                    e.OrganiserId == userId);

            if (!ownsEvent)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "You can view enrolments only for your own events."
                });
            }
        }
        else
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You do not have permission to view this enrolment."
            });
        }

        return Ok(enrolment);
    }

    /// <summary>
    /// Lists enrolments belonging to the logged-in participant.
    /// </summary>
    /// <returns>The participant's enrolments, ordered from newest to oldest.</returns>
    /// <response code="200">The participant's enrolments were retrieved successfully.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not a participant.</response>
    [HttpGet]
    [HttpGet("mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEnrolments()
    {
        if (!SessionAuthorization.IsAuthenticated(HttpContext))
        {
            return Unauthorized(new
            {
                message = "Please log in to access this endpoint."
            });
        }

        if (!SessionAuthorization.HasRole(HttpContext, "Participant"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Only Participants can view their enrolments here."
            });
        }

        var participantId = HttpContext.Session.GetInt32("UserId");

        if (!participantId.HasValue)
        {
            return Unauthorized(new
            {
                message = "Your session is invalid. Please log in again."
            });
        }

        var enrolments = await _context.Enrolments
            .AsNoTracking()
            .Where(e => e.ParticipantId == participantId.Value)
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new
            {
                e.EnrolmentId,
                e.EventId,
                EventName = e.Event != null
                    ? e.Event.EventName
                    : "Unknown Event",
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

    /// <summary>
    /// Deletes an enrolment belonging to the logged-in participant if no result has been recorded.
    /// </summary>
    /// <param name="enrolmentId">The ID of the enrolment to delete.</param>
    /// <returns>No response body when deletion succeeds.</returns>
    /// <response code="204">The enrolment was deleted successfully.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not a participant or does not own the enrolment.</response>
    /// <response code="404">The enrolment does not exist.</response>
    /// <response code="409">The enrolment cannot be deleted because a result has been recorded.</response>
    [HttpDelete("{enrolmentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEnrolment(int enrolmentId)
    {
        if (!SessionAuthorization.IsAuthenticated(HttpContext))
        {
            return Unauthorized(new
            {
                message = "Please log in to access this endpoint."
            });
        }

        if (!SessionAuthorization.HasRole(HttpContext, "Participant"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Only Participants can delete enrolments."
            });
        }

        var participantId = HttpContext.Session.GetInt32("UserId");

        if (!participantId.HasValue)
        {
            return Unauthorized(new
            {
                message = "Your session is invalid. Please log in again."
            });
        }

        var enrolment = await _context.Enrolments
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);

        if (enrolment == null)
        {
            return NotFound(new
            {
                message = "Enrolment not found."
            });
        }

        if (enrolment.ParticipantId != participantId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can delete only your own enrolments."
            });
        }

        var hasResult = await _context.Results
            .AnyAsync(r => r.EnrolmentId == enrolmentId);

        if (hasResult)
        {
            return Conflict(new
            {
                message = "This enrolment cannot be deleted because a result has already been recorded."
            });
        }

        _context.Enrolments.Remove(enrolment);
        await _context.SaveChangesAsync();

        // Successful deletion returns 204 No Content.
        return NoContent();
    }
}

/// <summary>
/// Request body for updating an enrolment status.
/// </summary>
public class UpdateEnrolmentStatusRequest
{
    /// <summary>
    /// The new status. Allowed values are Pending and Confirmed.
    /// </summary>
    public string? EnrolmentStatus { get; set; }
}