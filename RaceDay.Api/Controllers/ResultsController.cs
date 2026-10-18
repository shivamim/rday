
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Provides result recording, updating, and authorised result retrieval.
/// </summary>
[ApiController]
[Route("api/results")]
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _context;

    public ResultsController(RaceDayDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Records a finish result for an enrolment in an event owned by the logged-in organiser.
    /// </summary>
    /// <param name="enrolmentId">The ID of the enrolment receiving the result.</param>
    /// <param name="request">The participant's finish position and finish time.</param>
    /// <returns>The newly recorded result.</returns>
    /// <response code="201">The result was created successfully.</response>
    /// <response code="400">The enrolment ID, finish position, finish time, or event association is invalid.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not an organiser or does not own the event.</response>
    /// <response code="404">The enrolment does not exist.</response>
    /// <response code="409">A result already exists or the result could not be saved.</response>
    [HttpPost("/api/enrolments/{enrolmentId:int}/result")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateResult(
        int enrolmentId,
        [FromBody] CreateResultRequest request)
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
                message = "Only Organisers can record results."
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

        if (enrolmentId <= 0)
        {
            return BadRequest(new
            {
                message = "Enrolment ID must be a positive number."
            });
        }

        var enrolment = await _context.Enrolments
            .AsNoTracking()
            .Where(e => e.EnrolmentId == enrolmentId)
            .Select(e => new
            {
                e.EnrolmentId,
                e.EventId,
                EventOrganiserId = (int?)e.Event!.OrganiserId
            })
            .FirstOrDefaultAsync();

        if (enrolment == null)
        {
            return NotFound(new
            {
                message = "Enrolment not found."
            });
        }

        if (!enrolment.EventOrganiserId.HasValue)
        {
            return BadRequest(new
            {
                message = "The enrolment is not associated with a valid event."
            });
        }

        if (enrolment.EventOrganiserId.Value != organiserId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can record results only for enrolments in your own events."
            });
        }

        if (!IsValidFinishTime(request.FinishTime))
        {
            return BadRequest(new
            {
                message = "FinishTime must be between 00:00:00 and 23:59:59.9999999."
            });
        }

        if (request.FinishPosition <= 0)
        {
            return BadRequest(new
            {
                message = "FinishPosition must be greater than zero."
            });
        }

        var existingResult = await _context.Results
            .AnyAsync(r => r.EnrolmentId == enrolmentId);

        if (existingResult)
        {
            return Conflict(new
            {
                message = "A result has already been recorded for this enrolment."
            });
        }

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishPosition = request.FinishPosition,
            FinishTime = request.FinishTime,
            RecordedAt = DateTime.Now
        };

        _context.Results.Add(result);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "The result could not be saved. A result may already exist for this enrolment."
            });
        }

        return Created(
            $"/api/enrolments/{result.EnrolmentId}/result",
            new
            {
                result.ResultId,
                result.EnrolmentId,
                result.FinishPosition,
                result.FinishTime,
                result.RecordedAt
            });
    }

    /// <summary>
    /// Retrieves the results belonging to the logged-in participant.
    /// </summary>
    /// <returns>The participant's results, ordered by finish position.</returns>
    /// <response code="200">The participant's results were retrieved successfully.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not a participant.</response>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyResults()
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
                message = "Only Participants can view their results here."
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

        var results = await _context.Results
            .AsNoTracking()
            .Where(r => r.Enrolment != null &&
                        r.Enrolment.ParticipantId == participantId.Value &&
                        r.Enrolment.Event != null)
            .OrderBy(r => r.FinishPosition)
            .Select(r => new
            {
                r.ResultId,
                r.EnrolmentId,
                EventId = r.Enrolment!.EventId,
                EventName = r.Enrolment.Event!.EventName,
                r.FinishPosition,
                r.FinishTime,
                r.RecordedAt
            })
            .ToListAsync();

        return Ok(results);
    }

    /// <summary>
    /// Retrieves results for an event owned by the logged-in organiser.
    /// </summary>
    /// <param name="eventId">The ID of the event whose results are requested.</param>
    /// <returns>The event results, ordered by finish position and including participant names.</returns>
    /// <response code="200">The event results were retrieved successfully.</response>
    /// <response code="400">The event ID is invalid.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not an organiser or does not own the event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("/api/events/{eventId:int}/results")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventResults(int eventId)
    {
        if (eventId <= 0)
        {
            return BadRequest(new
            {
                message = "Event ID must be a positive number."
            });
        }

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
                message = "Only Organisers can view event results."
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

        var eventOrganiserId = await _context.Events
            .AsNoTracking()
            .Where(e => e.EventId == eventId)
            .Select(e => (int?)e.OrganiserId)
            .FirstOrDefaultAsync();

        if (!eventOrganiserId.HasValue)
        {
            return NotFound(new
            {
                message = "Event not found."
            });
        }

        if (eventOrganiserId.Value != organiserId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can view results only for your own events."
            });
        }

        var results = await _context.Results
            .AsNoTracking()
            .Where(r => r.Enrolment != null &&
                        r.Enrolment.EventId == eventId &&
                        r.Enrolment.Participant != null)
            .OrderBy(r => r.FinishPosition)
            .Select(r => new
            {
                r.ResultId,
                r.EnrolmentId,
                ParticipantName =
                    r.Enrolment!.Participant!.FirstName + " " +
                    r.Enrolment.Participant.LastName,
                r.FinishPosition,
                r.FinishTime,
                r.RecordedAt
            })
            .ToListAsync();

        return Ok(results);
    }

    /// <summary>
    /// Retrieves the result for an enrolment when the requester is its participant or event organiser.
    /// </summary>
    /// <param name="enrolmentId">The ID of the enrolment whose result is requested.</param>
    /// <returns>The result details, including event information.</returns>
    /// <response code="200">The result was retrieved successfully.</response>
    /// <response code="400">The enrolment ID is invalid.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not authorised to view this result.</response>
    /// <response code="404">The enrolment or its result does not exist.</response>
    [HttpGet("/api/enrolments/{enrolmentId:int}/result")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrolmentResult(int enrolmentId)
    {
        if (!SessionAuthorization.IsAuthenticated(HttpContext))
        {
            return Unauthorized(new
            {
                message = "Please log in to access this endpoint."
            });
        }

        if (enrolmentId <= 0)
        {
            return BadRequest(new
            {
                message = "Enrolment ID must be a positive number."
            });
        }

        var userId = HttpContext.Session.GetInt32("UserId");
        var role = HttpContext.Session.GetString("Role");

        if (!userId.HasValue)
        {
            return Unauthorized(new
            {
                message = "Your session is invalid. Please log in again."
            });
        }

        var result = await _context.Results
            .AsNoTracking()
            .Where(r => r.EnrolmentId == enrolmentId)
            .Select(r => new
            {
                r.ResultId,
                r.EnrolmentId,
                r.FinishPosition,
                r.FinishTime,
                r.RecordedAt,
                ParticipantId = r.Enrolment!.ParticipantId,
                EventId = r.Enrolment.EventId,
                EventName = r.Enrolment.Event!.EventName,
                EventOrganiserId = r.Enrolment.Event.OrganiserId
            })
            .FirstOrDefaultAsync();

        if (result == null)
        {
            var enrolmentExists = await _context.Enrolments
                .AnyAsync(e => e.EnrolmentId == enrolmentId);

            if (!enrolmentExists)
            {
                return NotFound(new
                {
                    message = "Enrolment not found."
                });
            }

            return NotFound(new
            {
                message = "No result has been recorded for this enrolment."
            });
        }

        if (string.Equals(
                role,
                "Participant",
                StringComparison.OrdinalIgnoreCase))
        {
            if (result.ParticipantId != userId.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "You can view results only for your own enrolments."
                });
            }
        }
        else if (string.Equals(
                     role,
                     "Organiser",
                     StringComparison.OrdinalIgnoreCase))
        {
            if (result.EventOrganiserId != userId.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "You can view results only for your own events."
                });
            }
        }
        else
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You do not have permission to view this result."
            });
        }

        return Ok(new
        {
            result.ResultId,
            result.EnrolmentId,
            result.EventId,
            result.EventName,
            result.FinishPosition,
            result.FinishTime,
            result.RecordedAt
        });
    }

    /// <summary>
    /// Updates a recorded result for an event owned by the logged-in organiser.
    /// </summary>
    /// <param name="enrolmentId">The ID of the enrolment whose result is being updated.</param>
    /// <param name="request">The replacement finish position and finish time.</param>
    /// <returns>The updated result details.</returns>
    /// <response code="200">The result was updated successfully.</response>
    /// <response code="400">The enrolment ID, finish position, finish time, or event association is invalid.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not an organiser or does not own the event.</response>
    /// <response code="404">The enrolment or its result does not exist.</response>
    /// <response code="409">The result could not be updated.</response>
    [HttpPut("/api/enrolments/{enrolmentId:int}/result")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateEnrolmentResult(
        int enrolmentId,
        [FromBody] UpdateResultRequest request)
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
                message = "Only Organisers can update results."
            });
        }

        if (enrolmentId <= 0)
        {
            return BadRequest(new
            {
                message = "Enrolment ID must be a positive number."
            });
        }

        if (request.FinishPosition <= 0)
        {
            return BadRequest(new
            {
                message = "FinishPosition must be greater than zero."
            });
        }

        if (!IsValidFinishTime(request.FinishTime))
        {
            return BadRequest(new
            {
                message = "FinishTime must be between 00:00:00 and 23:59:59.9999999."
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

        var result = await _context.Results
            .Include(r => r.Enrolment)
                .ThenInclude(e => e!.Event)
            .FirstOrDefaultAsync(r => r.EnrolmentId == enrolmentId);

        if (result == null)
        {
            var enrolmentExists = await _context.Enrolments
                .AnyAsync(e => e.EnrolmentId == enrolmentId);

            if (!enrolmentExists)
            {
                return NotFound(new
                {
                    message = "Enrolment not found."
                });
            }

            return NotFound(new
            {
                message = "No result has been recorded for this enrolment."
            });
        }

        if (result.Enrolment?.Event == null)
        {
            return BadRequest(new
            {
                message = "The enrolment is not associated with a valid event."
            });
        }

        if (result.Enrolment.Event.OrganiserId != organiserId.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can update results only for your own events."
            });
        }

        result.FinishPosition = request.FinishPosition;
        result.FinishTime = request.FinishTime;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "The result could not be updated."
            });
        }

        return Ok(new
        {
            result.ResultId,
            result.EnrolmentId,
            result.FinishPosition,
            result.FinishTime,
            result.RecordedAt
        });
    }

    private static bool IsValidFinishTime(TimeSpan finishTime)
    {
        return finishTime >= TimeSpan.Zero &&
               finishTime < TimeSpan.FromDays(1);
    }
}
