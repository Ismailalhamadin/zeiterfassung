using Zeiterfassung.Web.Data.Models;
using Zeiterfassung.Web.Services;

namespace Zeiterfassung.Tests;

/// <summary>Summen je Tag, Woche, Monat und Projekt sowie Dauerberechnung.</summary>
public class CalculationTests
{
    [Theory]
    [InlineData(9, 0, 17, 0, 30, 450)]   // 8 h brutto, 30 min Pause
    [InlineData(9, 0, 9, 0, 0, 0)]        // gleiche Zeit
    [InlineData(22, 0, 2, 0, 0, 240)]     // über Mitternacht
    [InlineData(9, 0, 9, 30, 45, 0)]      // Pause länger als Arbeit: nie negativ
    public void RecalculateDuration_rechnet_netto(int sh, int sm, int eh, int em, int pause, int erwartet)
    {
        var e = new TimeEntry { StartTime = new TimeOnly(sh, sm), EndTime = new TimeOnly(eh, em), BreakMinutes = pause };
        e.RecalculateDuration();
        Assert.Equal(erwartet, e.DurationMinutes);
    }

    [Theory]
    [InlineData("2026-11-09", "2026-11-09")] // Montag
    [InlineData("2026-11-12", "2026-11-09")] // Donnerstag
    [InlineData("2026-11-15", "2026-11-09")] // Sonntag gehört zur Woche davor
    [InlineData("2026-11-16", "2026-11-16")] // nächster Montag
    public void StartOfWeek_ist_der_Montag(string datum, string montag)
    {
        Assert.Equal(DateOnly.Parse(montag), TimeTrackingService.StartOfWeek(DateOnly.Parse(datum)));
    }

    [Fact]
    public async Task Dashboard_summiert_Heute_Woche_Monat_und_Gesamt()
    {
        var db = new TestDb();
        var service = db.Service();
        var p = await db.AddProjectAsync();
        var today = TestDb.Today;
        var weekStart = TimeTrackingService.StartOfWeek(today);

        await db.AddEntryAsync(p, today, 8, 60);              // heute
        await db.AddEntryAsync(p, weekStart, 8, 120);         // Montag dieser Woche (ist evtl. heute)
        await db.AddEntryAsync(p, today.AddDays(-90), 8, 300); // lange her: nur Gesamt

        var stats = await service.GetDashboardStatsAsync();

        Assert.Equal(60 + (weekStart == today ? 120 : 0), stats.TodayMinutes);
        Assert.Equal(180, stats.WeekMinutes);
        Assert.Equal(480, stats.TotalMinutes);
        Assert.Equal(3, stats.TotalEntryCount);
        Assert.Equal(1, stats.ActiveProjectCount);
    }

    [Fact]
    public async Task Stunden_pro_Projekt_summiert_und_sortiert_absteigend()
    {
        var db = new TestDb();
        var a = await db.AddProjectAsync("A", "#111111");
        var b = await db.AddProjectAsync("B", "#222222");
        var today = TestDb.Today;
        await db.AddEntryAsync(a, today.AddDays(-1), 8, 60);
        await db.AddEntryAsync(a, today.AddDays(-2), 8, 30);
        await db.AddEntryAsync(b, today.AddDays(-1), 8, 200);

        var result = await db.Service().GetHoursPerProjectAsync();

        Assert.Equal(["B", "A"], result.Select(r => r.Name));
        Assert.Equal([200, 90], result.Select(r => r.Minutes));
    }

    [Fact]
    public async Task Stunden_pro_Projekt_beachtet_den_Zeitraum()
    {
        var db = new TestDb();
        var a = await db.AddProjectAsync();
        var today = TestDb.Today;
        await db.AddEntryAsync(a, today.AddDays(-20), 8, 100);
        await db.AddEntryAsync(a, today.AddDays(-2), 8, 40);

        var result = await db.Service().GetHoursPerProjectAsync(today.AddDays(-5), today);

        Assert.Equal(40, Assert.Single(result).Minutes);
    }

    [Fact]
    public async Task Stunden_pro_Woche_liefert_acht_Wochen_mit_richtigen_Summen()
    {
        var db = new TestDb();
        var p = await db.AddProjectAsync();
        var thisWeek = TimeTrackingService.StartOfWeek(TestDb.Today);
        await db.AddEntryAsync(p, thisWeek, 8, 60);
        await db.AddEntryAsync(p, thisWeek.AddDays(-7), 8, 90);
        await db.AddEntryAsync(p, thisWeek.AddDays(-7).AddDays(6), 8, 30); // Sonntag der Vorwoche
        await db.AddEntryAsync(p, thisWeek.AddDays(-7 * 9), 8, 500);       // zu alt, nicht dabei

        var weeks = await db.Service().GetHoursPerWeekAsync(8);

        Assert.Equal(8, weeks.Count);
        Assert.Equal(thisWeek, weeks[^1].Start);
        Assert.Equal(60, weeks[^1].Minutes);
        Assert.Equal(120, weeks[^2].Minutes);
        Assert.Equal(180, weeks.Sum(w => w.Minutes));
    }

    [Fact]
    public async Task Stunden_pro_Monat_trennt_die_Monate()
    {
        var db = new TestDb();
        var p = await db.AddProjectAsync();
        var firstOfMonth = new DateOnly(TestDb.Today.Year, TestDb.Today.Month, 1);
        await db.AddEntryAsync(p, firstOfMonth, 8, 45);
        await db.AddEntryAsync(p, firstOfMonth.AddDays(-1), 8, 75); // Vormonat

        var months = await db.Service().GetHoursPerMonthAsync(3);

        Assert.Equal(3, months.Count);
        Assert.Equal(45, months[^1].Minutes);
        Assert.Equal(75, months[^2].Minutes);
    }

    [Fact]
    public async Task Tagesverlauf_hat_je_Tag_einen_Punkt_und_summiert_mehrere_Eintraege()
    {
        var db = new TestDb();
        var p = await db.AddProjectAsync();
        var today = TestDb.Today;
        await db.AddEntryAsync(p, today, 7, 30);
        await db.AddEntryAsync(p, today, 9, 45);

        var trend = await db.Service().GetDailyTrendAsync(30);

        Assert.Equal(30, trend.Count);
        Assert.Equal(75, trend[^1].Minutes);
        Assert.Equal(75, trend.Sum(t => t.Minutes));
    }

    [Fact]
    public async Task Kalender_liefert_Tagessumme_Anzahl_und_Farben()
    {
        var db = new TestDb();
        var a = await db.AddProjectAsync("A", "#AA0000");
        var b = await db.AddProjectAsync("B", "#00BB00");
        var day = new DateOnly(TestDb.Today.Year, TestDb.Today.Month, 1);
        await db.AddEntryAsync(a, day, 8, 60);
        await db.AddEntryAsync(a, day, 10, 30);
        await db.AddEntryAsync(b, day, 12, 15);

        var cal = await db.Service().GetCalendarAsync(day.Year, day.Month);

        var d = Assert.Single(cal).Value;
        Assert.Equal(105, d.TotalMinutes);
        Assert.Equal(3, d.EntryCount);
        Assert.Equal(2, d.ProjectColors.Count);
    }

    [Fact]
    public async Task Jahre_mit_Eintraegen_absteigend()
    {
        var db = new TestDb();
        var p = await db.AddProjectAsync();
        await db.AddEntryAsync(p, new DateOnly(2024, 5, 5), 8, 10);
        await db.AddEntryAsync(p, new DateOnly(2026, 1, 5), 8, 10);
        await db.AddEntryAsync(p, new DateOnly(2026, 6, 5), 8, 10);

        Assert.Equal([2026, 2024], await db.Service().GetYearsWithEntriesAsync());
    }

    [Theory]
    [InlineData(0, "0m")]
    [InlineData(-5, "0m")]
    [InlineData(5, "5m")]
    [InlineData(60, "1h")]
    [InlineData(135, "2h 15m")]
    public void TimeFormat_Hm_formatiert_Minuten(int minuten, string erwartet)
    {
        Assert.Equal(erwartet, TimeFormat.Hm(minuten));
    }

    [Fact]
    public void TimeFormat_Clock_zeigt_Stunden_Minuten_Sekunden_und_nie_negativ()
    {
        Assert.Equal("01:02:03", TimeFormat.Clock(new TimeSpan(1, 2, 3)));
        Assert.Equal("26:00:00", TimeFormat.Clock(TimeSpan.FromHours(26)));
        Assert.Equal("00:00:00", TimeFormat.Clock(TimeSpan.FromSeconds(-9)));
    }
}
