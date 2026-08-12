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

    // GET: api/Person
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PersonResponseDto>>> GetPeople()
    {
        var people = await _context.People
            .Select(p => new PersonResponseDto(
                p.IdPerson, p.FirstName, p.LastName, p.Email, p.Phone, p.Address, p.CompanyName, p.Position, p.Role
            ))
            .ToListAsync();

        return Ok(people);
    }

    // GET: api/Person/5
    [HttpGet("{id}")]
    public async Task<ActionResult<PersonResponseDto>> GetPerson(int id)
    {
        var p = await _context.People.FindAsync(id);
        if (p == null) return NotFound();

        return Ok(new PersonResponseDto(
            p.IdPerson, p.FirstName, p.LastName, p.Email, p.Phone, p.Address, p.CompanyName, p.Position, p.Role
        ));
    }

    // POST: api/Person
    [HttpPost]
    public async Task<ActionResult<PersonResponseDto>> CreatePerson(CreatePersonDto dto)
    {
        var person = new Person
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Phone = dto.Phone,
            Address = dto.Address,
            CompanyName = dto.CompanyName,
            Position = dto.Position,
            Role = dto.Role // 👈 Mapping role from DTO
        };

        _context.People.Add(person);
        await _context.SaveChangesAsync();

        var response = new PersonResponseDto(
            person.IdPerson, person.FirstName, person.LastName, person.Email,
            person.Phone, person.Address, person.CompanyName, person.Position, person.Role
        );

        return CreatedAtAction(nameof(GetPerson), new { id = person.IdPerson }, response);
    }

    // PUT: api/Person/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePerson(int id, CreatePersonDto dto)
    {
        var person = await _context.People.FindAsync(id);
        if (person == null) return NotFound("Person not found.");

        person.FirstName = dto.FirstName;
        person.LastName = dto.LastName;
        person.Email = dto.Email;
        person.Phone = dto.Phone;
        person.Address = dto.Address;
        person.CompanyName = dto.CompanyName;
        person.Position = dto.Position;
        person.Role = dto.Role; // 👈 Allows updating role

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/Person/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePerson(int id)
    {
        var person = await _context.People.FindAsync(id);
        if (person == null) return NotFound("Person not found.");

        _context.People.Remove(person);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Person '{person.FirstName} {person.LastName}' was deleted." });
    }
}