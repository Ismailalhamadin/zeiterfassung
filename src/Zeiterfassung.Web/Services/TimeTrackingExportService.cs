using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Web.Data;
using Zeiterfassung.Web.Data.Models;

namespace Zeiterfassung.Web.Services;

/// <summary>Ergebnis eines Exports: fertige Datei-Bytes samt Metadaten.</summary>
public record ExportResult(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Erzeugt den CSV-Export der (gefilterten) Zeiteinträge. Excel und PDF gibt es nur in der
/// Vollversion; in der Browser-Demo bleibt es bei CSV (läuft ohne Zusatzpakete).
/// </summary>
public class TimeTrackingExportService(IDbContextFactory<ZeiterfassungDbContext> factory)
{
    private static readonly CultureInfo De = new("de-DE");

    private static readonly string[] Headers =
        { "Datum", "Projekt", "Start", "Ende", "Pause (min)", "Dauer (h)", "Beschreibung", "Notizen" };

    /// <summary>Lädt die gefilterten Einträge und erzeugt die CSV-Datei.</summary>
    public async Task<ExportResult> ExportCsvAsync(TimeEntryFilter filter)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entries = await TimeTrackingService.BuildEntryQuery(db, filter)
            .OrderBy(e => e.Date).ThenBy(e => e.StartTime)
            .ToListAsync();

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        return new ExportResult(BuildCsv(entries), "text/csv; charset=utf-8", $"zeiterfassung-{stamp}.csv");
    }

    /// <summary>Semikolon-getrennt (deutsches Excel-Format), UTF-8 mit BOM für Umlaute.</summary>
    public static byte[] BuildCsv(IReadOnlyList<TimeEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', Headers.Select(CsvEscape)));

        foreach (var e in entries)
        {
            sb.AppendLine(string.Join(';', new[]
            {
                CsvEscape(e.Date.ToString("dd.MM.yyyy", De)),
                CsvEscape(e.TimeProject?.Name ?? ""),
                CsvEscape(e.StartTime.ToString("HH:mm", De)),
                CsvEscape(e.EndTime.ToString("HH:mm", De)),
                CsvEscape(e.BreakMinutes.ToString(De)),
                CsvEscape((e.DurationMinutes / 60.0).ToString("0.00", De)),
                CsvEscape(e.Description),
                CsvEscape(e.Notes ?? "")
            }));
        }

        var totalHours = entries.Sum(e => e.DurationMinutes) / 60.0;
        sb.AppendLine();
        sb.AppendLine($"Summe;;;;;{CsvEscape(totalHours.ToString("0.00", De))};;");

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString().ReplaceLineEndings("\r\n"))).ToArray();
    }

    private static string CsvEscape(string value)
    {
        // Zellen, die mit = + - @ beginnen, würde Excel als Formel auslesen: entschärfen.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
            value = "'" + value;
        if (value.Contains('"') || value.Contains(';') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
