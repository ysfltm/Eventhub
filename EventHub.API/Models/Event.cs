namespace EventHub.API.Models;

public class Event
{
    public int IdEvent { get; set; }
    public int IdCompany { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string? Person { get; set; }
    public string? ProgramPath { get; set; }

    public int Capacity { get; set; } = 100;
    public Company Company { get; set; } = null!;
    public ICollection<Participation> Participations { get; set; } = new List<Participation>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}