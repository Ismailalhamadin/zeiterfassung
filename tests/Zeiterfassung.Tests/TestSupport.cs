using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Zeiterfassung.Web.Data;
using Zeiterfassung.Web.Data.Models;
using Zeiterfassung.Web.Services;

namespace Zeiterfassung.Tests;

/// <summary>Frische InMemory-Datenbank je Test (eigener Name), damit sich Tests nicht beeinflussen.</summary>
public sealed class TestDb : IDbContextFactory<ZeiterfassungDbContext>
{
    private readonly DbContextOptions<ZeiterfassungDbContext> _options =
        new DbContextOptionsBuilder<ZeiterfassungDbContext>()
            .UseInMemoryDatabase("test-" + Guid.NewGuid())
            .Options;

    public ZeiterfassungDbContext CreateDbContext() => new(_options);

    public TimeTrackingService Service() => new(this, NullLogger<TimeTrackingService>.Instance);

    public TimeTrackingExportService Export() => new(this);

    public async Task<TimeProject> AddProjectAsync(string name = "Projekt A", string color = "#112233")
    {
        await using var db = CreateDbContext();
        var project = new TimeProject { Name = name, Color = color };
        db.TimeProjects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    public async Task<TimeEntry> AddEntryAsync(TimeProject project, DateOnly date, int startHour, int minutes,
        int breakMinutes = 0, string description = "Arbeit")
    {
        await using var db = CreateDbContext();
        var entry = new TimeEntry
        {
            TimeProjectId = project.Id,
            Date = date,
            StartTime = new TimeOnly(startHour, 0),
            EndTime = new TimeOnly(startHour, 0).AddMinutes(minutes + breakMinutes),
            BreakMinutes = breakMinutes,
            Description = description
        };
        entry.RecalculateDuration();
        db.TimeEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
