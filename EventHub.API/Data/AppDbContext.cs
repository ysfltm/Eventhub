using EventHub.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EventHub.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Participation> Participations => Set<Participation>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(e => e.IdCompany);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.IdEvent);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            
            entity.HasOne(e => e.Company)
                  .WithMany(c => c.Events)
                  .HasForeignKey(e => e.IdCompany)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasIndex(p => p.Email).IsUnique();
            entity.HasKey(p => p.IdPerson);
            
            // 1. Map PersonRole Enum to/from string in SQL
            entity.Property(p => p.Role)
                  .HasConversion<string>();

            // 2. Set default value using the Enum instead of string
            entity.Property(p => p.Role)
                  .HasDefaultValue(PersonRole.Attendee);

            entity.Property(p => p.Email).IsRequired().HasMaxLength(150);
            entity.Property(p => p.IsAccountActivated).HasDefaultValue(false);
            entity.HasOne(p => p.Company)
                .WithMany(c => c.Persons)
                .HasForeignKey(p => p.IdCompany)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Participation>(entity =>
        {
            entity.HasKey(pt => pt.IdParticipation);

            entity.HasOne(pt => pt.Event)
                  .WithMany(e => e.Participations)
                  .HasForeignKey(pt => pt.IdEvent)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pt => pt.Person)
                  .WithMany(p => p.Participations)
                  .HasForeignKey(pt => pt.IdPerson)
                  .OnDelete(DeleteBehavior.Restrict); // ✅ Keeps Person safe!

            entity.HasIndex(pt => new { pt.IdEvent, pt.IdPerson }).IsUnique();
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.HasKey(i => i.IdInvitation);

            entity.HasOne(i => i.Participation)
                  .WithOne(pt => pt.Invitation)
                  .HasForeignKey<Invitation>(i => i.IdParticipation)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(f => f.IdFeedback);

            // ✅ FIX: Changed NoAction to Cascade so deleting Event cleans up Feedbacks
            entity.HasOne(f => f.Event)
                .WithMany(e => e.Feedbacks)
                .HasForeignKey(f => f.IdEvent)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(f => f.Participation)
                .WithOne(pt => pt.Feedback)
                .HasForeignKey<Feedback>(f => f.IdParticipation)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}