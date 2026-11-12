using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Web.Data.Models;

namespace Zeiterfassung.Web.Data;

/// <summary>
/// Erfundene Beispieldaten: vier Projekte, rund zehn Wochen Einträge und ein laufender Timer.
/// Alle Zeiten werden relativ zu "jetzt" erzeugt, damit die Demo nie veraltet.
/// </summary>
public static class DemoSeed
{
    /// <summary>Wie viele Tage rückwärts Einträge erzeugt werden (zehn Wochen: die Wochen-Diagramme zeigen dann nur volle Wochen).</summary>
    public const int Days = 70;

    /// <summary>Ergebnis von <see cref="Build"/>, unabhängig von der Datenbank (gut testbar).</summary>
    public sealed record SeedData(List<TimeProject> Projects, List<TimeEntry> Entries, TimerSession Timer);

    private record Slot(DayOfWeek Day, int Project, int StartMinute, int Minutes, int Break, double Chance);

    // Wochenmuster: Schule und Lernen vormittags/nachmittags, Praktikum an zwei ganzen Tagen, Sport am Rand.
    private static readonly Slot[] Pattern =
    [
        new(DayOfWeek.Monday,    0, 8 * 60 + 30,  195, 15, 0.95),
        new(DayOfWeek.Monday,    1, 13 * 60,      120, 0,  0.85),
        new(DayOfWeek.Tuesday,   2, 8 * 60,       510, 30, 0.95),
        new(DayOfWeek.Wednesday, 0, 8 * 60 + 30,  210, 15, 0.9),
        new(DayOfWeek.Wednesday, 1, 13 * 60 + 30, 120, 0,  0.8),
        new(DayOfWeek.Wednesday, 3, 18 * 60,       75, 0,  0.7),
        new(DayOfWeek.Thursday,  2, 8 * 60,       510, 30, 0.95),
        new(DayOfWeek.Friday,    1, 9 * 60,       150, 10, 0.9),
        new(DayOfWeek.Friday,    0, 12 * 60 + 30, 150, 0,  0.85),
        new(DayOfWeek.Saturday,  3, 10 * 60,       90, 0,  0.6),
        new(DayOfWeek.Sunday,    1, 16 * 60,       90, 0,  0.4),
    ];

    private static readonly (string Name, string Description, string Color, string Icon)[] ProjectDefs =
    [
        ("Schulprojekt Website", "Gruppenarbeit: Webseite für einen fiktiven Verein.", "#732F3B", "bi-globe"),
        ("Lernen .NET", "Eigene Übungen zu C#, Blazor und Datenbanken.", "#3F566B", "bi-code-slash"),
        ("Praktikum", "Pflichtpraktikum in einer IT-Abteilung (erfundenes Beispiel).", "#3F7A5A", "bi-briefcase"),
        ("Sport", "Training neben der Schule.", "#B07A2A", "bi-bicycle"),
    ];

    private static readonly string[][] Descriptions =
    [
        ["Layout der Startseite umgesetzt", "Navigation und Fußzeile gebaut", "Kontaktformular mit Validierung",
         "Responsives Design geprüft", "Texte und Bilder eingepflegt", "Code-Review im Team", "Farbschema abgestimmt"],
        ["Blazor-Komponenten geübt", "LINQ-Aufgaben gelöst", "Entity Framework Grundlagen",
         "Async und Await verstanden", "Unit-Tests mit xUnit geschrieben", "Git-Branches geübt"],
        ["Einarbeitung ins Ticketsystem", "Netzwerkdokumentation aktualisiert", "Support-Anfragen bearbeitet",
         "Arbeitsplatzrechner eingerichtet", "Backup-Protokolle geprüft", "Inventarliste gepflegt"],
        ["Laufen", "Krafttraining", "Schwimmen", "Radfahren"],
    ];

    /// <summary>Baut die Beispieldaten für den Zeitpunkt <paramref name="now"/>. Gleiche Eingabe, gleiche Daten.</summary>
    public static SeedData Build(DateTime now)
    {
        var projects = ProjectDefs.Select(d => new TimeProject
        {
            Name = d.Name, Description = d.Description, Color = d.Color, Icon = d.Icon,
            Status = ProjectStatus.Active, CreatedAt = now.Date.AddDays(-Days - 5)
        }).ToList();

        // Fester Startwert: die Verteilung ändert sich nicht von Aufruf zu Aufruf.
        var rng = new Random(20260101);
        var today = DateOnly.FromDateTime(now);
        var nowMinute = now.Hour * 60 + now.Minute;
        var entries = new List<TimeEntry>();

        for (var i = Days; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            foreach (var slot in Pattern.Where(s => s.Day == date.DayOfWeek))
            {
                if (rng.NextDouble() > slot.Chance) continue;

                var start = slot.StartMinute + rng.Next(-1, 3) * 15;
                var minutes = slot.Minutes + rng.Next(-2, 3) * 15;
                var end = start + minutes;
                if (date == today)
                {
                    // Nichts in der Zukunft: Was heute noch läuft, wird auf eine Stunde vor jetzt gekürzt
                    // (auf 15 Minuten gerundet); bleibt weniger als eine halbe Stunde, entfällt es.
                    end = Math.Min(end, (nowMinute - 60) / 15 * 15);
                    if (end - start - slot.Break < 30) continue;
                }

                var texts = Descriptions[slot.Project];
                var entry = new TimeEntry
                {
                    TimeProject = projects[slot.Project],
                    Date = date,
                    StartTime = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(start)),
                    EndTime = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(end)),
                    BreakMinutes = slot.Break,
                    Description = texts[rng.Next(texts.Length)],
                    FromTimer = rng.NextDouble() < 0.15,
                    CreatedAt = date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(end))),
                };
                entry.UpdatedAt = entry.CreatedAt;
                if (rng.NextDouble() < 0.2) entry.Notes = "Offene Punkte für das nächste Mal notiert.";
                entry.RecalculateDuration();
                entries.Add(entry);
            }
        }

        // Ein Timer läuft bereits, mit einer kurzen Pause dazwischen.
        var started = now.AddMinutes(-47);
        var timer = new TimerSession
        {
            TimeProject = projects[1],
            StartedAt = started,
            Status = TimerStatus.Running,
            Pauses = { new TimerPause { PausedAt = started.AddMinutes(20), ResumedAt = started.AddMinutes(26) } }
        };

        return new SeedData(projects, entries, timer);
    }

    /// <summary>Leert die Datenbank und füllt sie neu mit den Beispieldaten.</summary>
    public static async Task SeedAsync(IDbContextFactory<ZeiterfassungDbContext> factory, DateTime now)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var data = Build(now);
        db.TimeProjects.AddRange(data.Projects);
        db.TimeEntries.AddRange(data.Entries);
        db.TimerSessions.Add(data.Timer);
        await db.SaveChangesAsync();
    }
}
