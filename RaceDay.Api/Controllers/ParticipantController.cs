
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Provides dashboard information for authenticated participants.
/// </summary>
[ApiController]
[Route("api/participant")]
public class ParticipantController : ControllerBase
{
    /// <summary>
    /// Retrieves the dashboard information for the logged-in participant.
    /// </summary>
    /// <returns>A confirmation message, the participant's user ID, and their role.</returns>
    /// <response code="200">The participant dashboard was accessed successfully.</response>
    /// <response code="401">The user is not logged in or the session is invalid.</response>
    /// <response code="403">The user is not a participant.</response>
    [HttpGet("dashboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetDashboard()
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
                message = "Only Participants can access this endpoint."
            });
        }

        var userId = HttpContext.Session.GetInt32("UserId");

        return Ok(new
        {
            message = "Participant dashboard accessed successfully.",
            userId,
            role = "Participant"
        });
    }
}
