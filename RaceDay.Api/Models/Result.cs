
namespace RaceDay.Api.Models;

public class Result
{
    public int ResultId { get; set; }

    public int EnrolmentId { get; set; }

    public int FinishPosition { get; set; }

    public TimeSpan FinishTime { get; set; }

    public DateTime RecordedAt { get; set; }

    public Enrolment? Enrolment { get; set; }
}
