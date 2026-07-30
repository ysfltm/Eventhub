using System.Text;
using System.Text.Json;

namespace EventHub.API.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(HttpClient httpClient, IConfiguration config, ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

public async Task<bool> SendInvitationWhatsAppAsync(string toPhoneNumber, string attendeeName, string eventTitle)
{
    try
    {
        var settings = _config.GetSection("MetaWhatsApp");
        string phoneNumberId = settings["PhoneNumberId"] 
            ?? throw new InvalidOperationException("Meta PhoneNumberId missing.");
        string accessToken = settings["AccessToken"] 
            ?? throw new InvalidOperationException("Meta AccessToken missing.");

        var requestUrl = $"https://graph.facebook.com/v18.0/{phoneNumberId}/messages";
        string cleanPhone = toPhoneNumber.Replace("+", "").Replace(" ", "").Replace("-", "").Trim();

        var payload = new
        {
            messaging_product = "whatsapp",
            to = cleanPhone,
            type = "template",
            template = new
            {
                name = "hello_world", // approved sandbox template
                language = new { code = "en_US" }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(httpRequest);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        // Capture the exact error body sent back from Facebook/Meta API
        string errorResponseBody = await response.Content.ReadAsStringAsync();
        _logger.LogError("Meta WhatsApp API error response: {Error}", errorResponseBody);
        Console.WriteLine($"[Meta API Error]: {errorResponseBody}");
        
        return false;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to dispatch WhatsApp invitation to {Phone}", toPhoneNumber);
        Console.WriteLine($"[Exception Error]: {ex.Message}");
        return false;
    }
}

    public async Task<bool> SendPassPdfWhatsAppAsync(string toPhoneNumber, string pdfPublicUrl, string fileName, string caption)
    {
        try
        {
            var settings = _config.GetSection("MetaWhatsApp");
            string phoneNumberId = settings["PhoneNumberId"] 
                ?? throw new InvalidOperationException("Meta PhoneNumberId missing.");
            string accessToken = settings["AccessToken"] 
                ?? throw new InvalidOperationException("Meta AccessToken missing.");

            var requestUrl = $"https://graph.facebook.com/v18.0/{phoneNumberId}/messages";
            string cleanPhone = toPhoneNumber.Replace("+", "").Replace(" ", "").Replace("-", "").Trim();

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = cleanPhone,
                type = "document",
                document = new
                {
                    link = pdfPublicUrl,
                    filename = fileName,
                    caption = caption
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(httpRequest);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp document to {Phone}", toPhoneNumber);
            return false;
        }
    }
}