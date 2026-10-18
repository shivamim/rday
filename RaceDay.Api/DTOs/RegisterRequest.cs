
using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class RegisterRequest
{
    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Required]
    [RegularExpression(
        "^(Participant|Organiser)$",
        ErrorMessage = "RoleName must be Participant or Organiser.")]
    public string RoleName { get; set; } = string.Empty;
}
