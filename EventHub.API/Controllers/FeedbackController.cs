using System.Security.Claims;
using EventHub.API.Data;
using EventHub.API.DTOs;
using EventHub.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController : ControllerBase
{
    private readonly AppDbContext _context;

    public FeedbackController(AppDbContext context)
    {
        _context = context;
    }

    // POST: api/Feedback
    [HttpPost]
    [Authorize] // Requires Bearer Token
    public async Task<ActionResult<FeedbackResponseDto>> CreateFeedback([FromBody] CreateFeedbackDto dto)
    {
        // 1. Extract logged-in User's ID from JWT Claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                       ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentPersonId))
        {
            return Unauthorized("User identity could not be verified from token.");
        }

        // 2. Validate rating range (1-5)
        if (dto.Rating < 1 || dto.Rating > 5)
        {
            return BadRequest("Rating must be an integer between 1 and 5.");
        }

        // 3. Find the Participation record for THIS logged-in user at THIS event
        var pt = await _context.Participations
            .Include(p => p.Event)
            .Include(p => p.Person)
            .FirstOrDefaultAsync(p => p.IdEvent == dto.IdEvent && p.IdPerson == currentPersonId);

        if (pt == null)
        {
            return BadRequest("You are not registered for this event.");
        }

        // 4. Ensure attendee actually checked in
        if (pt.CheckInTime == null)
        {
            return BadRequest("You can only submit feedback after checking into the event.");
        }

        // 5. Prevent duplicate feedback
        var exists = await _context.Feedbacks
            .AnyAsync(f => f.IdParticipation == pt.IdParticipation);

        if (exists)
        {
            return BadRequest("You have already submitted feedback for this event.");
        }

        var feedback = new Feedback
        {
            IdEvent = pt.IdEvent,
            IdParticipation = pt.IdParticipation,
            Rating = dto.Rating,
            Comment = dto.Comment,
            CreatedAt = DateTime.UtcNow
        };

        _context.Feedbacks.Add(feedback);
        await _context.SaveChangesAsync();

        var response = new FeedbackResponseDto(
            feedback.IdFeedback,
            pt.IdParticipation,
            pt.IdEvent,
            pt.Event.Title,
            pt.IdPerson,
            $"{pt.Person.FirstName} {pt.Person.LastName}",
            feedback.Rating,
            feedback.Comment,
            feedback.CreatedAt
        );

        return CreatedAtAction(nameof(GetFeedbackById), new { id = feedback.IdFeedback }, response);
    }

    // GET: api/Feedback/5
    [HttpGet("{id}")]
    public async Task<ActionResult<FeedbackResponseDto>> GetFeedbackById(int id)
    {
        var feedback = await _context.Feedbacks
            .Include(f => f.Event)
            .Include(f => f.Participation)
                .ThenInclude(p => p.Person)
            .FirstOrDefaultAsync(f => f.IdFeedback == id);

        if (feedback == null) return NotFound("Feedback not found.");

        return Ok(new FeedbackResponseDto(
            feedback.IdFeedback,
            feedback.IdParticipation,
            feedback.IdEvent,
            feedback.Event.Title,
            feedback.Participation.IdPerson,
            $"{feedback.Participation.Person.FirstName} {feedback.Participation.Person.LastName}",
            feedback.Rating,
            feedback.Comment,
            feedback.CreatedAt
        ));
    }

    // GET: api/Feedback/event/1
    [HttpGet("event/{eventId}")]
    public async Task<ActionResult<IEnumerable<FeedbackResponseDto>>> GetEventFeedbacks(int eventId)
    {
        var list = await _context.Feedbacks
            .Include(f => f.Event)
            .Include(f => f.Participation)
                .ThenInclude(p => p.Person)
            .Where(f => f.IdEvent == eventId)
            .Select(f => new FeedbackResponseDto(
                f.IdFeedback,
                f.IdParticipation,
                f.IdEvent,
                f.Event.Title,
                f.Participation.IdPerson,
                $"{f.Participation.Person.FirstName} {f.Participation.Person.LastName}",
                f.Rating,
                f.Comment,
                f.CreatedAt
            ))
            .ToListAsync();

        return Ok(list);
    }

    // GET: api/Feedback/event/1/summary
    [HttpGet("event/{eventId}/summary")]
    public async Task<ActionResult<EventRatingSummaryDto>> GetEventRatingSummary(int eventId)
    {
        var evt = await _context.Events.FindAsync(eventId);
        if (evt == null) return NotFound("Event not found.");

        var feedbacks = await _context.Feedbacks
            .Where(f => f.IdEvent == eventId)
            .ToListAsync();

        int count = feedbacks.Count;
        double average = count > 0 ? Math.Round(feedbacks.Average(f => f.Rating), 2) : 0;

        return Ok(new EventRatingSummaryDto(
            evt.IdEvent,
            evt.Title,
            average,
            count
        ));
    }

    // PUT: api/Feedback/5
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateFeedback(int id, [FromBody] UpdateFeedbackDto dto)
    {
        if (dto.Rating < 1 || dto.Rating > 5)
        {
            return BadRequest("Rating must be an integer between 1 and 5.");
        }

        var feedback = await _context.Feedbacks.FindAsync(id);
        if (feedback == null) return NotFound("Feedback record not found.");

        feedback.Rating = dto.Rating;
        feedback.Comment = dto.Comment;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/Feedback/5
    [HttpDelete("{id}")]
    [Authorize(Roles = "EventOrganiser,SuperAdmin")]
    public async Task<IActionResult> DeleteFeedback(int id)
    {
        var feedback = await _context.Feedbacks.FindAsync(id);
        if (feedback == null) return NotFound("Feedback record not found.");

        _context.Feedbacks.Remove(feedback);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Feedback record {id} deleted successfully." });
    }
}