
using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class CreateCategoryRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string CategoryName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(
        "^(Age|Distance)$",
        ErrorMessage = "CategoryType must be Age or Distance.")]
    public string CategoryType { get; set; } = string.Empty;

    [Range(0, 120)]
    public int? MinAge { get; set; }

    [Range(0, 120)]
    public int? MaxAge { get; set; }

    [Range(typeof(decimal), "0.01", "9999.99")]
    public decimal? CategoryDistanceKm { get; set; }
}
