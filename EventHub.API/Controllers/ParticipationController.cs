using EventHub.API.Data;
using EventHub.API.DTOs;
using EventHub.API.Models;
using EventHub.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
// ✅ FIX 1: Expanded Class-Level Authorize attribute to include all 8 roles
[Authorize(Roles = "Attendee,VIP,Spokesperson,Speaker,Sponsor,Staff,EventOrganiser,SuperAdmin")]
public class ParticipationController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IInvitationPdfService _pdfService;
    private readonly IWebHostEnvironment _env;

    public ParticipationController(
        AppDbContext context,
        IEmailService emailService,
        IInvitationPdfService pdfService,
        IWhatsAppService whatsAppService,
        IWebHostEnvironment env)
    {
        _context = context;
        _emailService = emailService;
        _whatsAppService = whatsAppService;
        _pdfService = pdfService;
        _env = env;
    }

    // 1. GET: api/Participation/event/1
    [HttpGet("event/{eventId}")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin,Staff,Sponsor,Attendee,VIP,Spokesperson,Speaker")]
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

    // 2. POST: api/Participation
    [HttpPost]
    [Authorize(Roles = "Attendee,VIP,Spokesperson,Speaker,Sponsor,Staff,EventOrganiser,SuperAdmin")] // ✅ FIX 2: Removed leading space before Attendee
    public async Task<ActionResult<ParticipationResponseDto>> AssignParticipant(CreateParticipationDto dto)
    {
        // ✅ FIX 3: Enforce self-registration for non-Organiser users to prevent impersonation
        int targetPersonId = dto.IdPerson;
        bool isOrganiserOrAdmin = User.IsInRole("EventOrganiser") || User.IsInRole("SuperAdmin");

        if (!isOrganiserOrAdmin)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                              ?? User.FindFirst("sub")?.Value 
                              ?? User.FindFirst("nameid")?.Value;

            if (!int.TryParse(userIdClaim, out int currentPersonId))
            {
                return Unauthorized(new { message = "Invalid token or user identity." });
            }
            targetPersonId = currentPersonId;
        }

        var evt = await _context.Events.FindAsync(dto.IdEvent);
        if (evt == null) return BadRequest("Event does not exist.");

        var person = await _context.People.FindAsync(targetPersonId);
        if (person == null) return BadRequest("Person does not exist.");

        // Prevent duplicate registration for the same event
        var exists = await _context.Participations
            .AnyAsync(pt => pt.IdEvent == dto.IdEvent && pt.IdPerson == targetPersonId);

        if (exists)
        {
            return BadRequest("This person is already registered for this event.");
        }

        var participation = new Participation
        {
            IdEvent = dto.IdEvent,
            IdPerson = targetPersonId,
            Type = string.IsNullOrWhiteSpace(dto.Type) ? "Attendee" : dto.Type,
            Status = "Invited",
            InvitationDate = DateTime.UtcNow
        };

        _context.Participations.Add(participation);
        await _context.SaveChangesAsync();

        // Automatically generate pass + program and dispatch email/WhatsApp to participant
        try
        {
            await ProcessAndSendPassAsync(participation.IdParticipation);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Failed to auto-send pass email/WhatsApp: {ex.Message}");
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

    // 3. GET: api/Participation/my-passes
    [HttpGet("my-passes")]
    [Authorize(Roles = "Attendee,VIP,Spokesperson,Speaker,Sponsor,Staff,EventOrganiser,SuperAdmin")] // ✅ FIX 4: Removed leading space before Attendee
    public async Task<IActionResult> GetMyPasses()
    {
        // 1. Extract IdPerson securely from JWT Claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? User.FindFirst("sub")?.Value
                          ?? User.FindFirst("nameid")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentPersonId))
        {
            return Unauthorized(new { message = "Invalid token or user identity." });
        }

        // 2. Fetch all participation passes for the current user with Event & Company details
        var myPasses = await _context.Participations
            .Include(p => p.Event)
                .ThenInclude(e => e.Company)
            .Include(p => p.Invitation)
            .Where(p => p.IdPerson == currentPersonId)
            .OrderByDescending(p => p.Event.Date)
            .Select(p => new
            {
                p.IdParticipation,
                p.IdEvent,
                p.Type,
                p.Status,
                p.InvitationDate,
                p.CheckInTime,
                p.CheckOutTime,
                Event = new
                {
                    p.Event.IdEvent,
                    p.Event.Title,
                    p.Event.Description,
                    p.Event.Date,
                    p.Event.StartTime,
                    p.Event.EndTime,
                    p.Event.Address,
                    Company = p.Event.Company != null ? new
                    {
                        p.Event.Company.IdCompany,
                        p.Event.Company.Name,
                        p.Event.Company.Email
                    } : null
                },
                Pass = p.Invitation != null ? new
                {
                    p.Invitation.IdInvitation,
                    p.Invitation.QRCode,
                    p.Invitation.PDFPath,
                    p.Invitation.SentEmail,
                    p.Invitation.SentWhatsApp
                } : null
            })
            .ToListAsync();

        return Ok(myPasses);
    }

    // 4. PUT: api/Participation/5
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

    // DELETE: api/Participation/cancel/1
    [HttpDelete("cancel/{eventId:int}")]
    [Authorize(Roles = "Attendee,VIP,Spokesperson,Speaker,Sponsor,Staff,EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> CancelMyRegistration(int eventId)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                          ?? User.FindFirst("sub")?.Value
                          ?? User.FindFirst("nameid")?.Value
                          ?? User.FindFirst("id")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentPersonId))
        {
            return Unauthorized(new { message = "Invalid token or user context." });
        }

        bool isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("EventOrganiser");

        // ✅ FIX 1: Allow Admins/Organisers to cancel any pass for the event
        var participation = await _context.Participations
            .Include(p => p.Event)
            .Include(p => p.Invitation) // ✅ Include Invitation
            .FirstOrDefaultAsync(p => p.IdEvent == eventId && (p.IdPerson == currentPersonId || isAdmin));

        if (participation == null)
        {
            return NotFound(new { message = "You are not registered for this event." });
        }

        if (participation.CheckInTime != null)
        {
            return BadRequest(new { message = "Cannot cancel registration after you have already checked in." });
        }

        // ✅ FIX 2: Delete QR Invitation first to prevent SQL Foreign Key Exception
        if (participation.Invitation != null)
        {
            _context.Invitations.Remove(participation.Invitation);
        }

        _context.Participations.Remove(participation);
        await _context.SaveChangesAsync();

        return Ok(new { 
            message = $"Successfully cancelled registration for '{participation.Event?.Title ?? "Event"}'.",
            eventId = eventId
        });
    }

    // DELETE: api/Participation/5
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> DeleteParticipation(int id)
    {
        var pt = await _context.Participations
            .Include(p => p.Invitation)
            .FirstOrDefaultAsync(p => p.IdParticipation == id);

        if (pt == null) return NotFound("Participation record not found.");

        // ✅ FIX 3: Delete QR Invitation first to prevent SQL Foreign Key Exception
        if (pt.Invitation != null)
        {
            _context.Invitations.Remove(pt.Invitation);
        }

        _context.Participations.Remove(pt);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Participation record {id} deleted successfully." });
    }


    // 7. POST: api/Participation/check-in
    [HttpPost("check-in")]
    [Authorize(Roles = "Attendee,VIP,Spokesperson,Speaker,Sponsor,Staff,EventOrganiser,SuperAdmin")] // ✅ FIX 6: Removed leading space before Attendee
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

    // 8. POST: api/Participation/5/send-invitation
    [HttpPost("{participationId:int}/send-invitation")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> SendInvitation(int participationId)
    {
        var pt = await _context.Participations
            .Include(p => p.Event)
            .Include(p => p.Person)
            .FirstOrDefaultAsync(p => p.IdParticipation == participationId);

        if (pt == null) return NotFound("Participation record not found.");
        if (string.IsNullOrWhiteSpace(pt.Person.Phone)) return BadRequest("Participant has no phone number.");

        bool success = await _whatsAppService.SendInvitationWhatsAppAsync(
            toPhoneNumber: pt.Person.Phone,
            attendeeName: $"{pt.Person.FirstName} {pt.Person.LastName}",
            eventTitle: pt.Event.Title
        );

        if (!success) return StatusCode(500, "Failed to dispatch WhatsApp invitation.");

        return Ok(new { message = "WhatsApp invitation template sent successfully. Awaiting participant reply." });
    }

    // 9. POST: api/Participation/event/1/send-all-invitations
    [HttpPost("event/{eventId:int}/send-all-invitations")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> SendAllInvitations(int eventId)
    {
        var participations = await _context.Participations
            .Include(p => p.Event)
            .Include(p => p.Person)
            .Where(p => p.IdEvent == eventId && !string.IsNullOrWhiteSpace(p.Person.Phone))
            .ToListAsync();

        if (!participations.Any())
        {
            return NotFound(new { message = "No valid participants with phone numbers found for this event." });
        }

        // Run all WhatsApp invitations concurrently in parallel
        var tasks = participations.Select(pt => _whatsAppService.SendInvitationWhatsAppAsync(
            toPhoneNumber: pt.Person.Phone,
            attendeeName: $"{pt.Person.FirstName} {pt.Person.LastName}",
            eventTitle: pt.Event.Title
        ));

        await Task.WhenAll(tasks);

        return Ok(new { 
            message = $"Bulk WhatsApp invitations dispatched to {participations.Count} participants." 
        });
    }

    // 10. POST: api/Participation/5/send-pass
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
            message = "Pass and Event Program generated and emailed/WhatsApped successfully!",
            pdfUrl = relativePdfPath
        });
    }

    // 11. POST: api/Participation/event/1/send-all-passes
    [HttpPost("event/{eventId:int}/send-all-passes")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> SendAllPasses(int eventId)
    {
        var participationIds = await _context.Participations
            .Where(p => p.IdEvent == eventId)
            .Select(p => p.IdParticipation)
            .ToListAsync();

        if (!participationIds.Any())
        {
            return NotFound(new { message = "No participants found for this event." });
        }

        int successCount = 0;

        // Process sequentially to keep DbContext thread-safe
        foreach (var id in participationIds)
        {
            try
            {
                var result = await ProcessAndSendPassAsync(id);
                if (result != null) successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed processing pass for participation ID {id}: {ex.Message}");
            }
        }

        return Ok(new { 
            message = $"Bulk Passes & Programs generated and dispatched to {successCount}/{participationIds.Count} participants!" 
        });
    }

    // 12. Helper method: ProcessAndSendPassAsync
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

        // 5. Build Attachments Collection for Email
        string safeTitle = pt.Event.Title.Replace(" ", "_");
        var attachments = new List<EmailAttachmentDto>
        {
            new EmailAttachmentDto(physicalPassPath, $"Pass_{safeTitle}.pdf"),
            new EmailAttachmentDto(physicalProgramPath, $"Program_{safeTitle}.pdf")
        };

        // Safely format date & time strings before string interpolation
        string formattedDate = pt.Event.Date.ToString("MMMM dd, yyyy");
        string formattedTime = DateTime.Today.Add(pt.Event.StartTime).ToString("hh:mm tt");

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

        // Update DB Email Status
        pt.Invitation.SentEmail = true;
        pt.Invitation.EmailSentDate = DateTime.UtcNow;

        // 8. Dispatch WhatsApp Automated Message (if phone number is present)
        if (!string.IsNullOrWhiteSpace(pt.Person.Phone))
        {
            try
            {
                string publicBaseUrl = "https://grandkid-copy-catering.ngrok-free.dev";
                
                string fullPassPdfUrl = $"{publicBaseUrl}{relativePassPath}";
                string fullProgramPdfUrl = $"{publicBaseUrl}{relativeProgramPath}";

                string passCaption = $"🎟️ *Event Pass: {pt.Event.Title}*\n\n" +
                                     $"Hello *{pt.Person.FirstName} {pt.Person.LastName}*,\n" +
                                     $"Attached is your official entry pass with your personal QR code for check-in.";

                string programCaption = $"📖 *Official Event Program: {pt.Event.Title}*";

                // Send Pass PDF
                bool passSent = await _whatsAppService.SendPassPdfWhatsAppAsync(
                    toPhoneNumber: pt.Person.Phone,
                    pdfPublicUrl: fullPassPdfUrl,
                    fileName: $"Pass_{safeTitle}.pdf",
                    caption: passCaption
                );

                // Send Program PDF
                bool programSent = await _whatsAppService.SendPassPdfWhatsAppAsync(
                    toPhoneNumber: pt.Person.Phone,
                    pdfPublicUrl: fullProgramPdfUrl,
                    fileName: $"Program_{safeTitle}.pdf",
                    caption: programCaption
                );

                if (passSent || programSent)
                {
                    pt.Invitation.SentWhatsApp = true;
                    pt.Invitation.WhatsAppSentDate = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Failed to send WhatsApp documents: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();

        return relativePassPath;
    }
}
