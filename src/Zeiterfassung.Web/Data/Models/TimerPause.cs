namespace Zeiterfassung.Web.Data.Models;

/// <summary>
/// Eine einzelne Pause innerhalb einer <see cref="TimerSession"/>.
/// Mehrere Pausen pro Session sind möglich.
/// </summary>
public class TimerPause
{
    public int Id { get; set; }

    /// <summary>Fremdschlüssel auf die Session.</summary>
    public int TimerSessionId { get; set; }
    public TimerSession? TimerSession { get; set; }

    /// <summary>Beginn der Pause.</summary>
    public DateTime PausedAt { get; set; } = DateTime.Now;

    /// <summary>Ende der Pause; <c>null</c> solange die Pause andauert.</summary>
    public DateTime? ResumedAt { get; set; }
}
