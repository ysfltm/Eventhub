using EventHub.API.Data;
using EventHub.API.DTOs;
using EventHub.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonController : ControllerBase
{
    private readonly AppDbContext _context;

    public PersonController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/Person?companyId=5
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PersonResponseDto>>> GetPeople([FromQuery] int? companyId = null)
    {
        var query = _context.People.AsQueryable();

        if (companyId.HasValue && companyId.Value > 0)
        {
            query = query.Where(p => p.IdCompany == companyId.Value);
        }

        var people = await query
            .Select(p => new PersonResponseDto(
                p.IdPerson,
                p.IdCompany,
                p.FirstName,
                p.LastName,
                p.Email,
                p.Phone,
                p.Address,
                p.CompanyName ?? (p.Company != null ? p.Company.Name : null),
                p.Position,
                p.LinkedInUrl,
                p.Role
            ))
            .ToListAsync();

        return Ok(people);
    }

    // GET: api/Person/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PersonResponseDto>> GetPerson(int id)
    {
        var p = await _context.People
            .Include(x => x.Company)
            .FirstOrDefaultAsync(x => x.IdPerson == id);

        if (p == null) return NotFound();

        return Ok(new PersonResponseDto(
            p.IdPerson,
            p.IdCompany,
            p.FirstName,
            p.LastName,
            p.Email,
            p.Phone,
            p.Address,
            p.CompanyName ?? p.Company?.Name,
            p.Position,
            p.LinkedInUrl,
            p.Role
        ));
    }

    // POST: api/Person
    [HttpPost]
    public async Task<ActionResult<PersonResponseDto>> CreatePerson(CreatePersonDto dto)
    {
        string? resolvedCompanyName = dto.CompanyName;

        if (dto.IdCompany.HasValue && dto.IdCompany.Value > 0)
        {
            var company = await _context.Companies.FindAsync(dto.IdCompany.Value);
            if (company != null && string.IsNullOrWhiteSpace(resolvedCompanyName))
            {
                resolvedCompanyName = company.Name;
            }
        }

        var person = new Person
        {
            IdCompany = dto.IdCompany,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Phone = dto.Phone,
            Address = dto.Address,
            CompanyName = resolvedCompanyName,
            Position = dto.Position,
            LinkedInUrl = dto.LinkedInUrl,
            Role = dto.Role
        };

        _context.People.Add(person);
        await _context.SaveChangesAsync();

        var response = new PersonResponseDto(
            person.IdPerson,
            person.IdCompany,
            person.FirstName,
            person.LastName,
            person.Email,
            person.Phone,
            person.Address,
            person.CompanyName,
            person.Position,
            person.LinkedInUrl,
            person.Role
        );

        return CreatedAtAction(nameof(GetPerson), new { id = person.IdPerson }, response);
    }

    // POST: api/Person/bulk-import (Company provides list of users)
    [HttpPost("bulk-import")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin,Sponsor")]
    public async Task<IActionResult> BulkImportEmployees([FromBody] BulkCreatePersonDto dto)
    {
        if (dto.Employees == null || !dto.Employees.Any())
        {
            return BadRequest("Employee list cannot be empty.");
        }

        var company = await _context.Companies.FindAsync(dto.IdCompany);
        if (company == null) return NotFound($"Company with ID {dto.IdCompany} not found.");

        var existingEmails = await _context.People
            .Where(p => dto.Employees.Select(e => e.Email.ToLower()).Contains(p.Email.ToLower()))
            .Select(p => p.Email.ToLower())
            .ToListAsync();

        var newPeople = dto.Employees
            .Where(e => !existingEmails.Contains(e.Email.ToLower()))
            .Select(e => new Person
            {
                IdCompany = dto.IdCompany,
                FirstName = e.FirstName,
                LastName = e.LastName,
                Email = e.Email,
                Phone = e.Phone,
                Address = e.Address,
                CompanyName = company.Name,
                Position = e.Position,
                LinkedInUrl = e.LinkedInUrl,
                Role = e.Role
            })
            .ToList();

        if (newPeople.Any())
        {
            _context.People.AddRange(newPeople);
            await _context.SaveChangesAsync();
        }

        return Ok(new { 
            importedCount = newPeople.Count, 
            skippedDuplicates = existingEmails.Count 
        });
    }

    // PUT: api/Person/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePerson(int id, CreatePersonDto dto)
    {
        var person = await _context.People.FindAsync(id);
        if (person == null) return NotFound("Person not found.");

        string? resolvedCompanyName = dto.CompanyName;
        if (dto.IdCompany.HasValue && dto.IdCompany.Value > 0)
        {
            var company = await _context.Companies.FindAsync(dto.IdCompany.Value);
            if (company != null && string.IsNullOrWhiteSpace(resolvedCompanyName))
            {
                resolvedCompanyName = company.Name;
            }
        }

        person.IdCompany = dto.IdCompany;
        person.FirstName = dto.FirstName;
        person.LastName = dto.LastName;
        person.Email = dto.Email;
        person.Phone = dto.Phone;
        person.Address = dto.Address;
        person.CompanyName = resolvedCompanyName;
        person.Position = dto.Position;
        person.LinkedInUrl = dto.LinkedInUrl;
        person.Role = dto.Role;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/Person/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePerson(int id)
    {
        var person = await _context.People.FindAsync(id);
        if (person == null) return NotFound("Person not found.");

        _context.People.Remove(person);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Person '{person.FirstName} {person.LastName}' was deleted." });
    }
}
