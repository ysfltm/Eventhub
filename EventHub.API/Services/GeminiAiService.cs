using System.Text;
using System.Text.Json;
using EventHub.API.DTOs;

namespace EventHub.API.Services;

public class GeminiAiService : IGeminiAiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiAiService> _logger;

    public GeminiAiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    // 1. Event Creation Plan Generator
    public async Task<GeneratedEventResponseDto> GenerateEventPlanAsync(GenerateEventPromptDto dto)
    {
        var apiKey = _configuration["Gemini:ApiKey"] 
                     ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_GEMINI_API_KEY_HERE")
        {
            _logger.LogWarning("Gemini API Key missing or default. Executing intelligent failsafe fallback.");
            return GenerateFailsafeEventPlan(dto);
        }

        try
        {
            var systemPrompt = $@"You are an expert enterprise event planner for EventHub.
Generate a structured JSON response for a corporate/tech event based on the following parameters:
- Topic/Prompt: '{dto.Topic}'
- Category: '{dto.Category ?? "Technology"}'
- Preferred Language: '{dto.Language ?? "en"}'
- Expected Duration: {dto.DurationHours ?? 6} hours
- Target Audience: '{dto.TargetAudience ?? "Corporate & Developers"}'
- Venue: '{dto.LocationPreference ?? "Tunis Convention Center"}'
- Target Capacity: {dto.PreferredCapacity ?? 150}

Strictly output ONLY valid raw JSON matching this schema (do not wrap in markdown ```json blocks):
{{
  ""title"": ""string"",
  ""description"": ""string (rich 2-3 paragraphs)"",
  ""category"": ""string"",
  ""address"": ""string"",
  ""capacity"": {dto.PreferredCapacity ?? 150},
  ""startTime"": ""09:00"",
  ""endTime"": ""17:00"",
  ""agendaSchedule"": [
    {{ ""time"": ""09:00 - 10:00"", ""title"": ""string"", ""speakerRole"": ""string"", ""description"": ""string"" }},
    {{ ""time"": ""10:15 - 12:00"", ""title"": ""string"", ""speakerRole"": ""string"", ""description"": ""string"" }},
    {{ ""time"": ""12:00 - 13:30"", ""title"": ""Networking Luncheon & Expo"", ""speakerRole"": ""All Attendees"", ""description"": ""string"" }},
    {{ ""time"": ""13:30 - 15:30"", ""title"": ""string"", ""speakerRole"": ""string"", ""description"": ""string"" }},
    {{ ""time"": ""15:45 - 17:00"", ""title"": ""Closing Panel & Key Takeaways"", ""speakerRole"": ""Keynote Panel"", ""description"": ""string"" }}
  ],
  ""keyTakeaways"": [""Takeaway 1"", ""Takeaway 2"", ""Takeaway 3""]
}}";

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = systemPrompt } } }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    responseMimeType = "application/json"
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Gemini API call failed with status {Status}: {Body}. Using failsafe.", response.StatusCode, errorBody);
                return GenerateFailsafeEventPlan(dto);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var rawText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return GenerateFailsafeEventPlan(dto);
            }

            var cleanJson = rawText.Trim().Replace("```json", "").Replace("```", "").Trim();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<GeneratedEventResponseDto>(cleanJson, options);

            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Title))
            {
                return parsed with { IsAiGenerated = true, IsFallback = false };
            }

            return GenerateFailsafeEventPlan(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error communicating with Gemini. Falling back safely.");
            return GenerateFailsafeEventPlan(dto);
        }
    }

    // 2. Feedback Sentiment Analyzer
    public async Task<EventSentimentInsightsDto> AnalyzeFeedbackSentimentAsync(int eventId, string eventTitle, List<(int Rating, string Comment)> reviews)
    {
        if (reviews.Count == 0)
        {
            return new EventSentimentInsightsDto(
                TotalReviewsAnalyzed: 0,
                AverageRating: 5.0,
                SentimentScorePercent: 100.0,
                SentimentLabel: "No Reviews Yet",
                ExecutiveSummary: "No attendee feedback has been submitted for this session yet.",
                TopStrengths: new List<string> { "Awaiting initial feedback" },
                AreasForImprovement: new List<string>(),
                IsFallback: true
            );
        }

        var avgRating = Math.Round(reviews.Average(r => r.Rating), 1);
        var apiKey = _configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_GEMINI_API_KEY_HERE")
        {
            return GenerateFailsafeSentiment(reviews, avgRating);
        }

        try
        {
            var reviewSnippets = string.Join("\n", reviews.Select((r, i) => $"#{i + 1} Rating: {r.Rating}/5, Comment: \"{r.Comment}\""));
            var prompt = $@"Analyze these attendee feedback reviews for event '{eventTitle}':
{reviewSnippets}

Strictly output ONLY valid JSON matching this schema:
{{
  ""sentimentScorePercent"": 88.5,
  ""sentimentLabel"": ""Positive"",
  ""executiveSummary"": ""Concise 2-3 sentence executive briefing summarizing overall sentiment and attendee engagement."",
  ""topStrengths"": [""Strength 1"", ""Strength 2"", ""Strength 3""],
  ""areasForImprovement"": [""Improvement 1"", ""Improvement 2""]
}}";

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { temperature = 0.3, responseMimeType = "application/json" }
            };

            var response = await _httpClient.PostAsync(endpoint, new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
            {
                return GenerateFailsafeSentiment(reviews, avgRating);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var rawText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            var cleanJson = rawText!.Trim().Replace("```json", "").Replace("```", "").Trim();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<EventSentimentInsightsDto>(cleanJson, options);

            return parsed != null
                ? parsed with { TotalReviewsAnalyzed = reviews.Count, AverageRating = avgRating, IsFallback = false }
                : GenerateFailsafeSentiment(reviews, avgRating);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed Gemini sentiment analysis. Falling back.");
            return GenerateFailsafeSentiment(reviews, avgRating);
        }
    }

    // 3. Attendee AI Concierge In-Event Assistant
    public async Task<EventConciergeChatResponseDto> AskEventConciergeAsync(
        int eventId,
        string eventTitle,
        string description,
        string address,
        string timeWindow,
        string dateFormatted,
        string companyName,
        string companyContact,
        EventConciergeChatRequestDto dto
    )
    {
        var apiKey = _configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_GEMINI_API_KEY_HERE")
        {
            return GenerateFailsafeConciergeReply(eventTitle, address, timeWindow, dateFormatted, companyName, companyContact, dto.Message);
        }

        try
        {
            var systemPrompt = $@"You are the official, friendly AI Concierge for the event '{eventTitle}' on EventHub.
Ground your responses strictly on these official facts:
- Event: {eventTitle} (ID #{eventId})
- Date: {dateFormatted}
- Time: {timeWindow}
- Venue: {address}
- Host Organization: {companyName}
- Contact: {companyContact}
- Description: {description}
- Digital Passes: Attendees can view/print their verified QR Pass in 'My Registrations'.
- Certificates: Official Certificates of Attendance are unlocked after optical QR check-in at the entrance.

Respond naturally and warmly in {dto.Language ?? "the user's language"}.

Strictly output ONLY valid JSON matching this schema:
{{
  ""reply"": ""Your conversational response (1-2 paragraphs max)."",
  ""suggestedFollowUps"": [""Follow-up question 1?"", ""Follow-up question 2?""]
}}";

            var historyList = new List<object>();
            if (dto.History != null && dto.History.Count > 0)
            {
                var cleanHistory = dto.History
                    .Where(h => !string.IsNullOrWhiteSpace(h.Content))
                    .SkipWhile(h => h.Role != "user")
                    .TakeLast(6)
                    .ToList();

                string? lastRole = null;
                foreach (var h in cleanHistory)
                {
                    var currentRole = h.Role == "user" ? "user" : "model";
                    if (currentRole != lastRole)
                    {
                        historyList.Add(new
                        {
                            role = currentRole,
                            parts = new[] { new { text = h.Content } }
                        });
                        lastRole = currentRole;
                    }
                }
            }

            historyList.Add(new
            {
                role = "user",
                parts = new[] { new { text = dto.Message } }
            });

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
            var requestBody = new
            {
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = historyList,
                generationConfig = new
                {
                    temperature = 0.6,
                    responseMimeType = "application/json"
                }
            };

            var response = await _httpClient.PostAsync(endpoint, new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
            {
                var errStr = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Gemini chat status {Status}: {Body}. Falling back.", response.StatusCode, errStr);
                return GenerateFailsafeConciergeReply(eventTitle, address, timeWindow, dateFormatted, companyName, companyContact, dto.Message);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var rawText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            var cleanJson = rawText!.Trim().Replace("```json", "").Replace("```", "").Trim();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<EventConciergeChatResponseDto>(cleanJson, options);

            return parsed != null && !string.IsNullOrWhiteSpace(parsed.Reply)
                ? parsed with { IsFallback = false }
                : GenerateFailsafeConciergeReply(eventTitle, address, timeWindow, dateFormatted, companyName, companyContact, dto.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini concierge error. Falling back safely.");
            return GenerateFailsafeConciergeReply(eventTitle, address, timeWindow, dateFormatted, companyName, companyContact, dto.Message);
        }
    }

    // ── 4. High-Quality Failsafe Fallbacks ─────────────────────────────────────────
    private static GeneratedEventResponseDto GenerateFailsafeEventPlan(GenerateEventPromptDto dto)
    {
        var cleanTopic = string.IsNullOrWhiteSpace(dto.Topic) ? "Enterprise Innovation Summit" : dto.Topic.Trim();
        return new GeneratedEventResponseDto(
            Title: cleanTopic,
            Description: $"Join industry pioneers and technology leaders for {cleanTopic}. This comprehensive session explores the latest architectural breakthroughs, best practices, and practical implementations tailored for {dto.TargetAudience ?? "technology professionals"}.",
            Category: dto.Category ?? "Technology",
            Address: dto.LocationPreference ?? "Tunis Convention Center, Tunis",
            Capacity: dto.PreferredCapacity ?? 150,
            StartTime: "09:00",
            EndTime: "17:00",
            AgendaSchedule: new List<AgendaSessionDto>
            {
                new("09:00 - 09:30", "Registration & Morning Coffee", "Event Staff", "Badge verification, pass check-in, and welcome refreshments."),
                new("09:30 - 11:00", $"Opening Keynote: The Future of {cleanTopic}", "Keynote Speaker", "Visionary overview of industry trends, strategic roadmaps, and key methodologies."),
                new("11:15 - 12:30", "Deep-Dive Technical Masterclass", "Lead Architect", "Hands-on architectural patterns, live case studies, and engineering benchmarks."),
                new("12:30 - 14:00", "Networking Luncheon & Partner Expo", "All Attendees", "Structured networking and sponsor product exhibitions."),
                new("14:00 - 15:30", "Interactive Panel Discussion & Q&A", "Industry Expert Panel", "Live audience Q&A tackling real-world deployment challenges."),
                new("15:45 - 17:00", "Closing Remarks & Certificate Issuance", "Organising Committee", "Summary of conclusions, closing addresses, and digital certificate verification.")
            },
            KeyTakeaways: new List<string>
            {
                "Proven blueprints and architectural patterns for real-world execution.",
                "Direct networking opportunities with seasoned industry specialists.",
                "Official Verified Certificate of Attendance credential."
            },
            IsAiGenerated: false,
            IsFallback: true
        );
    }

    private static EventSentimentInsightsDto GenerateFailsafeSentiment(List<(int Rating, string Comment)> reviews, double avgRating)
    {
        var percent = Math.Round((avgRating / 5.0) * 100, 1);
        var label = percent >= 85 ? "Very Positive" : percent >= 70 ? "Positive" : percent >= 50 ? "Mixed" : "Needs Improvement";

        return new EventSentimentInsightsDto(
            TotalReviewsAnalyzed: reviews.Count,
            AverageRating: avgRating,
            SentimentScorePercent: percent,
            SentimentLabel: label,
            ExecutiveSummary: $"Based on {reviews.Count} attendee reviews, this event achieved an overall rating of {avgRating}/5 ({percent}% satisfaction rate). Attendees highlighted strong speaker depth and organized flow.",
            TopStrengths: new List<string> { "Engaging presentations", "Seamless QR pass entry flow", "High technical relevance" },
            AreasForImprovement: new List<string> { "Allocate more time for interactive Q&A discussions" },
            IsFallback: true
        );
    }

    private static EventConciergeChatResponseDto GenerateFailsafeConciergeReply(
        string title, string address, string timeWindow, string dateFormatted, string company, string contact, string message)
    {
        var lower = (message ?? "").ToLower();
        string reply;
        var followUps = new List<string>();

        if (lower.Contains("time") || lower.Contains("schedule") || lower.Contains("hour") || lower.Contains("when") || lower.Contains("heure") || lower.Contains("programme") || lower.Contains("وقت") || lower.Contains("برنامج"))
        {
            reply = $"📅 **{title}** is taking place on **{dateFormatted}** during the hours of **{timeWindow}**.\n\nWe recommend arriving 15 minutes before the opening session for pass verification.";
            followUps.Add("📍 Where is the venue located?");
            followUps.Add("🎟️ How do I check in with my QR pass?");
        }
        else if (lower.Contains("where") || lower.Contains("venue") || lower.Contains("location") || lower.Contains("address") || lower.Contains("lieu") || lower.Contains("adresse") || lower.Contains("مكان") || lower.Contains("عنوان") || lower.Contains("أين"))
        {
            reply = $"📍 The session takes place at **{address}**.\n\nYou can click 'View Venue on Map' on this page for turn-by-turn navigation.";
            followUps.Add("🕒 What time does the session begin?");
            followUps.Add("🎟️ How do I access my digital QR pass?");
        }
        else if (lower.Contains("certif") || lower.Contains("attest") || lower.Contains("شهادة"))
        {
            reply = "🏆 **Official Certificates of Attendance** are automatically generated and verifiable for participants immediately after optical QR check-in at the venue entrance.";
            followUps.Add("🎟️ How do I show my QR entry pass?");
            followUps.Add("🏢 Who is hosting this session?");
        }
        else if (lower.Contains("pass") || lower.Contains("ticket") || lower.Contains("qr") || lower.Contains("billet") || lower.Contains("تذكرة") || lower.Contains("دخول"))
        {
            reply = "🎟️ Your verified entry QR pass is accessible in the **'My Registrations'** wallet tab. Simply present this barcode to door staff for optical turnstile entry.";
            followUps.Add("🕒 What is the event schedule?");
            followUps.Add("🏆 How do I get my certificate?");
        }
        else if (lower.Contains("host") || lower.Contains("company") || lower.Contains("organi") || lower.Contains("contact") || lower.Contains("société") || lower.Contains("منظم") || lower.Contains("شركة"))
        {
            reply = $"🏢 **{title}** is organized by **{company}**.\n\nFor direct inquiries or partnership details, you can reach out via **{contact}**.";
            followUps.Add("📍 Where is the venue located?");
            followUps.Add("🕒 What time does it start?");
        }
        else
        {
            reply = $"👋 Hello! I am your AI Concierge for **{title}**, hosted by **{company}** on **{dateFormatted}** ({timeWindow}) at **{address}**.\n\nHow can I help you today?";
            followUps.Add("🕒 What is the session schedule?");
            followUps.Add("📍 How do I get to the venue?");
            followUps.Add("🏆 Is an attendance certificate provided?");
        }

        return new EventConciergeChatResponseDto(
            Reply: reply,
            SuggestedFollowUps: followUps,
            IsFallback: true
        );
    }
}
