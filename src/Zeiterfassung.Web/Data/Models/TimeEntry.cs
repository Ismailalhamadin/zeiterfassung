using System.ComponentModel.DataAnnotations;

namespace Zeiterfassung.Web.Data.Models;

/// <summary>
/// Ein einzelner Zeiteintrag innerhalb eines Projekts.
/// Kann manuell erfasst oder automatisch aus einer <see cref="TimerSession"/> erzeugt werden.
/// </summary>
public class TimeEntry
{
    public int Id { get; set; }

    /// <summary>Fremdschlüssel auf das Projekt.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Bitte ein Projekt auswählen.")]
    public int TimeProjectId { get; set; }

    /// <summary>Navigations-Property auf das Projekt.</summary>
    public TimeProject? TimeProject { get; set; }

    /// <summary>Arbeitsdatum.</summary>
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Start-Uhrzeit (Wanduhr).</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>End-Uhrzeit (Wanduhr).</summary>
    public TimeOnly EndTime { get; set; }

    /// <summary>Reine Pausenzeit in Minuten (0 bei manuellen Einträgen ohne Pause).</summary>
    [Range(0, 24 * 60, ErrorMessage = "Die Pausenzeit ist ungültig.")]
    public int BreakMinutes { get; set; }

    /// <summary>
    /// Netto-Arbeitszeit in Minuten (Quelle der Wahrheit für alle Auswertungen).
    /// Wird beim Speichern automatisch berechnet: (Ende − Start) − Pause.
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>Beschreibung der erledigten Arbeit.</summary>
    [Required(ErrorMessage = "Bitte beschreiben, was gemacht wurde.")]
    [StringLength(500, ErrorMessage = "Die Beschreibung darf maximal 500 Zeichen lang sein.")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Optionale Notizen.</summary>
    [StringLength(1000, ErrorMessage = "Die Notizen dürfen maximal 1000 Zeichen lang sein.")]
    public string? Notes { get; set; }

    /// <summary>Wurde dieser Eintrag aus der Live-Zeiterfassung erzeugt?</summary>
    public bool FromTimer { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>Bruttozeit (Ende − Start) in Minuten, inkl. Pausen. Nur Anzeige, nicht persistiert.</summary>
    public int GrossMinutes
    {
        get
        {
            var minutes = (int)(EndTime.ToTimeSpan() - StartTime.ToTimeSpan()).TotalMinutes;
            // Über Mitternacht laufende Einträge korrekt behandeln.
            if (minutes < 0) minutes += 24 * 60;
            return minutes;
        }
    }

    /// <summary>Berechnet die Netto-Arbeitszeit aus Start/Ende/Pause und legt sie in <see cref="DurationMinutes"/> ab.</summary>
    public void RecalculateDuration()
    {
        var net = GrossMinutes - BreakMinutes;
        DurationMinutes = net < 0 ? 0 : net;
    }
}
