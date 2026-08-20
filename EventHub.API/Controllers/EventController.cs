using System.Security.Claims;
using EventHub.API.Data;
using EventHub.API.DTOs;
using EventHub.API.Models;
using EventHub.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IInvitationPdfService _pdfService;

    public EventController(
        AppDbContext context, 
        IWebHostEnvironment environment, 
        IInvitationPdfService pdfService)
    {
        _context = context;
        _environment = environment;
        _pdfService = pdfService;
    }

    // GET: api/Event?companyId=5
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EventResponseDto>>> GetEvents([FromQuery] int? companyId = null)
    {
        var query = _context.Events.Include(e => e.Company).AsQueryable();

        // 1. Explicit query param filter (e.g. for Admin dashboards or company-specific views)
        if (companyId.HasValue && companyId.Value > 0)
        {
            query = query.Where(e => e.IdCompany == companyId.Value);
        }
        // 2. Company Isolation: If authenticated employee/attendee, only return their company's events
        else if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            bool isSuperAdmin = User.IsInRole("SuperAdmin");
            var userCompanyClaim = User.FindFirst("CompanyId")?.Value;

            if (!isSuperAdmin && int.TryParse(userCompanyClaim, out int userCompanyId) && userCompanyId > 0)
            {
                query = query.Where(e => e.IdCompany == userCompanyId);
            }
        }

        var events = await query
            .OrderByDescending(e => e.Date)
            .Select(e => new EventResponseDto(
                e.IdEvent,
                e.IdCompany,
                e.Company != null ? e.Company.Name : "Independent Session",
                e.Title,
                e.Description,
                e.Address,
                e.Date,
                e.StartTime,
                e.EndTime,
                e.Status,
                e.Person,
                e.ProgramPath,
                e.Capacity
            ))
            .ToListAsync();

        return Ok(events);
    }

    // GET: api/Event/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<EventResponseDto>> GetEvent(int id)
    {
        var e = await _context.Events
            .Include(ev => ev.Company)
            .FirstOrDefaultAsync(ev => ev.IdEvent == id);

        if (e == null) return NotFound();

        // Company Isolation check on single event
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            bool isSuperAdmin = User.IsInRole("SuperAdmin");
            var userCompanyClaim = User.FindFirst("CompanyId")?.Value;

            if (!isSuperAdmin && int.TryParse(userCompanyClaim, out int userCompanyId) && userCompanyId > 0)
            {
                if (e.IdCompany != userCompanyId)
                {
                    return Forbid(); // User does not belong to the hosting company
                }
            }
        }

        return Ok(new EventResponseDto(
            e.IdEvent,
            e.IdCompany,
            e.Company != null ? e.Company.Name : "Independent Session",
            e.Title,
            e.Description,
            e.Address,
            e.Date,
            e.StartTime,
            e.EndTime,
            e.Status,
            e.Person,
            e.ProgramPath,
            e.Capacity
        ));
    }

    // POST: api/Event
    [HttpPost]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<ActionResult<EventResponseDto>> CreateEvent([FromBody] CreateEventDto dto)
    {
        string companyName = "Independent Session";

        if (dto.IdCompany > 0)
        {
            var company = await _context.Companies.FindAsync(dto.IdCompany);
            if (company == null)
            {
                return BadRequest(new { message = $"Company with ID {dto.IdCompany} does not exist." });
            }
            companyName = company.Name;
        }

        var newEvent = new Event
        {
            IdCompany = dto.IdCompany,
            Title = dto.Title,
            Description = dto.Description,
            Address = dto.Address,
            Date = dto.Date,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Person = dto.Person,
            Capacity = dto.Capacity > 0 ? dto.Capacity : 100,
            Status = "Scheduled"
        };

        _context.Events.Add(newEvent);
        await _context.SaveChangesAsync();

        var response = new EventResponseDto(
            newEvent.IdEvent,
            newEvent.IdCompany,
            companyName,
            newEvent.Title,
            newEvent.Description,
            newEvent.Address,
            newEvent.Date,
            newEvent.StartTime,
            newEvent.EndTime,
            newEvent.Status,
            newEvent.Person,
            newEvent.ProgramPath,
            newEvent.Capacity
        );

        return CreatedAtAction(nameof(GetEvent), new { id = newEvent.IdEvent }, response);
    }

    // POST: api/Event/{id}/upload-program
    [HttpPost("{id:int}/upload-program")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> UploadProgramPdf(int id, IFormFile file)
    {
        var e = await _context.Events.FindAsync(id);
        if (e == null) return NotFound("Event not found.");

        if (file == null || file.Length == 0)
            return BadRequest("Please upload a valid PDF file.");

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only PDF files are allowed.");

        string uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "programs");
        Directory.CreateDirectory(uploadsFolder);

        string uniqueFileName = $"Program_Event_{e.IdEvent}_{Guid.NewGuid():N}.pdf";
        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        e.ProgramPath = $"/programs/{uniqueFileName}";
        await _context.SaveChangesAsync();

        return Ok(new { message = "Program uploaded successfully!", programPath = e.ProgramPath });
    }

    // POST: api/Event/{id}/generate-program
    [HttpPost("{id:int}/generate-program")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> GenerateProgramPdf(int id, [FromBody] List<string>? sponsorNames = null)
    {
        var evt = await _context.Events
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.IdEvent == id);

        if (evt == null) return NotFound("Event not found.");

        string relativePath = _pdfService.GenerateEventProgramPdf(
            evt.IdEvent,
            evt.Title,
            evt.Description,
            evt.Company != null ? evt.Company.Name : "Event Organizer",
            evt.Date,
            evt.StartTime,
            evt.EndTime,
            evt.Address,
            evt.Person,
            sponsorNames
        );

        evt.ProgramPath = relativePath;
        await _context.SaveChangesAsync();

        return Ok(new { 
            message = "Program PDF generated successfully!", 
            programPath = evt.ProgramPath 
        });
    }

    // PUT: api/Event/5
    [HttpPut("{id:int}")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> UpdateEvent(int id, CreateEventDto dto)
    {
        var evt = await _context.Events.FindAsync(id);
        if (evt == null) return NotFound("Event not found.");

        if (dto.IdCompany > 0)
        {
            var companyExists = await _context.Companies.AnyAsync(c => c.IdCompany == dto.IdCompany);
            if (!companyExists) return BadRequest("Associated company does not exist.");
        }

        evt.IdCompany = dto.IdCompany;
        evt.Title = dto.Title;
        evt.Description = dto.Description; 
        evt.Person = dto.Person;
        evt.Date = dto.Date;
        evt.StartTime = dto.StartTime;
        evt.EndTime = dto.EndTime;
        evt.Address = dto.Address;
        if (dto.Capacity > 0) evt.Capacity = dto.Capacity;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/Event/5
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var evt = await _context.Events
            .Include(e => e.Feedbacks)
            .Include(e => e.Participations)
            .ThenInclude(p => p.Invitation)
            .FirstOrDefaultAsync(e => e.IdEvent == id);

        if (evt == null) return NotFound("Event not found.");

        if (evt.Feedbacks.Any())
        {
            _context.Feedbacks.RemoveRange(evt.Feedbacks);
        }

        var invitations = evt.Participations
            .Where(p => p.Invitation != null)
            .Select(p => p.Invitation!);

        if (invitations.Any())
        {
            _context.Invitations.RemoveRange(invitations);
        }

        _context.Events.Remove(evt);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Event '{evt.Title}' and its associated records were deleted successfully." });
    }
}
