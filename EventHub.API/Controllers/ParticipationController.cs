using EventHub.API.Data;
using EventHub.API.DTOs;
using EventHub.API.Models;
using EventHub.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParticipationController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IInvitationPdfService _pdfService;
    private readonly IWebHostEnvironment _env;

    public ParticipationController(
        AppDbContext context,
        IEmailService emailService,
        IInvitationPdfService pdfService,
        IWebHostEnvironment env)
    {
        _context = context;
        _emailService = emailService;
        _pdfService = pdfService;
        _env = env;
    }

    // GET: api/Participation/event/1
    [HttpGet("event/{eventId}")]
    public async Task<ActionResult<IEnumerable<ParticipationResponseDto>>> GetParticipationsByEvent(int eventId)
    {
        var list = await _context.Participations
            .Include(pt => pt.Event)
            .Include(pt => pt.Person)
            .Where(pt => pt.IdEvent == eventId)
            .Select(pt => new ParticipationResponseDto(
                pt.IdParticipation,
                pt.IdEvent,
                pt.Event.Title,
                pt.IdPerson,
                $"{pt.Person.FirstName} {pt.Person.LastName}",
                pt.Person.Email,
                pt.Type,
                pt.Status,
                pt.InvitationDate,
                pt.CheckInTime,
                pt.CheckOutTime
            ))
            .ToListAsync();

        return Ok(list);
    }

    // POST: api/Participation
    [HttpPost]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<ActionResult<ParticipationResponseDto>> AssignParticipant(CreateParticipationDto dto)
    {
        var evt = await _context.Events.FindAsync(dto.IdEvent);
        if (evt == null) return BadRequest("Event does not exist.");

        var person = await _context.People.FindAsync(dto.IdPerson);
        if (person == null) return BadRequest("Person does not exist.");

        // Prevent duplicate registration for the same event
        var exists = await _context.Participations
            .AnyAsync(pt => pt.IdEvent == dto.IdEvent && pt.IdPerson == dto.IdPerson);

        if (exists)
        {
            return BadRequest("This person is already registered for this event.");
        }

        var participation = new Participation
        {
            IdEvent = dto.IdEvent,
            IdPerson = dto.IdPerson,
            Type = dto.Type,
            Status = "Invited",
            InvitationDate = DateTime.UtcNow
        };

        _context.Participations.Add(participation);
        await _context.SaveChangesAsync();

        // Automatically generate pass + program and dispatch email to participant
        try
        {
            await ProcessAndSendPassAsync(participation.IdParticipation);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Failed to auto-send pass email: {ex.Message}");
        }

        var response = new ParticipationResponseDto(
            participation.IdParticipation,
            evt.IdEvent,
            evt.Title,
            person.IdPerson,
            $"{person.FirstName} {person.LastName}",
            person.Email,
            participation.Type,
            participation.Status,
            participation.InvitationDate,
            participation.CheckInTime,
            participation.CheckOutTime
        );

        return Ok(response);
    }

    // PUT: api/Participation/5
    [HttpPut("{id}")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> UpdateParticipation(int id, [FromBody] CreateParticipationDto dto)
    {
        var pt = await _context.Participations.FindAsync(id);
        if (pt == null) return NotFound("Participation record not found.");

        // Validate foreign keys
        var eventExists = await _context.Events.AnyAsync(e => e.IdEvent == dto.IdEvent);
        var personExists = await _context.People.AnyAsync(p => p.IdPerson == dto.IdPerson);

        if (!eventExists || !personExists)
            return BadRequest("Invalid Event or Person ID.");

        pt.IdEvent = dto.IdEvent;
        pt.IdPerson = dto.IdPerson;
        pt.Type = dto.Type;
        pt.Status = dto.Status;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/Participation/5
    [HttpDelete("{id}")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> DeleteParticipation(int id)
    {
        var pt = await _context.Participations.FindAsync(id);
        if (pt == null) return NotFound("Participation record not found.");

        _context.Participations.Remove(pt);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Participation record {id} deleted successfully." });
    }

    // POST: api/Participation/check-in
    [HttpPost("check-in")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<ActionResult<CheckInResponseDto>> CheckInParticipant([FromBody] CheckInRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.QrPayload))
        {
            return BadRequest(new CheckInResponseDto(
                Success: false,
                Message: "QR payload is required.",
                ParticipationId: null,
                AttendeeName: null,
                CheckInTime: null
            ));
        }

        // 1. Locate the Invitation by QR Code payload
        var invitation = await _context.Invitations
            .Include(i => i.Participation)
                .ThenInclude(p => p.Person)
            .Include(i => i.Participation)
                .ThenInclude(p => p.Event)
            .FirstOrDefaultAsync(i => i.QRCode == dto.QrPayload);

        if (invitation == null)
        {
            return NotFound(new CheckInResponseDto(
                Success: false,
                Message: "Invalid QR pass code.",
                ParticipationId: null,
                AttendeeName: null,
                CheckInTime: null
            ));
        }

        var pt = invitation.Participation;

        // 2. Ensure pass matches the intended event
        if (pt.IdEvent != dto.EventId)
        {
            return BadRequest(new CheckInResponseDto(
                Success: false,
                Message: $"Pass is for event '{pt.Event.Title}', not event ID {dto.EventId}.",
                ParticipationId: pt.IdParticipation,
                AttendeeName: $"{pt.Person.FirstName} {pt.Person.LastName}",
                CheckInTime: null
            ));
        }

        // 3. Check for duplicate scan / already checked in
        if (pt.CheckInTime != null)
        {
            return Conflict(new CheckInResponseDto(
                Success: false,
                Message: $"Already checked in at {pt.CheckInTime:yyyy-MM-dd HH:mm:ss UTC}.",
                ParticipationId: pt.IdParticipation,
                AttendeeName: $"{pt.Person.FirstName} {pt.Person.LastName}",
                CheckInTime: pt.CheckInTime
            ));
        }

        // 4. Record check-in timestamp and update status
        pt.CheckInTime = DateTime.UtcNow;
        pt.Status = "CheckedIn";

        await _context.SaveChangesAsync();

        return Ok(new CheckInResponseDto(
            Success: true,
            Message: "Check-in successful! Access Granted.",
            ParticipationId: pt.IdParticipation,
            AttendeeName: $"{pt.Person.FirstName} {pt.Person.LastName}",
            CheckInTime: pt.CheckInTime
        ));
    }

    // POST: api/Participation/5/send-pass
    [HttpPost("{participationId:int}/send-pass")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> SendPassEmail(int participationId)
    {
        var relativePdfPath = await ProcessAndSendPassAsync(participationId);

        if (relativePdfPath == null)
        {
            return NotFound(new { message = $"Participation record with ID {participationId} not found." });
        }

        return Ok(new { 
            message = "Pass and Event Program generated and emailed successfully!",
            pdfUrl = relativePdfPath
        });
    }

    /// <summary>
    /// Helper method to generate both QR Pass & Event Program PDFs, then email them to the participant.
    /// </summary>
    private async Task<string?> ProcessAndSendPassAsync(int participationId)
    {
        var pt = await _context.Participations
            .Include(p => p.Event)
                .ThenInclude(e => e.Company)
            .Include(p => p.Person)
            .Include(p => p.Invitation)
            .FirstOrDefaultAsync(p => p.IdParticipation == participationId);

        if (pt == null) return null;

        // 1. Generate or reuse unique QR payload
        string qrPayload = pt.Invitation?.QRCode ?? $"EVENTHUB-{pt.IdEvent}-{pt.IdPerson}-{Guid.NewGuid():N}";
        string wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        // 2. Generate Access Pass PDF via QuestPDF service
        string relativePassPath = _pdfService.GenerateInvitationPdf(
            participationId: pt.IdParticipation,
            eventTitle: pt.Event.Title,
            companyName: pt.Event.Company?.Name ?? "Event Organizer",
            eventDate: pt.Event.Date,
            startTime: pt.Event.StartTime,
            address: pt.Event.Address,
            personName: $"{pt.Person.FirstName} {pt.Person.LastName}",
            personEmail: pt.Person.Email,
            qrPayload: qrPayload
        );
        string physicalPassPath = Path.Combine(wwwroot, relativePassPath.TrimStart('/'));

        // 3. Generate Event Program PDF via QuestPDF service
        string relativeProgramPath = _pdfService.GenerateEventProgramPdf(
            eventId: pt.Event.IdEvent,
            eventTitle: pt.Event.Title,
            description: pt.Event.Description,
            companyName: pt.Event.Company?.Name ?? "Event Organizer",
            eventDate: pt.Event.Date,
            startTime: pt.Event.StartTime,
            endTime: pt.Event.EndTime,
            address: pt.Event.Address,
            spokesperson: null
        );
        string physicalProgramPath = Path.Combine(wwwroot, relativeProgramPath.TrimStart('/'));

        // 4. Track Invitation record in Database
        if (pt.Invitation == null)
        {
            pt.Invitation = new Invitation
            {
                IdParticipation = pt.IdParticipation,
                QRCode = qrPayload,
                PDFPath = relativePassPath,
                CreatedAt = DateTime.UtcNow
            };
            _context.Invitations.Add(pt.Invitation);
        }
        else
        {
            pt.Invitation.PDFPath = relativePassPath;
        }

        // 5. Build Attachments Collection
        string safeTitle = pt.Event.Title.Replace(" ", "_");
        var attachments = new List<EmailAttachmentDto>
        {
            new EmailAttachmentDto(physicalPassPath, $"Pass_{safeTitle}.pdf"),
            new EmailAttachmentDto(physicalProgramPath, $"Program_{safeTitle}.pdf")
        };

        // Safely format date & time strings before string interpolation to avoid FormatException
        string formattedDate = pt.Event.Date.ToString("MMMM dd, yyyy");
        string formattedTime = pt.Event.StartTime.ToString(@"hh\:mm");

        // 6. Build Email HTML Body
        string htmlBody = $@"
            <div style='font-family: Arial, sans-serif; color: #333; max-width: 600px; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                <h2 style='color: #1a56db; margin-top: 0;'>🎟️ Your Event Pass & Official Program</h2>
                <p>Hello <strong>{pt.Person.FirstName} {pt.Person.LastName}</strong>,</p>
                <p>You are officially registered for <strong>{pt.Event.Title}</strong>!</p>
                <hr style='border: none; border-top: 1px solid #eee; margin: 15px 0;' />
                <p><strong>📅 Date:</strong> {formattedDate}<br/>
                   <strong>⏰ Time:</strong> {formattedTime}<br/>
                   <strong>📍 Venue:</strong> {pt.Event.Address}</p>
                <hr style='border: none; border-top: 1px solid #eee; margin: 15px 0;' />
                <p>We've attached two documents to this email for your convenience:</p>
                <ul>
                    <li><strong>Your Access Pass (PDF):</strong> Contains your personal QR code for check-in at entry.</li>
                    <li><strong>Event Program (PDF):</strong> Details the full schedule and event info.</li>
                </ul>
                <br/>
                <p>See you there!<br/><strong>The EventHub Team</strong></p>
            </div>";

        // 7. Dispatch Email with both PDF attachments via MailKit/Gmail
        await _emailService.SendEmailWithAttachmentsAsync(
            toEmail: pt.Person.Email,
            subject: $"🎟️ Access Pass & Program: {pt.Event.Title}",
            bodyHtml: htmlBody,
            attachments: attachments
        );

        // 8. Update status in DB
        pt.Invitation.SentEmail = true;
        pt.Invitation.EmailSentDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return relativePassPath;
    }
}