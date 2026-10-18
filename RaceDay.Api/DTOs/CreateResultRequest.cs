
using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class CreateResultRequest
{
    [Range(1, int.MaxValue)]
    public int FinishPosition { get; set; }

    public TimeSpan FinishTime { get; set; }
}
