using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Web.Data.Models;

namespace Zeiterfassung.Web.Data;

/// <summary>
/// Datenbank der Demo. Läuft mit dem InMemory-Provider komplett im Browser;
/// nach einem Neuladen der Seite ist alles wieder auf dem Stand der Beispieldaten.
/// </summary>
public class ZeiterfassungDbContext(DbContextOptions<ZeiterfassungDbContext> options) : DbContext(options)
{
    public DbSet<TimeProject> TimeProjects => Set<TimeProject>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<TimerSession> TimerSessions => Set<TimerSession>();
    public DbSet<TimerPause> TimerPauses => Set<TimerPause>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimeProject>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Description).HasMaxLength(1000);
            e.Property(p => p.Color).HasMaxLength(20).IsRequired();
            e.Property(p => p.Icon).HasMaxLength(50);
            e.Property(p => p.Status).HasConversion<int>();
        });

        modelBuilder.Entity<TimeEntry>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Description).HasMaxLength(500).IsRequired();
            e.Property(t => t.Notes).HasMaxLength(1000);
            e.Ignore(t => t.GrossMinutes);

            e.HasOne(t => t.TimeProject)
                .WithMany(p => p.Entries)
                .HasForeignKey(t => t.TimeProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TimerSession>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Description).HasMaxLength(500);
            e.Property(s => s.Notes).HasMaxLength(1000);
            e.Property(s => s.Status).HasConversion<int>();

            e.HasOne(s => s.TimeProject)
                .WithMany()
                .HasForeignKey(s => s.TimeProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TimerPause>(e =>
        {
            e.HasKey(p => p.Id);

            e.HasOne(p => p.TimerSession)
                .WithMany(s => s.Pauses)
                .HasForeignKey(p => p.TimerSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
