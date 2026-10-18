
using System.ComponentModel.DataAnnotations;
using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Provides profile retrieval, profile updates, and profile picture uploads for authenticated users.
/// </summary>
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _context;
    private readonly IAzureBlobStorageService _blobStorage;

    public UsersController(
        RaceDayDbContext context,
        IAzureBlobStorageService blobStorage)
    {
        _context = context;
        _blobStorage = blobStorage;
    }

    /// <summary>
    /// Retrieves the profile of the currently authenticated user.
    /// </summary>
    /// <returns>The user's profile details and role.</returns>
    /// <response code="200">The profile was retrieved successfully.</response>
    /// <response code="401">The user is not authenticated or the session is invalid.</response>
    /// <response code="404">The user profile was not found.</response>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile()
    {
        if (!SessionAuthorization.IsAuthenticated(HttpContext))
        {
            return Unauthorized(new
            {
                message = "Please log in first."
            });
        }

        var userId = HttpContext.Session.GetInt32("UserId");

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.UserId,
                u.RoleId,
                u.FirstName,
                u.LastName,
                u.Email,
                u.PhoneNumber,
                u.ProfilePictureUrl,
                u.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new
            {
                message = "User profile not found."
            });
        }

        var roleName = await _context.Roles
            .Where(r => r.RoleId == user.RoleId)
            .Select(r => r.RoleName)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            user.UserId,
            user.FirstName,
            user.LastName,
            user.Email,
            user.PhoneNumber,
            user.ProfilePictureUrl,
            user.CreatedAt,
            Role = roleName
        });
    }

    /// <summary>
    /// Updates the profile details of the currently authenticated user.
    /// </summary>
    /// <param name="request">The new first name, last name, and optional phone number.</param>
    /// <returns>A confirmation message and the updated profile.</returns>
    /// <response code="200">The profile was updated successfully.</response>
    /// <response code="400">The supplied profile details failed validation.</response>
    /// <response code="401">The user is not authenticated or the session is invalid.</response>
    /// <response code="404">The user profile was not found.</response>
    [HttpPut("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateMyProfileRequest request)
    {
        if (!SessionAuthorization.IsAuthenticated(HttpContext))
        {
            return Unauthorized(new
            {
                message = "Please log in first."
            });
        }

        var userId = HttpContext.Session.GetInt32("UserId");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User profile not found."
            });
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();

        await _context.SaveChangesAsync();

        var roleName = await _context.Roles
            .Where(r => r.RoleId == user.RoleId)
            .Select(r => r.RoleName)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            message = "Profile updated successfully.",
            user = new
            {
                user.UserId,
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                user.ProfilePictureUrl,
                user.CreatedAt,
                Role = roleName
            }
        });
    }

    /// <summary>
    /// Uploads a profile picture for the currently authenticated user to Azure Blob Storage.
    /// </summary>
    /// <param name="request">The image file to upload as multipart form data.</param>
    /// <param name="cancellationToken">A token used to cancel the upload operation.</param>
    /// <returns>A confirmation message and the stored profile picture URL.</returns>
    /// <response code="200">The profile picture was uploaded and its URL saved successfully.</response>
    /// <response code="400">The image file is missing, invalid, or unsupported.</response>
    /// <response code="401">The user is not authenticated or the session is invalid.</response>
    /// <response code="404">The user profile was not found.</response>
    /// <response code="503">Azure Blob Storage is not configured or is temporarily unavailable.</response>
    [HttpPost("me/profile-picture")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadProfilePicture(
        [FromForm] ProfilePictureUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (!SessionAuthorization.IsAuthenticated(HttpContext))
        {
            return Unauthorized(new
            {
                message = "Please log in first."
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

        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => u.UserId == userId.Value,
                cancellationToken);

        if (user == null)
        {
            return NotFound(new
            {
                message = "User profile not found."
            });
        }

        try
        {
            AzureBlobStorageService.ValidateImage(request.File);

            var imageUrl = await _blobStorage.UploadImageAsync(
                request.File,
                "profile-pictures",
                cancellationToken);

            user.ProfilePictureUrl = imageUrl;

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = "Profile picture uploaded successfully.",
                user.UserId,
                user.ProfilePictureUrl
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
/// Contains the profile fields a user can update.
/// </summary>
public class UpdateMyProfileRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Phone]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// Contains the image file submitted for a profile picture upload.
/// </summary>
public class ProfilePictureUploadRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;
}
