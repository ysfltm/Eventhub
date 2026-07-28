namespace EventHub.API.Models;

public class Person
{
    public int IdPerson { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? CompanyName { get; set; }
    public string? Position { get; set; }
    
    public string? PasswordHash { get; set; } // Nullable until account activated
    
    public PersonRole Role { get; set; } = PersonRole.Attendee; // SuperAdmin | EventOrganiser | Attendee
    
    public bool IsAccountActivated { get; set; } = false;

    public ICollection<Participation> Participations { get; set; } = new List<Participation>();
}