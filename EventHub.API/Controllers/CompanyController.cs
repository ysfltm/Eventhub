using EventHub.API.Data;
using EventHub.API.DTOs;
using EventHub.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompanyController : ControllerBase
{
    private readonly AppDbContext _context;

    public CompanyController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CompanyResponseDto>>> GetCompanies()
    {
        var companies = await _context.Companies
            .Select(c => new CompanyResponseDto(
                c.IdCompany, c.Name, c.Email, c.Phone, c.Address, c.Expertise, c.Logo, c.Website
            ))
            .ToListAsync();

        return Ok(companies);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CompanyResponseDto>> GetCompany(int id)
    {
        var c = await _context.Companies.FindAsync(id);

        if (c == null) return NotFound();

        return Ok(new CompanyResponseDto(
            c.IdCompany, c.Name, c.Email, c.Phone, c.Address, c.Expertise, c.Logo, c.Website
        ));
    }

    [HttpPost]
    public async Task<ActionResult<CompanyResponseDto>> CreateCompany(CreateCompanyDto dto)
    {
        var company = new Company
        {
            Name = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            Address = dto.Address,
            Expertise = dto.Expertise,
            Logo = dto.Logo,
            Website = dto.Website
        };

        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        var response = new CompanyResponseDto(
            company.IdCompany, company.Name, company.Email, company.Phone, 
            company.Address, company.Expertise, company.Logo, company.Website
        );

        return CreatedAtAction(nameof(GetCompany), new { id = company.IdCompany }, response);
    }
    // PUT: api/Company/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCompany(int id, CreateCompanyDto dto)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null) return NotFound("Company not found.");

        company.Name = dto.Name;
        company.Email = dto.Email;
        company.Phone = dto.Phone;
        company.Address = dto.Address;
        company.Expertise = dto.Expertise;
        company.Logo = dto.Logo;
        company.Website = dto.Website;

        await _context.SaveChangesAsync();
        return NoContent();
    }

// DELETE: api/Company/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCompany(int id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null) return NotFound("Company not found.");

        _context.Companies.Remove(company);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Company '{company.Name}' and its related records were deleted." });
    }
}
