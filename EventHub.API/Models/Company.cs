namespace EventHub.API.Models;

public class Company
{
    public int IdCompany { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Expertise { get; set; }
    public string? Logo { get; set; }
    public string? Website { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    

    public ICollection<Event> Events { get; set; } = new List<Event>();
}