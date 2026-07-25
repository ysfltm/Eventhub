using EventHub.API.Data;
using EventHub.API.DTOs;
using EventHub.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public EventController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // GET: api/Event
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EventResponseDto>>> GetEvents()
    {
        var events = await _context.Events
            .Include(e => e.Company)
            .Select(e => new EventResponseDto(
                e.IdEvent,
                e.IdCompany,
                e.Company.Name,
                e.Title,
                e.Description,
                e.Address,
                e.Date,
                e.StartTime,
                e.EndTime,
                e.Status,
                e.Person,
                e.ProgramPath
            ))
            .ToListAsync();

        return Ok(events);
    }

    // GET: api/Event/5
    [HttpGet("{id}")]
    public async Task<ActionResult<EventResponseDto>> GetEvent(int id)
    {
        var e = await _context.Events
            .Include(ev => ev.Company)
            .FirstOrDefaultAsync(ev => ev.IdEvent == id);

        if (e == null) return NotFound();

        return Ok(new EventResponseDto(
            e.IdEvent,
            e.IdCompany,
            e.Company.Name,
            e.Title,
            e.Description,
            e.Address,
            e.Date,
            e.StartTime,
            e.EndTime,
            e.Status,
            e.Person,
            e.ProgramPath
        ));
    }

    // POST: api/Event
    [HttpPost]
    public async Task<ActionResult<EventResponseDto>> CreateEvent(CreateEventDto dto)
    {
        // Verify company exists
        var company = await _context.Companies.FindAsync(dto.IdCompany);
        if (company == null)
        {
            return BadRequest(new { message = $"Company with ID {dto.IdCompany} does not exist." });
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
            Status = "Scheduled"
        };

        _context.Events.Add(newEvent);
        await _context.SaveChangesAsync();

        var response = new EventResponseDto(
            newEvent.IdEvent,
            newEvent.IdCompany,
            company.Name,
            newEvent.Title,
            newEvent.Description,
            newEvent.Address,
            newEvent.Date,
            newEvent.StartTime,
            newEvent.EndTime,
            newEvent.Status,
            newEvent.Person,
            newEvent.ProgramPath
        );

        return CreatedAtAction(nameof(GetEvent), new { id = newEvent.IdEvent }, response);
    }

    // POST: api/Event/{id}/upload-program
    [HttpPost("{id}/upload-program")]
    public async Task<IActionResult> UploadProgramPdf(int id, IFormFile file)
    {
        var e = await _context.Events.FindAsync(id);
        if (e == null) return NotFound("Event not found.");

        if (file == null || file.Length == 0)
            return BadRequest("Please upload a valid PDF file.");

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only PDF files are allowed.");

        // Save file to wwwroot/programs/
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
    // PUT: api/Event/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEvent(int id, CreateEventDto dto)
    {
        var evt = await _context.Events.FindAsync(id);
        if (evt == null) return NotFound("Event not found.");

        // Validate that the company exists
        var companyExists = await _context.Companies.AnyAsync(c => c.IdCompany == dto.IdCompany);
        if (!companyExists) return BadRequest("Associated company does not exist.");

        evt.IdCompany = dto.IdCompany;
        evt.Title = dto.Title;
        evt.Date = dto.Date;
        evt.StartTime = dto.StartTime;
        evt.EndTime = dto.EndTime;
        evt.Address = dto.Address;

        await _context.SaveChangesAsync();
        return NoContent();
    }

// DELETE: api/Event/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var evt = await _context.Events.FindAsync(id);
        if (evt == null) return NotFound("Event not found.");

        _context.Events.Remove(evt);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Event '{evt.Title}' and its associated records were deleted." });
    }
}