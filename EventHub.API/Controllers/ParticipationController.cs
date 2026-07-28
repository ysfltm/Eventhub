using EventHub.API.Data;
using EventHub.API.DTOs;
using EventHub.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParticipationController : ControllerBase
{
    private readonly AppDbContext _context;

    public ParticipationController(AppDbContext context)
    {
        _context = context;
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
}