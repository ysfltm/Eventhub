using EventHub.API.Data;
using EventHub.API.DTOs;
using EventHub.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AIController : ControllerBase
{
    private readonly IGeminiAiService _aiService;
    private readonly AppDbContext _context;

    public AIController(IGeminiAiService aiService, AppDbContext context)
    {
        _aiService = aiService;
        _context = context;
    }

    // POST: api/AI/generate-event-plan
    [HttpPost("generate-event-plan")]
    [Authorize(Roles = "SuperAdmin,EventOrganiser")]
    public async Task<ActionResult<GeneratedEventResponseDto>> GenerateEventPlan([FromBody] GenerateEventPromptDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Topic))
        {
            return BadRequest("Please provide an event topic or prompt.");
        }

        var result = await _aiService.GenerateEventPlanAsync(dto);
        return Ok(result);
    }

    // GET: api/AI/event/5/feedback-insights
    [HttpGet("event/{eventId}/feedback-insights")]
    [Authorize(Roles = "SuperAdmin,EventOrganiser,Sponsor,Staff")]
    public async Task<ActionResult<EventSentimentInsightsDto>> GetEventFeedbackInsights(int eventId)
    {
        var ev = await _context.Events.FindAsync(eventId);
        if (ev == null) return NotFound("Event not found.");

        var feedbacks = await _context.Feedbacks
            .Where(f => f.IdEvent == eventId)
            .Select(f => new { f.Rating, f.Comment })
            .ToListAsync();

        var reviewsList = feedbacks.Select(f => (f.Rating, f.Comment ?? "")).ToList();

        var result = await _aiService.AnalyzeFeedbackSentimentAsync(eventId, ev.Title, reviewsList);
        return Ok(result);
    }

    // POST: api/AI/event/5/chat
    [HttpPost("event/{eventId}/chat")]
    [AllowAnonymous] // Accessible to all authenticated roles (Attendee, VIP, Speaker, Staff, Organiser, SuperAdmin)
    public async Task<ActionResult<EventConciergeChatResponseDto>> AskEventConcierge(int eventId, [FromBody] EventConciergeChatRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
        {
            return BadRequest("Message cannot be empty.");
        }

        var ev = await _context.Events
            .Include(e => e.Company)
            .FirstOrDefaultAsync(e => e.IdEvent == eventId);

        if (ev == null) return NotFound("Event not found.");

        var dateFormatted = ev.Date.ToString("dddd, MMMM dd, yyyy");
        var timeWindow = $"{ev.StartTime:hh\\:mm} – {ev.EndTime:hh\\:mm}";
        var companyName = ev.Company?.Name ?? "EventHub Host Organization";
        var companyContact = ev.Company?.Email ?? ev.Company?.Website ?? "info@eventhub.com";

        var result = await _aiService.AskEventConciergeAsync(
            eventId: eventId,
            eventTitle: ev.Title,
            description: ev.Description ?? "No detailed description provided.",
            address: ev.Address,
            timeWindow: timeWindow,
            dateFormatted: dateFormatted,
            companyName: companyName,
            companyContact: companyContact,
            dto: dto
        );

        return Ok(result);
    }
}
