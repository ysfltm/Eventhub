using EventHub.API.Data;
using EventHub.API.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

// 1. Configure QuestPDF License (Must be set before building the app)
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// 2. Register DbContext with SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Register Custom Application Services (Dependency Injection)
builder.Services.AddScoped<IQRCodeService, QRCodeService>();
builder.Services.AddScoped<IInvitationPdfService, InvitationPdfService>();

// 4. Add Controller support & CORS
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// 5. Add API Explorer & Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enables serving uploaded/generated files (e.g. PDFs, QR passes) from wwwroot/
app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();