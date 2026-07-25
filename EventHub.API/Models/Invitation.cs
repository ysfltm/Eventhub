namespace EventHub.API.Models;

public class Invitation
{
    public int IdInvitation { get; set; }
    public int IdParticipation { get; set; }
    public string QRCode { get; set; } = string.Empty;
    public string PDFPath { get; set; } = string.Empty;
    public string ProgramPath { get; set; } = string.Empty;
    public string Template { get; set; } = "Standard";
    public bool SentEmail { get; set; }
    public bool SentWhatsApp { get; set; }
    public DateTime? EmailSentDate { get; set; }
    public DateTime? WhatsAppSentDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Participation Participation { get; set; } = null!;
}