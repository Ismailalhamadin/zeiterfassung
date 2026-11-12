namespace Zeiterfassung.Web.Data.Models;

/// <summary>
/// Lebenszyklus-Status eines Zeiterfassungs-Projekts.
/// </summary>
public enum ProjectStatus
{
    /// <summary>Projekt ist aktiv und kann bebucht werden.</summary>
    Active = 0,

    /// <summary>Projekt ist inhaltlich abgeschlossen.</summary>
    Completed = 1
}

/// <summary>
/// Status einer laufenden Live-Zeiterfassungs-Session.
/// </summary>
public enum TimerStatus
{
    /// <summary>Timer läuft.</summary>
    Running = 0,

    /// <summary>Timer ist pausiert (mind. eine offene Pause).</summary>
    Paused = 1,

    /// <summary>Timer wurde beendet und in einen Zeiteintrag überführt.</summary>
    Stopped = 2
}
