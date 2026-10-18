
using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class CreateEnrolmentRequest
{
    [Range(1, int.MaxValue)]
    public int EventId { get; set; }

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}
