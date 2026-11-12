namespace Zeiterfassung.Web.Services;

/// <summary>Einheitliche Formatierung von Zeitdauern in der gesamten Zeiterfassung.</summary>
public static class TimeFormat
{
    /// <summary>Formatiert Minuten als „Xh Ym" (z. B. 135 → „2h 15m").</summary>
    public static string Hm(int totalMinutes)
    {
        if (totalMinutes <= 0) return "0m";
        var h = totalMinutes / 60;
        var m = totalMinutes % 60;
        if (h == 0) return $"{m}m";
        if (m == 0) return $"{h}h";
        return $"{h}h {m}m";
    }

    /// <summary>Formatiert Minuten als Dezimalstunden (z. B. 135 → „2,25 h").</summary>
    public static string DecimalHours(int totalMinutes) =>
        $"{totalMinutes / 60.0:0.00} h";

    /// <summary>Formatiert eine <see cref="TimeSpan"/> als HH:mm:ss für den Live-Timer.</summary>
    public static string Clock(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        return $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
    }
}
