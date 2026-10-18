
namespace RaceDay.Api.Models;

public class Category
{
    public int CategoryId { get; set; }

    public int EventId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public string CategoryType { get; set; } = string.Empty;

    public int? MinAge { get; set; }

    public int? MaxAge { get; set; }

    public decimal? CategoryDistanceKm { get; set; }
}
