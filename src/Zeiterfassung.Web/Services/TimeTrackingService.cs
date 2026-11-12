using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Web.Data;
using Zeiterfassung.Web.Data.Models;

namespace Zeiterfassung.Web.Services;

/// <summary>
/// Zentrale Geschäftslogik der Zeiterfassung: Projekte, Zeiteinträge, Live-Timer,
/// Kennzahlen, Kalender und Auswertungen.
///
/// Nutzt <see cref="IDbContextFactory{TContext}"/> (kurzlebige Kontexte pro Operation).
/// Zeitstempel werden bewusst in lokaler Zeit (<see cref="DateTime.Now"/>) geführt,
/// da „Datum/Startzeit/Endzeit" Wanduhr-Semantik haben.
/// </summary>
public class TimeTrackingService(
    IDbContextFactory<ZeiterfassungDbContext> factory,
    ILogger<TimeTrackingService> logger)
{
    // ─────────────────────────────────────────────────────────────────────
    //  Projekte
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Lädt Projekte, optional gefiltert nach Suchtext, Archiv-Status und Projekt-Status.</summary>
    public async Task<List<TimeProject>> GetProjectsAsync(
        bool includeArchived = false,
        string? search = null,
        ProjectStatus? status = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.TimeProjects.AsNoTracking().AsQueryable();

        if (!includeArchived)
            query = query.Where(p => !p.IsArchived);

        if (status is { } s)
            query = query.Where(p => p.Status == s);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{term}%") ||
                (p.Description != null && EF.Functions.Like(p.Description, $"%{term}%")));
        }

        return await query
            .OrderByDescending(p => p.Status == ProjectStatus.Active)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    /// <summary>Lädt ein Projekt inkl. der Anzahl seiner Einträge (für Detailansichten).</summary>
    public async Task<TimeProject?> GetProjectByIdAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TimeProjects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<TimeProject> AddProjectAsync(TimeProject project)
    {
        await using var db = await factory.CreateDbContextAsync();
        project.CreatedAt = DateTime.Now;
        db.TimeProjects.Add(project);
        await db.SaveChangesAsync();
        logger.LogInformation("Zeiterfassungs-Projekt {Id} ('{Name}') angelegt.", project.Id, project.Name);
        return project;
    }

    public async Task UpdateProjectAsync(TimeProject project)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.TimeProjects.Update(project);
        await db.SaveChangesAsync();
        logger.LogInformation("Zeiterfassungs-Projekt {Id} aktualisiert.", project.Id);
    }

    /// <summary>Löscht ein Projekt inkl. aller zugehörigen Zeiteinträge (Cascade).</summary>
    public async Task DeleteProjectAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var project = await db.TimeProjects.FindAsync(id);
        if (project is null) return;

        // Der InMemory-Provider kaskadiert nur über bereits geladene Objekte (anders als MySQL).
        // Darum Einträge und Timer-Sessions des Projekts vorher laden: Einträge werden mit
        // gelöscht, laufende Sessions verlieren nur die Projektzuordnung (wie SetNull).
        var entries = await db.TimeEntries.Where(e => e.TimeProjectId == id).ToListAsync();
        var sessions = await db.TimerSessions.Where(s => s.TimeProjectId == id).ToListAsync();
        db.TimeEntries.RemoveRange(entries);
        foreach (var session in sessions) session.TimeProjectId = null;
        db.TimeProjects.Remove(project);
        await db.SaveChangesAsync();
        logger.LogInformation("Zeiterfassungs-Projekt {Id} gelöscht.", id);
    }

    /// <summary>Archiviert ein Projekt oder hebt die Archivierung auf.</summary>
    public async Task SetArchivedAsync(int id, bool archived)
    {
        await using var db = await factory.CreateDbContextAsync();
        var project = await db.TimeProjects.FindAsync(id);
        if (project is null) return;
        project.IsArchived = archived;
        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Zeiteinträge
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Lädt Zeiteinträge nach Filter, jeweils inkl. Projekt, neueste zuerst.</summary>
    public async Task<List<TimeEntry>> GetEntriesAsync(TimeEntryFilter filter)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = BuildEntryQuery(db, filter);
        return await query
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.StartTime)
            .ToListAsync();
    }

    /// <summary>Baut die gefilterte Abfrage — wird auch vom Export wiederverwendet.</summary>
    internal static IQueryable<TimeEntry> BuildEntryQuery(ZeiterfassungDbContext db, TimeEntryFilter filter)
    {
        var query = db.TimeEntries
            .AsNoTracking()
            .Include(e => e.TimeProject)
            .AsQueryable();

        if (filter.ProjectId is > 0)
            query = query.Where(e => e.TimeProjectId == filter.ProjectId);

        if (filter.From is { } from)
            query = query.Where(e => e.Date >= from);

        if (filter.To is { } to)
            query = query.Where(e => e.Date <= to);

        // Monat/Jahr über Datumsbereiche statt DateOnly.Month (bessere SQL-Übersetzung).
        if (filter is { Year: { } y, Month: { } m })
        {
            var start = new DateOnly(y, m, 1);
            var end = start.AddMonths(1).AddDays(-1);
            query = query.Where(e => e.Date >= start && e.Date <= end);
        }
        else if (filter.Year is { } yearOnly)
        {
            var start = new DateOnly(yearOnly, 1, 1);
            var end = new DateOnly(yearOnly, 12, 31);
            query = query.Where(e => e.Date >= start && e.Date <= end);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(e =>
                EF.Functions.Like(e.Description, $"%{term}%") ||
                (e.Notes != null && EF.Functions.Like(e.Notes, $"%{term}%")) ||
                (e.TimeProject != null && EF.Functions.Like(e.TimeProject.Name, $"%{term}%")));
        }

        return query;
    }

    public async Task<TimeEntry?> GetEntryByIdAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TimeEntries
            .AsNoTracking()
            .Include(e => e.TimeProject)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<List<TimeEntry>> GetEntriesForDateAsync(DateOnly date)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TimeEntries
            .AsNoTracking()
            .Include(e => e.TimeProject)
            .Where(e => e.Date == date)
            .OrderBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task<TimeEntry> AddEntryAsync(TimeEntry entry)
    {
        entry.RecalculateDuration();
        entry.CreatedAt = DateTime.Now;
        entry.UpdatedAt = DateTime.Now;

        await using var db = await factory.CreateDbContextAsync();
        db.TimeEntries.Add(entry);
        await db.SaveChangesAsync();
        logger.LogInformation("Zeiteintrag {Id} für Projekt {ProjectId} angelegt ({Minutes} min).",
            entry.Id, entry.TimeProjectId, entry.DurationMinutes);
        return entry;
    }

    public async Task UpdateEntryAsync(TimeEntry entry)
    {
        entry.RecalculateDuration();
        entry.UpdatedAt = DateTime.Now;

        await using var db = await factory.CreateDbContextAsync();
        db.TimeEntries.Update(entry);
        await db.SaveChangesAsync();
    }

    public async Task DeleteEntryAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.TimeEntries.FindAsync(id);
        if (entry is null) return;
        db.TimeEntries.Remove(entry);
        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Live-Timer
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Liefert die aktuell laufende oder pausierte Session (falls vorhanden), inkl. Pausen und Projekt.
    /// Der komplette Zustand liegt in der DB → der Timer „überlebt" Reloads und Neustarts.
    /// </summary>
    public async Task<TimerSession?> GetActiveSessionAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.TimerSessions
            .AsNoTracking()
            .Include(s => s.Pauses)
            .Include(s => s.TimeProject)
            .Where(s => s.Status != TimerStatus.Stopped)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>Startet eine neue Live-Zeiterfassung. Wirft, wenn bereits eine läuft.</summary>
    public async Task<TimerSession> StartTimerAsync(int? projectId = null)
    {
        await using var db = await factory.CreateDbContextAsync();

        var exists = await db.TimerSessions.AnyAsync(s => s.Status != TimerStatus.Stopped);
        if (exists)
            throw new InvalidOperationException("Es läuft bereits eine Zeiterfassung.");

        var session = new TimerSession
        {
            TimeProjectId = projectId,
            StartedAt = DateTime.Now,
            Status = TimerStatus.Running
        };
        db.TimerSessions.Add(session);
        await db.SaveChangesAsync();
        logger.LogInformation("Live-Timer {Id} gestartet (Projekt {ProjectId}).", session.Id, projectId);
        return session;
    }

    /// <summary>Pausiert den laufenden Timer (öffnet eine neue Pause).</summary>
    public async Task PauseTimerAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var session = await LoadActiveTrackedAsync(db);
        if (session is null || session.Status != TimerStatus.Running) return;

        session.Pauses.Add(new TimerPause { PausedAt = DateTime.Now });
        session.Status = TimerStatus.Paused;
        await db.SaveChangesAsync();
    }

    /// <summary>Setzt einen pausierten Timer fort (schließt die offene Pause).</summary>
    public async Task ResumeTimerAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var session = await LoadActiveTrackedAsync(db);
        if (session is null || session.Status != TimerStatus.Paused) return;

        var open = session.Pauses.FirstOrDefault(p => p.ResumedAt is null);
        if (open is not null) open.ResumedAt = DateTime.Now;
        session.Status = TimerStatus.Running;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Beendet die Zeiterfassung und erzeugt daraus einen gespeicherten Zeiteintrag.
    /// </summary>
    public async Task<TimeEntry> StopTimerAsync(int projectId, string description, string? notes)
    {
        if (projectId <= 0)
            throw new ArgumentException("Bitte ein Projekt auswählen.", nameof(projectId));

        await using var db = await factory.CreateDbContextAsync();
        var session = await LoadActiveTrackedAsync(db);
        if (session is null)
            throw new InvalidOperationException("Es läuft keine Zeiterfassung.");

        var now = DateTime.Now;

        // Eventuell offene Pause schließen.
        var open = session.Pauses.FirstOrDefault(p => p.ResumedAt is null);
        if (open is not null) open.ResumedAt = now;

        session.EndedAt = now;
        session.Status = TimerStatus.Stopped;
        session.TimeProjectId = projectId;
        session.Description = description;
        session.Notes = notes;

        var breakMinutes = (int)Math.Round(session.TotalBreak(now).TotalMinutes);

        var entry = new TimeEntry
        {
            TimeProjectId = projectId,
            Date = DateOnly.FromDateTime(session.StartedAt),
            StartTime = TimeOnly.FromDateTime(session.StartedAt),
            EndTime = TimeOnly.FromDateTime(now),
            BreakMinutes = breakMinutes,
            Description = string.IsNullOrWhiteSpace(description) ? "(ohne Beschreibung)" : description.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            FromTimer = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        entry.RecalculateDuration();

        db.TimeEntries.Add(entry);
        await db.SaveChangesAsync();
        logger.LogInformation("Live-Timer {SessionId} beendet → Zeiteintrag {EntryId} ({Minutes} min).",
            session.Id, entry.Id, entry.DurationMinutes);
        return entry;
    }

    /// <summary>Verwirft die laufende Session ohne einen Zeiteintrag zu erzeugen.</summary>
    public async Task CancelTimerAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var session = await LoadActiveTrackedAsync(db);
        if (session is null) return;
        db.TimerSessions.Remove(session);
        await db.SaveChangesAsync();
        logger.LogInformation("Live-Timer {Id} verworfen.", session.Id);
    }

    private static async Task<TimerSession?> LoadActiveTrackedAsync(ZeiterfassungDbContext db) =>
        await db.TimerSessions
            .Include(s => s.Pauses)
            .Where(s => s.Status != TimerStatus.Stopped)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync();

    // ─────────────────────────────────────────────────────────────────────
    //  Dashboard-Kennzahlen
    // ─────────────────────────────────────────────────────────────────────

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var weekStart = StartOfWeek(today);
        var weekEnd = weekStart.AddDays(6);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        await using var db = await factory.CreateDbContextAsync();

        return new DashboardStats
        {
            TodayMinutes = await SumMinutesAsync(db, today, today),
            WeekMinutes = await SumMinutesAsync(db, weekStart, weekEnd),
            MonthMinutes = await SumMinutesAsync(db, monthStart, monthEnd),
            TotalMinutes = await db.TimeEntries.SumAsync(e => (int?)e.DurationMinutes) ?? 0,
            ActiveProjectCount = await db.TimeProjects.CountAsync(p => !p.IsArchived && p.Status == ProjectStatus.Active),
            TotalEntryCount = await db.TimeEntries.CountAsync()
        };
    }

    private static async Task<int> SumMinutesAsync(ZeiterfassungDbContext db, DateOnly from, DateOnly to) =>
        await db.TimeEntries
            .Where(e => e.Date >= from && e.Date <= to)
            .SumAsync(e => (int?)e.DurationMinutes) ?? 0;

    // ─────────────────────────────────────────────────────────────────────
    //  Kalender
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Liefert für jeden Tag des Monats die Tagessumme und die beteiligten Projektfarben.</summary>
    public async Task<Dictionary<DateOnly, CalendarDay>> GetCalendarAsync(int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.TimeEntries
            .AsNoTracking()
            .Where(e => e.Date >= monthStart && e.Date <= monthEnd)
            .Select(e => new { e.Date, e.DurationMinutes, Color = e.TimeProject!.Color })
            .ToListAsync();

        return rows
            .GroupBy(r => r.Date)
            .ToDictionary(g => g.Key, g => new CalendarDay
            {
                Date = g.Key,
                TotalMinutes = g.Sum(x => x.DurationMinutes),
                EntryCount = g.Count(),
                ProjectColors = g.Select(x => x.Color).Distinct().Take(3).ToList()
            });
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Auswertungen / Diagramme
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Stunden pro Projekt (optional auf einen Zeitraum begrenzt).</summary>
    public async Task<List<ProjectChartItem>> GetHoursPerProjectAsync(DateOnly? from = null, DateOnly? to = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.TimeEntries.AsNoTracking().AsQueryable();
        if (from is { } f) query = query.Where(e => e.Date >= f);
        if (to is { } t) query = query.Where(e => e.Date <= t);

        var rows = await query
            .GroupBy(e => new { e.TimeProjectId, e.TimeProject!.Name, e.TimeProject.Color })
            .Select(g => new ProjectChartItem(
                g.Key.TimeProjectId, g.Key.Name, g.Key.Color, g.Sum(e => e.DurationMinutes)))
            .ToListAsync();

        return rows.OrderByDescending(r => r.Minutes).ToList();
    }

    /// <summary>Stunden pro Kalenderwoche für die letzten <paramref name="weeks"/> Wochen.</summary>
    public async Task<List<PeriodChartItem>> GetHoursPerWeekAsync(int weeks = 8)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var currentWeekStart = StartOfWeek(today);
        var firstWeekStart = currentWeekStart.AddDays(-7 * (weeks - 1));

        var rows = await FetchDateMinutesAsync(firstWeekStart, currentWeekStart.AddDays(6));

        var result = new List<PeriodChartItem>();
        for (var i = 0; i < weeks; i++)
        {
            var wStart = firstWeekStart.AddDays(7 * i);
            var wEnd = wStart.AddDays(6);
            var minutes = rows.Where(r => r.Date >= wStart && r.Date <= wEnd).Sum(r => r.Minutes);
            result.Add(new PeriodChartItem($"KW {IsoWeek(wStart)}", minutes, wStart));
        }
        return result;
    }

    /// <summary>Stunden pro Monat für die letzten <paramref name="months"/> Monate.</summary>
    public async Task<List<PeriodChartItem>> GetHoursPerMonthAsync(int months = 6)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var lastMonthEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);

        var rows = await FetchDateMinutesAsync(firstMonth, lastMonthEnd);
        var germanCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");

        var result = new List<PeriodChartItem>();
        for (var i = 0; i < months; i++)
        {
            var mStart = firstMonth.AddMonths(i);
            var mEnd = mStart.AddMonths(1).AddDays(-1);
            var minutes = rows.Where(r => r.Date >= mStart && r.Date <= mEnd).Sum(r => r.Minutes);
            var label = mStart.ToDateTime(TimeOnly.MinValue).ToString("MMM yy", germanCulture);
            result.Add(new PeriodChartItem(label, minutes, mStart));
        }
        return result;
    }

    /// <summary>Täglicher Arbeitszeitverlauf der letzten <paramref name="days"/> Tage.</summary>
    public async Task<List<PeriodChartItem>> GetDailyTrendAsync(int days = 30)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var start = today.AddDays(-(days - 1));
        var rows = await FetchDateMinutesAsync(start, today);
        var byDay = rows.GroupBy(r => r.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.Minutes));

        var result = new List<PeriodChartItem>();
        for (var i = 0; i < days; i++)
        {
            var day = start.AddDays(i);
            byDay.TryGetValue(day, out var minutes);
            result.Add(new PeriodChartItem(day.ToString("dd.MM"), minutes, day));
        }
        return result;
    }

    private async Task<List<(DateOnly Date, int Minutes)>> FetchDateMinutesAsync(DateOnly from, DateOnly to)
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.TimeEntries
            .AsNoTracking()
            .Where(e => e.Date >= from && e.Date <= to)
            .Select(e => new { e.Date, e.DurationMinutes })
            .ToListAsync();
        return rows.Select(r => (r.Date, r.DurationMinutes)).ToList();
    }

    /// <summary>Liste aller Jahre, in denen Einträge existieren (für Jahr-Filter).</summary>
    public async Task<List<int>> GetYearsWithEntriesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var dates = await db.TimeEntries.AsNoTracking().Select(e => e.Date).ToListAsync();
        return dates.Select(d => d.Year).Distinct().OrderByDescending(y => y).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Hilfsfunktionen
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Montag als Wochenstart.</summary>
    public static DateOnly StartOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7; // Montag = 0
        return date.AddDays(-diff);
    }

    private static int IsoWeek(DateOnly date) =>
        System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
}
