namespace EventHub.API.Models;

public class Feedback
{
    public int IdFeedback { get; set; }
    public int IdEvent { get; set; }
    public int IdParticipation { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Event Event { get; set; } = null!;
    public Participation Participation { get; set; } = null!;
}