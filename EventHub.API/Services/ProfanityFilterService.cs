namespace EventHub.API.Services;

public interface IProfanityFilterService
{
    bool ContainsProfanity(string? text);
    string? CensorText(string? text);
}

public class ProfanityFilterService : IProfanityFilterService
{
    // Use the fully qualified class name to prevent namespace conflict
    private readonly ProfanityFilter.ProfanityFilter _detector = new();

    public bool ContainsProfanity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return _detector.ContainsProfanity(text);
    }

    public string? CensorText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        return _detector.CensorString(text);
    }
}