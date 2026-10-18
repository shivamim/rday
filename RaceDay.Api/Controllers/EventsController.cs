using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _context;
    private readonly IAzureBlobStorageService _blobStorage;

    public EventsController(
        RaceDayDbContext context,
        IAzureBlobStorageService blobStorage)
    {
        _context = context;
        _blobStorage = blobStorage;
    }

    /// <summary>
    /// Lists upcoming events, optionally filtered by event type, date, or location, with each event's enrolment count.
    /// </summary>
    /// <param name="eventType">Optional event type used to filter the results.</param>
    /// <param name="eventDate">Optional event date used to filter the results.</param>
    /// <param name="location">Optional location text used to filter the results.</param>
    /// <returns>The matching upcoming events, ordered by event date.</returns>
    /// <response code="200">The events were retrieved successfully.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? eventType,
        [FromQuery] DateTime? eventDate,
        [FromQuery] string? location)
    {
        var today = DateTime.Today;

        var query = _context.Events
            .AsNoTracking()
            .Where(e => e.EventDate.Date >= today);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var requestedEventType = eventType.Trim();

            query = query.Where(e =>
                e.EventType == requestedEventType);
        }

        if (eventDate.HasValue)
        {
            var requestedDate = eventDate.Value.Date;

            query = query.Where(e =>
                e.EventDate.Date == requestedDate);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var requestedLocation = location.Trim();

            query = query.Where(e =>
                e.Location.Contains(requestedLocation));
        }

        var events = await query
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
                e.OrganiserId,
                e.BannerImageUrl,
                EnrolmentCount = _context.Enrolments.Count(
                    enrolment => enrolment.EventId == e.EventId)
            })
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// Retrieves the details of a specific event.
    /// </summary>
    /// <param name="id">The ID of the event to retrieve.</param>
    /// <returns>The event details.</returns>
    /// <response code="200">The event was retrieved successfully.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int id)
    {
        var eventItem = await _context.Events
            .AsNoTracking()
            .Where(e => e.EventId == id)
            .Select(e => new
            {
                e.EventId,
                e.EventName,
                e.Description,
                e.EventDate,
                e.Location,
                e.DistanceKm,
                e.EventType,
                e.OrganiserId,
                e.BannerImageUrl,
                EnrolmentCount = _context.Enrolments.Count(
                    enrolment => enrolment.EventId == e.EventId)
            })
            .FirstOrDefaultAsync();

        if (eventItem == null)
        {
            return NotFound(new
            {
                message = "Event not found."
            });
        }

        return Ok(eventItem);
    }

    /// <summary>
    /// Creates an event for the logged-in organiser.
    /// </summary>
    /// <param name="request">The event name, description, date, location, distance, and event type.</param>
    /// <returns>The newly created event.</returns>
    /// <response code="201">The event was created successfully.</response>
    /// <response code="400">The event details are invalid or the event date is in the past.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not an organiser.</response>
    /// <response code="409">An event with the same name already exists.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateEvent(
        [FromBody] CreateEventRequest request)
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
                message = "Only Organisers can create events."
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

        if (request.EventDate.Date < DateTime.Today)
        {
            return BadRequest(new
            {
                message = "EventDate cannot be in the past."
            });
        }

        var eventName = request.EventName.Trim();

        var duplicateExists = await _context.Events
            .AnyAsync(e => e.EventName == eventName);

        if (duplicateExists)
        {
            return Conflict(new
            {
                message = "An event with this name already exists."
            });
        }

        var eventItem = new Event
        {
            OrganiserId = organiserId.Value,
            EventName = eventName,
            Description = request.Description.Trim(),
            EventDate = request.EventDate.Date,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm,
            EventType = request.EventType,
            CreatedAt = DateTime.Now
        };

        _context.Events.Add(eventItem);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Recheck in case another request created the same name
            // after the initial duplicate check.
            var duplicateAfterSave = await _context.Events
                .AsNoTracking()
                .AnyAsync(e => e.EventName == eventName);

            if (duplicateAfterSave)
            {
                return Conflict(new
                {
                    message = "An event with this name already exists."
                });
            }

            throw;
        }

        return CreatedAtAction(
            nameof(GetEvent),
            new { id = eventItem.EventId },
            new
            {
                eventItem.EventId,
                eventItem.EventName,
                eventItem.Description,
                eventItem.EventDate,
                eventItem.Location,
                eventItem.DistanceKm,
                eventItem.EventType,
                eventItem.OrganiserId,
                eventItem.BannerImageUrl,
                eventItem.CreatedAt,
                EnrolmentCount = 0
            });
    }

    /// <summary>
    /// Updates an event owned by the logged-in organiser.
    /// </summary>
    /// <param name="id">The ID of the event to update.</param>
    /// <param name="request">The replacement event name, description, date, location, distance, and event type.</param>
    /// <returns>A confirmation and the updated event details.</returns>
    /// <response code="200">The event was updated successfully.</response>
    /// <response code="400">The event date is in the past.</response>
    /// <response code="401">The user is not logged in.</response>
    /// <response code="403">The user is not an organiser or does not own the event.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="409">Another event already uses this name.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateEvent(
        int id,
        [FromBody] CreateEventRequest request)
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
                message = "Only Organisers can update events."
            });
        }

        var organiserId = HttpContext.Session.GetInt32("UserId");

        var eventItem = await _context.Events
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (eventItem == null)
        {
            return NotFound(new
            {
                message = "Event not found."
            });
        }

        if (eventItem.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can update only your own events."
            });
        }

        if (request.EventDate.Date < DateTime.Today)
        {
            return BadRequest(new
            {
                message = "EventDate cannot be in the past."
            });
        }

        var eventName = request.EventName.Trim();

        var duplicateExists = await _context.Events
            .AnyAsync(e =>
                e.EventId != id &&
                e.EventName == eventName);

        if (duplicateExists)
        {
            return Conflict(new
            {
                message = "An event with this name already exists."
            });
        }

        eventItem.EventName = eventName;
        eventItem.Description = request.Description.Trim();
        eventItem.EventDate = request.EventDate.Date;
        eventItem.Location = request.Location.Trim();
        eventItem.DistanceKm = request.DistanceKm;
        eventItem.EventType = request.EventType;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Handle a concurrent duplicate-name conflict without
            // disguising unrelated database errors.
            var duplicateAfterSave = await _context.Events
                .AsNoTracking()
                .AnyAsync(e =>
                    e.EventId != id &&
                    e.EventName == eventName);

            if (duplicateAfterSave)
            {
                return Conflict(new
                {
                    message = "An event with this name already exists."
                });
            }

            throw;
        }

        return Ok(new
        {
            message = "Event updated successfully.",
            eventItem.EventId,
            eventItem.EventName,
            eventItem.Description,
            eventItem.EventDate,
            eventItem.Location,
            eventItem.DistanceKm,
            eventItem.EventType,
            eventItem.OrganiserId,
            eventItem.BannerImageUrl
        });
    }

    /// <summary>
    /// Deletes an event owned by the logged-in organiser when it has no categories or enrolments.
    /// </summary>
    /// <param name="id">The ID of the event to delete.</param>
    /// <returns>No response body when deletion succeeds.</returns>
    /// <response code="204">The event was deleted successfully.</response>
    /// <response code="401">The user is not logged in.</response>
    /// <response code="403">The user is not an organiser or does not own the event.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="409">The event still has categories or enrolments.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
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
                message = "Only Organisers can delete events."
            });
        }

        var organiserId = HttpContext.Session.GetInt32("UserId");

        var eventItem = await _context.Events
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (eventItem == null)
        {
            return NotFound(new
            {
                message = "Event not found."
            });
        }

        if (eventItem.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can delete only your own events."
            });
        }

        var hasCategories = await _context.Categories
            .AnyAsync(c => c.EventId == id);

        var hasEnrolments = await _context.Enrolments
            .AnyAsync(e => e.EventId == id);

        if (hasCategories || hasEnrolments)
        {
            return Conflict(new
            {
                message =
                    "This event cannot be deleted because it has categories or enrolments. " +
                    "Remove the related records first."
            });
        }

        _context.Events.Remove(eventItem);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Uploads a banner image for an event owned by the logged-in organiser.
    /// </summary>
    /// <param name="eventId">The ID of the event receiving the banner.</param>
    /// <param name="request">The image file to upload as multipart form data. The maximum request size is 6 MB.</param>
    /// <param name="cancellationToken">A token that can cancel the upload operation.</param>
    /// <returns>The event ID and URL of the uploaded banner.</returns>
    /// <response code="200">The banner was uploaded successfully.</response>
    /// <response code="400">The file is missing, invalid, or not an accepted image.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not an organiser or does not own the event.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="503">Image storage is not configured or is temporarily unavailable.</response>
    [HttpPost("{eventId:int}/banner")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadEventBanner(
        int eventId,
        [FromForm] EventBannerUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (!SessionAuthorization.IsAuthenticated(HttpContext))
        {
            return Unauthorized(new
            {
                message = "Please log in to upload an event banner."
            });
        }

        if (!SessionAuthorization.HasRole(HttpContext, "Organiser"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Only Organisers can upload event banners."
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
            .FirstOrDefaultAsync(
                e => e.EventId == eventId,
                cancellationToken);

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
                message = "You can upload banners only for your own events."
            });
        }

        try
        {
            AzureBlobStorageService.ValidateImage(request.File);

            var imageUrl = await _blobStorage.UploadImageAsync(
                request.File,
                "event-banners",
                cancellationToken);

            eventItem.BannerImageUrl = imageUrl;

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = "Event banner uploaded successfully.",
                eventItem.EventId,
                eventItem.BannerImageUrl
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message = "Image storage is not configured. " +
                              "Configure Azure Blob Storage before uploading."
                });
        }
        catch (RequestFailedException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message = "Image storage is temporarily unavailable. " +
                              "Please try again later."
                });
        }
    }
}

/// <summary>
/// Represents the image file submitted when uploading an event banner.
/// </summary>
public class EventBannerUploadRequest
{
    /// <summary>
    /// Gets or sets the banner image file.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Required]
    public IFormFile File { get; set; } = null!;
}