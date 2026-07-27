using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using EventHub.API.Services;

namespace EventHub.API.Services;

public class InvitationPdfService : IInvitationPdfService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IQRCodeService _qrCodeService;

    public InvitationPdfService(IWebHostEnvironment environment, IQRCodeService qrCodeService)
    {
        _environment = environment;
        _qrCodeService = qrCodeService;
    }

    public string GenerateInvitationPdf(
        int participationId,
        string eventTitle,
        string companyName,
        DateTime eventDate,
        TimeSpan startTime,
        string address,
        string personName,
        string personEmail,
        string qrPayload)
    {
        // 1. Generate QR code byte array
        byte[] qrBytes = _qrCodeService.GenerateQrCodePng(qrPayload);

        // 2. Prepare file destination path
        string folder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "invitations");
        Directory.CreateDirectory(folder);

        string fileName = $"Pass_Participation_{participationId}_{Guid.NewGuid():N}.pdf";
        string filePath = Path.Combine(folder, fileName);

        // 3. Render PDF document using QuestPDF
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5.Landscape());
                page.Margin(20);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                page.Header()
                    .Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("EVENTHUB ACCESS PASS").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().Text($"Hosted by {companyName}").FontSize(10).Italic().FontColor(Colors.Grey.Medium);
                        });
                    });

                page.Content()
                    .PaddingVertical(10)
                    .Row(row =>
                    {
                        // Event Details Side
                        row.RelativeItem(2).Column(col =>
                        {
                            col.Item().Text(eventTitle).FontSize(16).Bold().FontColor(Colors.Grey.Darken3);
                            col.Item().PaddingTop(5).Text($"📅 Date: {eventDate:MMMM dd, yyyy}");
                            col.Item().Text($"⏰ Time: {startTime:hh\\:mm}");
                            col.Item().Text($"📍 Venue: {address}");

                            col.Item().PaddingTop(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                            col.Item().PaddingTop(10).Text("ATTENDEE DETAILS").FontSize(10).Bold().FontColor(Colors.Grey.Medium);
                            col.Item().Text(personName).FontSize(12).Bold();
                            col.Item().Text(personEmail).FontSize(10).FontColor(Colors.Grey.Darken1);
                        });

                        // QR Code Side
                        row.RelativeItem(1).Column(col =>
                        {
                            col.Item().AlignCenter().Image(qrBytes);
                            col.Item().AlignCenter().Text("Scan at entry").FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                        });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text("Powered by EventHub Platform • Present this QR pass at the event entrance")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf(filePath);

        return $"/invitations/{fileName}";
    }
    public string GenerateEventProgramPdf(
    int eventId,
    string eventTitle,
    string? description,
    string companyName,
    DateTime eventDate,
    TimeSpan startTime,
    TimeSpan endTime,
    string address,
    string? spokesperson,
    List<string>? sponsors = null)
{
    // Prepare folder: wwwroot/programs/
    string folder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "programs");
    Directory.CreateDirectory(folder);

    string fileName = $"Program_Event_{eventId}_{Guid.NewGuid():N}.pdf";
    string filePath = Path.Combine(folder, fileName);

    Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

            // Header Banner
            page.Header().Column(col =>
            {
                col.Item().Text("OFFICIAL EVENT PROGRAM").FontSize(22).Bold().FontColor(Colors.Blue.Darken3);
                col.Item().Text($"Hosted by {companyName}").FontSize(12).Italic().FontColor(Colors.Grey.Medium);
                col.Item().PaddingTop(5).LineHorizontal(2).LineColor(Colors.Blue.Darken3);
            });

            // Content
            page.Content().PaddingVertical(15).Column(col =>
            {
                // Event Title & Description
                col.Item().Text(eventTitle).FontSize(18).Bold().FontColor(Colors.Grey.Darken4);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    col.Item().PaddingTop(5).Text(description).FontSize(11).FontColor(Colors.Grey.Darken2);
                }

                col.Item().PaddingTop(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                // Schedule & Details Grid
                col.Item().PaddingTop(15).Text("EVENT DETAILS & SCHEDULE").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

                col.Item().PaddingTop(5).Text($"📅 Date: {eventDate:EEEE, MMMM dd, yyyy}");
                col.Item().Text($"⏰ Time: {startTime:hh\\:mm} - {endTime:hh\\:mm}");
                col.Item().Text($"📍 Venue: {address}");

                if (!string.IsNullOrWhiteSpace(spokesperson))
                {
                    col.Item().PaddingTop(5).Text($"👤 Keynote / Spokesperson: {spokesperson}").Bold();
                }

                // Sponsors Section
                if (sponsors != null && sponsors.Any())
                {
                    col.Item().PaddingTop(20).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(10).Text("SPONSORS & PARTNERS").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

                    col.Item().PaddingTop(5).Column(sponsorsCol =>
                    {
                        foreach (var sponsor in sponsors)
                        {
                            sponsorsCol.Item().Text($"• {sponsor}").FontSize(11).Medium();
                        }
                    });
                }
            });

            // Footer
            page.Footer()
                .AlignCenter()
                .Text("Generated by EventHub • All rights reserved")
                .FontSize(9)
                .FontColor(Colors.Grey.Medium);
        });
    }).GeneratePdf(filePath);

    return $"/programs/{fileName}";
}
}