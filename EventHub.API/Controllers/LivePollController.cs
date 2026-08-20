using System.Collections.Concurrent;
using EventHub.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LivePollController : ControllerBase
{
    // Fast, thread-safe in-memory store for real-time live polls
    private static readonly ConcurrentDictionary<string, PollSession> _polls = new();
    private static readonly ConcurrentDictionary<int, string> _activeEventPoll = new(); // EventId -> PollId

    // 1. POST: api/LivePoll/create (Speaker / Organiser launches a live poll)
    [HttpPost("create")]
    [Authorize(Roles = "Speaker,Spokesperson,EventOrganiser,SuperAdmin")]
    public IActionResult CreatePoll([FromBody] CreateLivePollDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Question) || dto.Options == null || dto.Options.Count < 2)
        {
            return BadRequest("Poll must have a question and at least 2 options.");
        }

        string pollId = $"POLL-{dto.EventId}-{Guid.NewGuid():N}"[..18];

        var poll = new PollSession
        {
            PollId = pollId,
            EventId = dto.EventId,
            Question = dto.Question,
            Options = dto.Options,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            DurationSeconds = dto.DurationSeconds
        };

        _polls[pollId] = poll;
        _activeEventPoll[dto.EventId] = pollId;

        return Ok(poll.ToDto());
    }

    // 2. GET: api/LivePoll/event/1 (Attendees & Host get active poll and live stats)
    [HttpGet("event/{eventId:int}")]
    public IActionResult GetActivePollForEvent(int eventId)
    {
        if (!_activeEventPoll.TryGetValue(eventId, out var pollId) || !_polls.TryGetValue(pollId, out var poll))
        {
            return Ok(new { hasActivePoll = false, message = "No active poll currently running for this event." });
        }

        return Ok(new { hasActivePoll = true, poll = poll.ToDto() });
    }

    // 3. POST: api/LivePoll/vote (Attendee casts their vote)
    [HttpPost("vote")]
    public IActionResult Vote([FromBody] SubmitPollVoteDto dto)
    {
        if (!_polls.TryGetValue(dto.PollId, out var poll))
        {
            return NotFound("Poll not found.");
        }

        if (!poll.IsActive)
        {
            return BadRequest("This poll has ended and is no longer accepting votes.");
        }

        if (dto.OptionIndex < 0 || dto.OptionIndex >= poll.Options.Count)
        {
            return BadRequest("Invalid option selected.");
        }

        // Record or update vote for this person (1 vote per person)
        poll.Voters[dto.PersonId] = dto.OptionIndex;

        return Ok(new { 
            message = "Vote recorded successfully!", 
            poll = poll.ToDto() 
        });
    }

    // 4. POST: api/LivePoll/close/POLL-123 (Speaker / Organiser closes poll and reveals final stats)
    [HttpPost("close/{pollId}")]
    [Authorize(Roles = "Speaker,Spokesperson,EventOrganiser,SuperAdmin")]
    public IActionResult ClosePoll(string pollId)
    {
        if (!_polls.TryGetValue(pollId, out var poll))
        {
            return NotFound("Poll not found.");
        }

        poll.IsActive = false;
        return Ok(new { message = "Poll closed.", poll = poll.ToDto() });
    }

    // Helper Model for In-Memory State
    public class PollSession
    {
        public string PollId { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public ConcurrentDictionary<int, int> Voters { get; set; } = new(); // PersonId -> OptionIndex
        public bool IsActive { get; set; } = true;
        public int DurationSeconds { get; set; } = 60;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public PollResultDto ToDto()
        {
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < Options.Count; i++) counts[i] = 0;

            foreach (var kvp in Voters)
            {
                if (counts.ContainsKey(kvp.Value))
                    counts[kvp.Value]++;
            }

            return new PollResultDto(
                PollId,
                EventId,
                Question,
                Options,
                counts,
                Voters.Count,
                IsActive,
                CreatedAt
            );
        }
    }
}
