using EventHub.API.Data;
using EventHub.API.Models;
using EventHub.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvitationController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IInvitationPdfService _pdfService;

    public InvitationController(AppDbContext context, IInvitationPdfService pdfService)
    {
        _context = context;
        _pdfService = pdfService;
    }

    // POST: api/Invitation/generate/5
    [HttpPost("generate/{participationId}")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> GeneratePass(int participationId)
    {
        var pt = await _context.Participations
            .Include(p => p.Event)
                .ThenInclude(e => e.Company)
            .Include(p => p.Person)
            .FirstOrDefaultAsync(p => p.IdParticipation == participationId);

        if (pt == null) return NotFound("Participation record not found.");

        // Check if invitation already exists
        var existingInv = await _context.Invitations
            .FirstOrDefaultAsync(i => i.IdParticipation == participationId);

        if (existingInv != null)
        {
            return Ok(new
            {
                message = "Invitation already exists.",
                qrCode = existingInv.QRCode,
                pdfPath = existingInv.PDFPath,
                programPath = existingInv.ProgramPath,
                template = existingInv.Template,
                sentEmail = existingInv.SentEmail,
                sentWhatsApp = existingInv.SentWhatsApp,
                createdAt = existingInv.CreatedAt
            });
        }

        string companyName = pt.Event.Company.Name;

        // Generate unique QR payload and PDF pass
        string qrPayload = $"EVENTHUB-EVT{pt.IdEvent}-PRSN{pt.IdPerson}-{Guid.NewGuid():N}";
        string pdfPath = _pdfService.GenerateInvitationPdf(
            pt.IdParticipation,
            pt.Event.Title,
            companyName,
            pt.Event.Date,
            pt.Event.StartTime,
            pt.Event.Address,
            $"{pt.Person.FirstName} {pt.Person.LastName}",
            pt.Person.Email,
            qrPayload
        );

        var invitation = new Invitation
        {
            IdParticipation = participationId,
            QRCode = qrPayload,
            PDFPath = pdfPath,
            ProgramPath = pt.Event.ProgramPath ?? string.Empty,
            Template = "Standard",
            SentEmail = false,
            SentWhatsApp = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Invitations.Add(invitation);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Pass generated successfully!",
            idInvitation = invitation.IdInvitation,
            qrCode = invitation.QRCode,
            pdfPath = invitation.PDFPath,
            programPath = invitation.ProgramPath,
            template = invitation.Template,
            sentEmail = invitation.SentEmail,
            sentWhatsApp = invitation.SentWhatsApp,
            createdAt = invitation.CreatedAt
        });
    }

    // GET: api/Invitation/participation/5
    [HttpGet("participation/{participationId}")]
    [Authorize] // Accessible to any logged-in user (Attendees, Organisers, Admins)
    public async Task<IActionResult> GetByParticipationId(int participationId)
    {
        var inv = await _context.Invitations
            .Include(i => i.Participation)
                .ThenInclude(p => p.Event)
            .Include(i => i.Participation)
                .ThenInclude(p => p.Person)
            .FirstOrDefaultAsync(i => i.IdParticipation == participationId);

        if (inv == null)
            return NotFound("No invitation pass found for this participation record.");

        return Ok(new
        {
            idInvitation = inv.IdInvitation,
            idParticipation = inv.IdParticipation,
            eventTitle = inv.Participation.Event.Title,
            attendeeName = $"{inv.Participation.Person.FirstName} {inv.Participation.Person.LastName}",
            qrCode = inv.QRCode,
            pdfPath = inv.PDFPath,
            programPath = inv.ProgramPath,
            template = inv.Template,
            sentEmail = inv.SentEmail,
            emailSentDate = inv.EmailSentDate,
            sentWhatsApp = inv.SentWhatsApp,
            whatsAppSentDate = inv.WhatsAppSentDate,
            createdAt = inv.CreatedAt
        });
    }

    // PUT: api/Invitation/5/email-status
    [HttpPut("{invitationId}/email-status")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> UpdateEmailStatus(int invitationId, [FromQuery] bool sent)
    {
        var inv = await _context.Invitations.FindAsync(invitationId);
        if (inv == null) return NotFound("Invitation record not found.");

        inv.SentEmail = sent;
        inv.EmailSentDate = sent ? DateTime.UtcNow : null;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Email dispatch status updated successfully.",
            idInvitation = inv.IdInvitation,
            sentEmail = inv.SentEmail,
            emailSentDate = inv.EmailSentDate
        });
    }

    // PUT: api/Invitation/5/whatsapp-status
    [HttpPut("{invitationId}/whatsapp-status")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> UpdateWhatsAppStatus(int invitationId, [FromQuery] bool sent)
    {
        var inv = await _context.Invitations.FindAsync(invitationId);
        if (inv == null) return NotFound("Invitation record not found.");

        inv.SentWhatsApp = sent;
        inv.WhatsAppSentDate = sent ? DateTime.UtcNow : null;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "WhatsApp dispatch status updated successfully.",
            idInvitation = inv.IdInvitation,
            sentWhatsApp = inv.SentWhatsApp,
            whatsAppSentDate = inv.WhatsAppSentDate
        });
    }
}