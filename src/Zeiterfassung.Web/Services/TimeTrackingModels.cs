using Zeiterfassung.Web.Data.Models;

namespace Zeiterfassung.Web.Services;

/// <summary>Kennzahlen für das Dashboard.</summary>
public class DashboardStats
{
    public int TodayMinutes { get; set; }
    public int WeekMinutes { get; set; }
    public int MonthMinutes { get; set; }
    public int TotalMinutes { get; set; }
    public int ActiveProjectCount { get; set; }
    public int TotalEntryCount { get; set; }
}

/// <summary>Filter für die Zeiteintrags-Liste und den Export.</summary>
public class TimeEntryFilter
{
    /// <summary>Nur Einträge dieses Projekts (null = alle).</summary>
    public int? ProjectId { get; set; }

    /// <summary>Zeitraum ab (inklusive).</summary>
    public DateOnly? From { get; set; }

    /// <summary>Zeitraum bis (inklusive).</summary>
    public DateOnly? To { get; set; }

    /// <summary>Bestimmter Monat (1–12). Wird zusammen mit <see cref="Year"/> ausgewertet.</summary>
    public int? Month { get; set; }

    /// <summary>Bestimmtes Jahr.</summary>
    public int? Year { get; set; }

    /// <summary>Freitextsuche in Projektname, Beschreibung und Notizen.</summary>
    public string? Search { get; set; }
}

/// <summary>Ein Segment eines Balken-/Kreisdiagramms (Stunden pro Projekt).</summary>
public record ProjectChartItem(int ProjectId, string Name, string Color, int Minutes);

/// <summary>Ein Datenpunkt einer Zeitreihe (pro Tag / Woche / Monat).</summary>
public record PeriodChartItem(string Label, int Minutes, DateOnly Start);

/// <summary>Ein Tag in der Kalenderansicht inkl. Tagessumme.</summary>
public class CalendarDay
{
    public DateOnly Date { get; init; }
    public int TotalMinutes { get; set; }
    public int EntryCount { get; set; }

    /// <summary>Bis zu drei Projektfarben für farbige Tages-Punkte.</summary>
    public List<string> ProjectColors { get; set; } = new();
}
