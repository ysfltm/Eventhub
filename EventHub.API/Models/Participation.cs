namespace EventHub.API.Models;

public class Participation
{
    public int IdParticipation { get; set; }
    public int IdEvent { get; set; }
    public int IdPerson { get; set; }
    public string Type { get; set; } = "Attendee";
    public string Status { get; set; } = "Invited";
    public DateTime InvitationDate { get; set; } = DateTime.UtcNow;
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }

    public Event Event { get; set; } = null!;
    public Person Person { get; set; } = null!;
    public Invitation? Invitation { get; set; }
    public Feedback? Feedback { get; set; }
}