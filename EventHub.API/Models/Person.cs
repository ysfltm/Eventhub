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
    public int? IdCompany { get; set; }
    public string? PasswordHash { get; set; } // Nullable until account activated
    
    public PersonRole Role { get; set; } = PersonRole.Attendee; // SuperAdmin | EventOrganiser | Attendee
    
    public bool IsAccountActivated { get; set; } = false;
    
    
    public string? PasswordResetToken { get; set; }
    public DateTime? ResetTokenExpiresAt { get; set; }
    public string? LinkedInUrl { get; set; }
    public Company? Company { get; set; } 
    public ICollection<Participation> Participations { get; set; } = new List<Participation>();
}