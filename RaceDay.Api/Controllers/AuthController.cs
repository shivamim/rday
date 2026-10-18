
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Provides account registration, login, and logout operations.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthController(
        RaceDayDbContext context,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// Registers a new account with the Participant or Organiser role.
    /// </summary>
    /// <param name="request">
    /// The new user's name, email, password, optional phone number, and role.
    /// </param>
    /// <returns>
    /// The created account details, or an error if the request is invalid,
    /// the email is already registered, or the role is not configured.
    /// </returns>
    /// <response code="201">The account was created successfully.</response>
    /// <response code="400">The request contains invalid information.</response>
    /// <response code="409">An account with this email already exists.</response>
    /// <response code="500">The requested role is not configured.</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var roleName = request.RoleName.Trim();

        if (string.IsNullOrWhiteSpace(firstName) ||
            string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new
            {
                message = "First name, last name, and email are required."
            });
        }

        if (!string.Equals(
                roleName,
                "Participant",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                roleName,
                "Organiser",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "RoleName must be Participant or Organiser."
            });
        }

        if (await _context.Users.AnyAsync(user => user.Email == email))
        {
            return Conflict(new
            {
                message = "An account with this email already exists."
            });
        }

        var role = await _context.Roles.SingleOrDefaultAsync(
            item => item.RoleName == roleName);

        if (role is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = $"The {roleName} role is not configured."
            });
        }

        var user = new User
        {
            RoleId = role.RoleId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? null
                : request.PhoneNumber.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.Password);

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The database unique constraint also protects against
            // duplicate email registrations arriving simultaneously.
            if (await _context.Users.AnyAsync(
                    existing => existing.Email == email))
            {
                return Conflict(new
                {
                    message = "An account with this email already exists."
                });
            }

            throw;
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "Account created successfully.",
            user.UserId,
            user.FirstName,
            user.LastName,
            user.Email,
            Role = role.RoleName
        });
    }

    /// <summary>
    /// Authenticates a user and creates a session.
    /// </summary>
    /// <param name="request">The user's registered email and password.</param>
    /// <returns>
    /// The authenticated user's basic account details and role.
    /// </returns>
    /// <response code="200">Login succeeded and a session was created.</response>
    /// <response code="400">The request contains invalid information.</response>
    /// <response code="401">The credentials are invalid or the account role is unavailable.</response>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .SingleOrDefaultAsync(account => account.Email == email);

        if (user is null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        PasswordVerificationResult verification;

        try
        {
            verification = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);
        }
        catch (FormatException)
        {
            // Legacy or malformed password values are not valid hashes.
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                request.Password);

            await _context.SaveChangesAsync();
        }

        var role = await _context.Roles
            .Where(item => item.RoleId == user.RoleId)
            .Select(item => item.RoleName)
            .SingleOrDefaultAsync();

        if (role is null)
        {
            return Unauthorized(new
            {
                message = "The account role is not configured."
            });
        }

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32("UserId", user.UserId);
        HttpContext.Session.SetString("Role", role);

        return Ok(new
        {
            message = "Login successful.",
            user.UserId,
            user.FirstName,
            user.LastName,
            user.Email,
            Role = role
        });
    }

    /// <summary>
    /// Ends the current session by clearing its session data.
    /// </summary>
    /// <returns>A confirmation that logout succeeded.</returns>
    /// <response code="200">The session was cleared successfully.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        return Ok(new
        {
            message = "Logout successful."
        });
    }
}
