
using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class CreateEventRequest
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string EventName { get; set; } = string.Empty;

    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateTime EventDate { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Location { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999.99")]
    public decimal DistanceKm { get; set; }

    [Required]
    [RegularExpression(
        "^(Run|Walk|Cycle)$",
        ErrorMessage = "EventType must be Run, Walk, or Cycle.")]
    public string EventType { get; set; } = string.Empty;
}
