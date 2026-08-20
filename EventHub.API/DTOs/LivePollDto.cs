namespace EventHub.API.DTOs;

public record CreateLivePollDto(
    int EventId,
    string Question,
    List<string> Options, // e.g. ["NVIDIA", "AMD"] or ["React", "Vue", "Angular"]
    int DurationSeconds = 600
);

public record SubmitPollVoteDto(
    string PollId,
    int OptionIndex, // 0-based index of chosen option
    int PersonId
);

public record PollResultDto(
    string PollId,
    int EventId,
    string Question,
    List<string> Options,
    Dictionary<int, int> VoteCounts, // OptionIndex -> Count
    int TotalVotes,
    bool IsActive,
    DateTime CreatedAt
);