using EventHub.API.Data;
using EventHub.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "EventOrganiser,SuperAdmin")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AnalyticsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets detailed analytics for a single event (Registrations, Check-ins, Ratings)
    /// </summary>
    [HttpGet("event/{eventId:int}")]
    public async Task<ActionResult<EventAnalyticsDto>> GetEventAnalytics(int eventId)
    {
        var evt = await _context.Events
            .Include(e => e.Participations)
            .Include(e => e.Feedbacks)
            .FirstOrDefaultAsync(e => e.IdEvent == eventId);

        if (evt == null)
            return NotFound(new { message = $"Event with ID {eventId} not found." });

        int totalRegistrations = evt.Participations.Count;
        int totalCheckedIn = evt.Participations.Count(p => p.CheckInTime != null);
        
        double attendanceRate = totalRegistrations > 0
            ? Math.Round((double)totalCheckedIn / totalRegistrations * 100, 2)
            : 0;

        int totalFeedbacks = evt.Feedbacks.Count;
        double avgRating = totalFeedbacks > 0
            ? Math.Round(evt.Feedbacks.Average(f => f.Rating), 2)
            : 0;

        var dto = new EventAnalyticsDto(
            evt.IdEvent,
            evt.Title,
            totalRegistrations,
            totalCheckedIn,
            attendanceRate,
            avgRating,
            totalFeedbacks
        );

        return Ok(dto);
    }

    /// <summary>
    /// Gets global platform statistics for administrators/organizers
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<OverallPlatformStatsDto>> GetPlatformOverview()
    {
        int totalEvents = await _context.Events.CountAsync();
        int totalUsers = await _context.People.CountAsync();
        int totalParticipations = await _context.Participations.CountAsync();
        int totalCheckedIn = await _context.Participations.CountAsync(p => p.CheckInTime != null);

        double globalAttendanceRate = totalParticipations > 0
            ? Math.Round((double)totalCheckedIn / totalParticipations * 100, 2)
            : 0;

        int totalFeedbacks = await _context.Feedbacks.CountAsync();
        double globalAvgRating = totalFeedbacks > 0
            ? Math.Round(await _context.Feedbacks.AverageAsync(f => f.Rating), 2)
            : 0;

        var overview = new OverallPlatformStatsDto(
            totalEvents,
            totalUsers,
            totalParticipations,
            totalCheckedIn,
            globalAttendanceRate,
            globalAvgRating
        );

        return Ok(overview);
    }
}