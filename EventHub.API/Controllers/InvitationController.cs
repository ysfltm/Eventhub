using EventHub.API.Data;
using EventHub.API.Models;
using EventHub.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    [HttpPost("generate/{participationId}")]
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

        // Generate unique QR payload and PDF pass
        string qrPayload = $"EVENTHUB-EVT{pt.IdEvent}-PRSN{pt.IdPerson}-{Guid.NewGuid():N}";
        string pdfPath = _pdfService.GenerateInvitationPdf(
            pt.IdParticipation,
            pt.Event.Title,
            pt.Event.Company.Name,
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
            qrCode = invitation.QRCode,
            pdfPath = invitation.PDFPath,
            programPath = invitation.ProgramPath,
            template = invitation.Template,
            sentEmail = invitation.SentEmail,
            sentWhatsApp = invitation.SentWhatsApp,
            createdAt = invitation.CreatedAt
        });
    }
}