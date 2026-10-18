
using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class CreateEventEnrolmentRequest
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}
