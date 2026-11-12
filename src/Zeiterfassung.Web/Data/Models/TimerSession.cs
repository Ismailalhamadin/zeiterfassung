namespace Zeiterfassung.Web.Data.Models;

/// <summary>
/// Eine laufende bzw. abgeschlossene Live-Zeiterfassung.
/// Da der komplette Zustand serverseitig in der Datenbank liegt, läuft der Timer
/// nach Seiten-Reload oder Browser-Neustart korrekt weiter — die vergangene Zeit
/// wird immer aus <see cref="StartedAt"/> und den Pausen berechnet, nie im Client gehalten.
/// </summary>
public class TimerSession
{
    public int Id { get; set; }

    /// <summary>Optionales Projekt. Kann beim Start noch offen sein und erst beim Beenden gewählt werden.</summary>
    public int? TimeProjectId { get; set; }
    public TimeProject? TimeProject { get; set; }

    /// <summary>Startzeitpunkt (lokale Serverzeit).</summary>
    public DateTime StartedAt { get; set; } = DateTime.Now;

    /// <summary>Endzeitpunkt; <c>null</c> solange der Timer läuft oder pausiert ist.</summary>
    public DateTime? EndedAt { get; set; }

    public TimerStatus Status { get; set; } = TimerStatus.Running;

    /// <summary>Beschreibung (wird üblicherweise erst beim Beenden erfasst).</summary>
    public string? Description { get; set; }

    /// <summary>Notizen (optional).</summary>
    public string? Notes { get; set; }

    /// <summary>Alle Pausen dieser Session.</summary>
    public ICollection<TimerPause> Pauses { get; set; } = new List<TimerPause>();

    /// <summary>Gesamte Pausendauer bis <paramref name="now"/> (offene Pause zählt live weiter).</summary>
    public TimeSpan TotalBreak(DateTime now)
    {
        var total = TimeSpan.Zero;
        foreach (var p in Pauses)
            total += (p.ResumedAt ?? now) - p.PausedAt;
        return total < TimeSpan.Zero ? TimeSpan.Zero : total;
    }

    /// <summary>Netto-Arbeitszeit bis <paramref name="now"/> (Gesamtzeit abzüglich Pausen).</summary>
    public TimeSpan Worked(DateTime now)
    {
        var end = EndedAt ?? now;
        var worked = (end - StartedAt) - TotalBreak(now);
        return worked < TimeSpan.Zero ? TimeSpan.Zero : worked;
    }
}
