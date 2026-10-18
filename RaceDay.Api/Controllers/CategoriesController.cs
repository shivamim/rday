
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Provides public category browsing and organiser category management.
/// </summary>
[ApiController]
[Route("api")]
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _context;

    public CategoriesController(RaceDayDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lists the categories belonging to a specified event.
    /// </summary>
    /// <param name="eventId">The ID of the event whose categories are requested.</param>
    /// <returns>The event's categories, including their age or distance criteria.</returns>
    /// <response code="200">The categories were retrieved successfully.</response>
    /// <response code="404">The specified event does not exist.</response>
    [HttpGet("events/{eventId:int}/categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategories(int eventId)
    {
        var eventExists = await _context.Events
            .AnyAsync(e => e.EventId == eventId);

        if (!eventExists)
        {
            return NotFound(new { message = "Event not found." });
        }

        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.CategoryName)
            .Select(c => new
            {
                c.CategoryId,
                c.EventId,
                c.CategoryName,
                c.CategoryType,
                c.MinAge,
                c.MaxAge,
                c.CategoryDistanceKm
            })
            .ToListAsync();

        return Ok(categories);
    }

    /// <summary>
    /// Retrieves the details of a single category.
    /// </summary>
    /// <param name="categoryId">The ID of the category to retrieve.</param>
    /// <returns>The category's details and eligibility criteria.</returns>
    /// <response code="200">The category was retrieved successfully.</response>
    /// <response code="404">The category does not exist.</response>
    [HttpGet("categories/{categoryId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategory(int categoryId)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .Where(c => c.CategoryId == categoryId)
            .Select(c => new
            {
                c.CategoryId,
                c.EventId,
                c.CategoryName,
                c.CategoryType,
                c.MinAge,
                c.MaxAge,
                c.CategoryDistanceKm
            })
            .FirstOrDefaultAsync();

        if (category == null)
        {
            return NotFound(new
            {
                message = "Category not found."
            });
        }

        return Ok(category);
    }

    /// <summary>
    /// Creates a category for an event owned by the logged-in organiser.
    /// </summary>
    /// <param name="eventId">The ID of the event receiving the category.</param>
    /// <param name="request">
    /// The category name, type, and applicable age or distance criteria.
    /// </param>
    /// <returns>The newly created category.</returns>
    /// <response code="201">The category was created successfully.</response>
    /// <response code="400">The category details are invalid.</response>
    /// <response code="401">The user is not logged in.</response>
    /// <response code="403">The user is not an organiser or does not own the event.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="409">A category with this name already exists for the event.</response>
    [HttpPost("events/{eventId:int}/categories")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory(
        int eventId,
        [FromBody] CreateCategoryRequest request)
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
                message = "Only Organisers can create categories."
            });
        }

        var organiserId = HttpContext.Session.GetInt32("UserId");

        var eventItem = await _context.Events
            .FirstOrDefaultAsync(e => e.EventId == eventId);

        if (eventItem == null)
        {
            return NotFound(new { message = "Event not found." });
        }

        if (eventItem.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can manage categories only for your own events."
            });
        }

        var validationError = ValidateCategory(request);

        if (validationError != null)
        {
            return BadRequest(new { message = validationError });
        }

        var categoryName = request.CategoryName.Trim();

        var duplicate = await _context.Categories.AnyAsync(c =>
            c.EventId == eventId &&
            c.CategoryName == categoryName);

        if (duplicate)
        {
            return Conflict(new
            {
                message = "A category with this name already exists for the event."
            });
        }

        var category = new Category
        {
            EventId = eventId,
            CategoryName = categoryName,
            CategoryType = request.CategoryType,
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            CategoryDistanceKm = request.CategoryDistanceKm
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return Created(
            $"/api/categories/{category.CategoryId}",
            new
            {
                category.CategoryId,
                category.EventId,
                category.CategoryName,
                category.CategoryType,
                category.MinAge,
                category.MaxAge,
                category.CategoryDistanceKm
            });
    }

    /// <summary>
    /// Updates a category belonging to an event owned by the logged-in organiser.
    /// </summary>
    /// <param name="categoryId">The ID of the category to update.</param>
    /// <param name="request">
    /// The replacement category name, type, and applicable age or distance criteria.
    /// </param>
    /// <returns>The updated category details.</returns>
    /// <response code="200">The category was updated successfully.</response>
    /// <response code="400">The category details are invalid.</response>
    /// <response code="401">The user is not logged in.</response>
    /// <response code="403">The user is not an organiser or does not own the event.</response>
    /// <response code="404">The category or its associated event does not exist.</response>
    /// <response code="409">Another category for the event already uses this name.</response>
    [HttpPut("categories/{categoryId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategory(
        int categoryId,
        [FromBody] CreateCategoryRequest request)
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
                message = "Only Organisers can update categories."
            });
        }

        var organiserId = HttpContext.Session.GetInt32("UserId");

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

        if (category == null)
        {
            return NotFound(new { message = "Category not found." });
        }

        var eventItem = await _context.Events
            .FirstOrDefaultAsync(e => e.EventId == category.EventId);

        if (eventItem == null)
        {
            return NotFound(new
            {
                message = "Associated event not found."
            });
        }

        if (eventItem.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can manage categories only for your own events."
            });
        }

        var validationError = ValidateCategory(request);

        if (validationError != null)
        {
            return BadRequest(new { message = validationError });
        }

        var categoryName = request.CategoryName.Trim();

        var duplicate = await _context.Categories.AnyAsync(c =>
            c.EventId == category.EventId &&
            c.CategoryName == categoryName &&
            c.CategoryId != categoryId);

        if (duplicate)
        {
            return Conflict(new
            {
                message = "A category with this name already exists for the event."
            });
        }

        category.CategoryName = categoryName;
        category.CategoryType = request.CategoryType;
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.CategoryDistanceKm = request.CategoryDistanceKm;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Category updated successfully.",
            category.CategoryId,
            category.EventId,
            category.CategoryName,
            category.CategoryType,
            category.MinAge,
            category.MaxAge,
            category.CategoryDistanceKm
        });
    }

    /// <summary>
    /// Deletes a category if no enrolments use it.
    /// </summary>
    /// <param name="categoryId">The ID of the category to delete.</param>
    /// <returns>A confirmation that the category was deleted.</returns>
    /// <response code="204">The category was deleted successfully.</response>
    /// <response code="401">The user is not logged in.</response>
    /// <response code="403">The user is not an organiser or does not own the event.</response>
    /// <response code="404">The category or its associated event does not exist.</response>
    /// <response code="409">The category cannot be deleted because enrolments exist.</response>
    [HttpDelete("categories/{categoryId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int categoryId)
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
                message = "Only Organisers can delete categories."
            });
        }

        var organiserId = HttpContext.Session.GetInt32("UserId");

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

        if (category == null)
        {
            return NotFound(new { message = "Category not found." });
        }

        var eventItem = await _context.Events
            .FirstOrDefaultAsync(e => e.EventId == category.EventId);

        if (eventItem == null)
        {
            return NotFound(new
            {
                message = "Associated event not found."
            });
        }

        if (eventItem.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can manage categories only for your own events."
            });
        }

        var hasEnrolments = await _context.Enrolments
            .AnyAsync(e => e.CategoryId == categoryId);

        if (hasEnrolments)
        {
            return Conflict(new
            {
                message = "This category cannot be deleted because enrolments already exist."
            });
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static string? ValidateCategory(CreateCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CategoryName))
        {
            return "CategoryName is required.";
        }

        // Reject unsupported types before reaching the database.
        if (request.CategoryType != "Age" &&
            request.CategoryType != "Distance")
        {
            return "CategoryType must be Age or Distance.";
        }

        if (request.CategoryType == "Age")
        {
            if (!request.MinAge.HasValue || !request.MaxAge.HasValue)
            {
                return "Age categories require both MinAge and MaxAge.";
            }

            if (request.MinAge.Value > request.MaxAge.Value)
            {
                return "MinAge cannot be greater than MaxAge.";
            }

            if (request.CategoryDistanceKm.HasValue)
            {
                return "Age categories must not specify CategoryDistanceKm.";
            }
        }
        else
        {
            if (!request.CategoryDistanceKm.HasValue ||
                request.CategoryDistanceKm.Value <= 0)
            {
                return "Distance categories require a positive CategoryDistanceKm.";
            }

            if (request.MinAge.HasValue || request.MaxAge.HasValue)
            {
                return "Distance categories must not specify age limits.";
            }
        }

        return null;
    }
}
