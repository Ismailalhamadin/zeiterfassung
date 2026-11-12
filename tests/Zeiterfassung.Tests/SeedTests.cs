using System.Text.RegularExpressions;
using Zeiterfassung.Web.Data;
using Zeiterfassung.Web.Data.Models;

namespace Zeiterfassung.Tests;

public class SeedTests
{
    // Donnerstag, 12.11.2026, 14:30. Dazu ein früher Morgen und ein Montag.
    public static TheoryData<DateTime> Zeitpunkte => new()
    {
        new DateTime(2026, 11, 12, 14, 30, 0),
        new DateTime(2026, 11, 12, 6, 5, 0),
        new DateTime(2026, 11, 9, 23, 50, 0),
        new DateTime(2027, 3, 1, 12, 0, 0),
    };

    [Fact]
    public void Build_legt_vier_Projekte_mit_den_erwarteten_Namen_an()
    {
        var data = DemoSeed.Build(new DateTime(2026, 11, 12, 14, 30, 0));

        Assert.Equal(
            ["Schulprojekt Website", "Lernen .NET", "Praktikum", "Sport"],
            data.Projects.Select(p => p.Name));
        Assert.All(data.Projects, p => Assert.Matches("^#[0-9A-Fa-f]{6}$", p.Color));
        Assert.Equal(4, data.Projects.Select(p => p.Color).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Zeitpunkte))]
    public void Build_erzeugt_nichts_in_der_Zukunft(DateTime now)
    {
        var data = DemoSeed.Build(now);

        var today = DateOnly.FromDateTime(now);
        Assert.All(data.Entries, e =>
        {
            Assert.True(e.Date <= today, $"Eintrag {e.Date} liegt nach heute.");
            if (e.Date == today)
                Assert.True(e.EndTime.ToTimeSpan() < now.TimeOfDay, "Heutiger Eintrag endet nach der aktuellen Uhrzeit.");
        });
    }

    [Theory]
    [MemberData(nameof(Zeitpunkte))]
    public void Build_deckt_etwa_acht_Wochen_ab(DateTime now)
    {
        var data = DemoSeed.Build(now);
        var today = DateOnly.FromDateTime(now);

        Assert.InRange(data.Entries.Count, 80, 160);
        var oldest = data.Entries.Min(e => e.Date);
        Assert.True(oldest >= today.AddDays(-DemoSeed.Days));
        Assert.True(oldest <= today.AddDays(-DemoSeed.Days + 7), "Die ältesten Einträge sollten fast acht Wochen zurückliegen.");
        // Jedes Projekt kommt vor.
        Assert.All(data.Projects, p => Assert.Contains(data.Entries, e => e.TimeProject == p));
    }

    [Fact]
    public void Build_kuerzt_heutige_Eintraege_statt_sie_wegzulassen()
    {
        // Donnerstag 15:00: Das Praktikum (08:00 bis 16:00) läuft noch und wird auf 14:00 gekürzt.
        var now = new DateTime(2026, 11, 12, 15, 0, 0);
        var heute = DemoSeed.Build(now).Entries.Where(e => e.Date == DateOnly.FromDateTime(now)).ToList();

        Assert.All(heute, e => Assert.True(e.EndTime <= new TimeOnly(14, 0)));
        Assert.All(heute, e => Assert.True(e.DurationMinutes >= 30));
    }

    [Fact]
    public void Build_Einträge_haben_stimmige_Dauer()
    {
        var data = DemoSeed.Build(new DateTime(2026, 11, 12, 14, 30, 0));

        Assert.All(data.Entries, e =>
        {
            Assert.True(e.DurationMinutes > 0);
            Assert.Equal(e.GrossMinutes - e.BreakMinutes, e.DurationMinutes);
            Assert.False(string.IsNullOrWhiteSpace(e.Description));
            Assert.True(e.EndTime > e.StartTime);
        });
    }

    [Fact]
    public void Build_bringt_genau_einen_laufenden_Timer_mit_geschlossener_Pause()
    {
        var now = new DateTime(2026, 11, 12, 14, 30, 0);
        var timer = DemoSeed.Build(now).Timer;

        Assert.Equal(TimerStatus.Running, timer.Status);
        Assert.Null(timer.EndedAt);
        Assert.Equal(now.AddMinutes(-47), timer.StartedAt);
        var pause = Assert.Single(timer.Pauses);
        Assert.NotNull(pause.ResumedAt);
        Assert.Equal(TimeSpan.FromMinutes(6), timer.TotalBreak(now));
    }

    [Fact]
    public void Build_ist_deterministisch_und_wandert_mit_dem_Datum()
    {
        var now = new DateTime(2026, 11, 12, 14, 30, 0);
        var a = DemoSeed.Build(now);
        var b = DemoSeed.Build(now);
        Assert.Equal(
            a.Entries.Select(e => (e.Date, e.StartTime, e.DurationMinutes, e.Description)),
            b.Entries.Select(e => (e.Date, e.StartTime, e.DurationMinutes, e.Description)));

        // 14 Tage später: derselbe Wochentag-Rhythmus, die Daten rücken mit.
        var later = DemoSeed.Build(now.AddDays(14));
        Assert.True(later.Entries.Max(e => e.Date) > a.Entries.Max(e => e.Date));
        Assert.True(later.Entries.Min(e => e.Date) > a.Entries.Min(e => e.Date));
    }

    [Fact]
    public void Build_enthaelt_keine_echten_Kontaktdaten_oder_Adressen()
    {
        var data = DemoSeed.Build(new DateTime(2026, 11, 12, 14, 30, 0));
        var texts = data.Projects.SelectMany(p => new[] { p.Name, p.Description ?? "" })
            .Concat(data.Entries.SelectMany(e => new[] { e.Description, e.Notes ?? "" }));

        // E-Mail, Web-Adresse, IP, Telefonnummer
        var verboten = new Regex(@"@|https?://|www\.|\b\d{1,3}(\.\d{1,3}){3}\b|\+\d{6,}", RegexOptions.IgnoreCase);
        Assert.All(texts, t => Assert.DoesNotMatch(verboten, t));
    }
}
