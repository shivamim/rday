
namespace RaceDay.Api.Models;

public class Enrolment
{
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }

    public int EventId { get; set; }

    public int CategoryId { get; set; }

    public string EnrolmentStatus { get; set; } = "Pending";

    public DateTime EnrolledAt { get; set; }

    public User? Participant { get; set; }

    public Event? Event { get; set; }

    public Category? Category { get; set; }

    public Result? Result { get; set; }
}