using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Web.Data;
using Zeiterfassung.Web.Data.Models;
using Zeiterfassung.Web.Services;

namespace Zeiterfassung.Tests;

/// <summary>Anlegen, Ändern, Löschen, Suche, Timer, Export und Zurücksetzen gegen die InMemory-Datenbank.</summary>
public class ServiceTests
{
    [Fact]
    public async Task Eintrag_anlegen_aendern_loeschen()
    {
        var db = new TestDb();
        var service = db.Service();
        var p = await db.AddProjectAsync();

        var added = await service.AddEntryAsync(new TimeEntry
        {
            TimeProjectId = p.Id, Date = TestDb.Today,
            StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 30), BreakMinutes = 30,
            Description = "Erster Eintrag"
        });
        Assert.True(added.Id > 0);
        Assert.Equal(120, (await service.GetEntryByIdAsync(added.Id))!.DurationMinutes);

        var edit = (await service.GetEntryByIdAsync(added.Id))!;
        edit.TimeProject = null;
        edit.EndTime = new TimeOnly(12, 30);
        edit.Description = "Geändert";
        await service.UpdateEntryAsync(edit);
        var changed = (await service.GetEntryByIdAsync(added.Id))!;
        Assert.Equal("Geändert", changed.Description);
        Assert.Equal(180, changed.DurationMinutes);

        await service.DeleteEntryAsync(added.Id);
        Assert.Null(await service.GetEntryByIdAsync(added.Id));
    }

    [Fact]
    public async Task Projekt_anlegen_archivieren_und_Filter()
    {
        var db = new TestDb();
        var service = db.Service();
        var p = await service.AddProjectAsync(new TimeProject { Name = "Alpha", Color = "#111111" });
        await service.AddProjectAsync(new TimeProject { Name = "Beta", Color = "#222222", Description = "enthält Alpha-Text" });

        Assert.Equal(2, (await service.GetProjectsAsync()).Count);
        Assert.Equal(2, (await service.GetProjectsAsync(search: "alpha")).Count); // Name und Beschreibung, ohne Groß-/Kleinschreibung

        await service.SetArchivedAsync(p.Id, true);
        Assert.Single(await service.GetProjectsAsync());
        Assert.Equal(2, (await service.GetProjectsAsync(includeArchived: true)).Count);
    }

    [Fact]
    public async Task Suche_findet_Beschreibung_Notiz_und_Projektname_ohne_Gross_Kleinschreibung()
    {
        var db = new TestDb();
        var a = await db.AddProjectAsync("Lernen .NET");
        var b = await db.AddProjectAsync("Sport");
        await db.AddEntryAsync(a, TestDb.Today, 8, 60, description: "Blazor-Komponenten geübt");
        await db.AddEntryAsync(b, TestDb.Today, 10, 60, description: "Laufen");

        var service = db.Service();
        Assert.Single(await service.GetEntriesAsync(new TimeEntryFilter { Search = "blazor" }));
        Assert.Single(await service.GetEntriesAsync(new TimeEntryFilter { Search = "SPORT" }));
        Assert.Empty(await service.GetEntriesAsync(new TimeEntryFilter { Search = "gibtesnicht" }));
    }

    [Fact]
    public async Task Filter_nach_Projekt_Zeitraum_Monat_und_Jahr()
    {
        var db = new TestDb();
        var a = await db.AddProjectAsync("A");
        var b = await db.AddProjectAsync("B");
        await db.AddEntryAsync(a, new DateOnly(2026, 3, 10), 8, 60);
        await db.AddEntryAsync(a, new DateOnly(2026, 4, 10), 8, 60);
        await db.AddEntryAsync(b, new DateOnly(2025, 3, 10), 8, 60);
        var service = db.Service();

        Assert.Equal(2, (await service.GetEntriesAsync(new TimeEntryFilter { ProjectId = a.Id })).Count);
        Assert.Single(await service.GetEntriesAsync(new TimeEntryFilter { Year = 2026, Month = 3 }));
        Assert.Equal(2, (await service.GetEntriesAsync(new TimeEntryFilter { Year = 2026 })).Count);
        Assert.Single(await service.GetEntriesAsync(new TimeEntryFilter { From = new DateOnly(2026, 4, 1), To = new DateOnly(2026, 4, 30) }));
        // neueste zuerst
        var all = await service.GetEntriesAsync(new TimeEntryFilter());
        Assert.Equal(all.OrderByDescending(e => e.Date).Select(e => e.Id), all.Select(e => e.Id));
    }

    [Fact]
    public async Task Projekt_loeschen_entfernt_Eintraege_und_loest_Timer_vom_Projekt()
    {
        var db = new TestDb();
        var service = db.Service();
        var keep = await db.AddProjectAsync("Bleibt");
        var gone = await db.AddProjectAsync("Weg");
        await db.AddEntryAsync(keep, TestDb.Today, 8, 60);
        await db.AddEntryAsync(gone, TestDb.Today, 10, 60);
        await db.AddEntryAsync(gone, TestDb.Today.AddDays(-1), 10, 60);
        await service.StartTimerAsync(gone.Id);

        await service.DeleteProjectAsync(gone.Id);

        await using var ctx = db.CreateDbContext();
        Assert.Equal(1, await ctx.TimeEntries.CountAsync());
        Assert.Equal(keep.Id, (await ctx.TimeEntries.SingleAsync()).TimeProjectId);
        Assert.Equal(1, await ctx.TimeProjects.CountAsync());
        Assert.Null((await ctx.TimerSessions.SingleAsync()).TimeProjectId);
    }

    [Fact]
    public async Task Timer_Start_Pause_Fortsetzen_Stopp_erzeugt_Eintrag()
    {
        var db = new TestDb();
        var service = db.Service();
        var p = await db.AddProjectAsync();

        Assert.Null(await service.GetActiveSessionAsync());
        await service.StartTimerAsync(p.Id);
        Assert.Equal(TimerStatus.Running, (await service.GetActiveSessionAsync())!.Status);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartTimerAsync(p.Id));

        await service.PauseTimerAsync();
        var paused = (await service.GetActiveSessionAsync())!;
        Assert.Equal(TimerStatus.Paused, paused.Status);
        Assert.Single(paused.Pauses);
        Assert.Null(paused.Pauses.Single().ResumedAt);

        await service.ResumeTimerAsync();
        var resumed = (await service.GetActiveSessionAsync())!;
        Assert.Equal(TimerStatus.Running, resumed.Status);
        Assert.NotNull(resumed.Pauses.Single().ResumedAt);

        var entry = await service.StopTimerAsync(p.Id, "  Gelernt  ", null);
        Assert.True(entry.FromTimer);
        Assert.Equal("Gelernt", entry.Description);
        Assert.Null(await service.GetActiveSessionAsync());
        Assert.Single(await service.GetEntriesAsync(new TimeEntryFilter()));
    }

    [Fact]
    public async Task Timer_Stopp_ohne_Beschreibung_und_ohne_Projekt()
    {
        var db = new TestDb();
        var service = db.Service();
        var p = await db.AddProjectAsync();
        await service.StartTimerAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => service.StopTimerAsync(0, "x", null));
        var entry = await service.StopTimerAsync(p.Id, "", null);
        Assert.Equal("(ohne Beschreibung)", entry.Description);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StopTimerAsync(p.Id, "x", null));
    }

    [Fact]
    public async Task Timer_verwerfen_loescht_Session_samt_Pausen_ohne_Eintrag()
    {
        var db = new TestDb();
        var service = db.Service();
        await service.StartTimerAsync();
        await service.PauseTimerAsync();

        await service.CancelTimerAsync();

        await using var ctx = db.CreateDbContext();
        Assert.Empty(ctx.TimerSessions);
        Assert.Empty(ctx.TimerPauses);
        Assert.Empty(ctx.TimeEntries);
    }

    [Fact]
    public async Task Export_CSV_hat_Kopf_Zeilen_und_Summe_im_deutschen_Format()
    {
        var db = new TestDb();
        var p = await db.AddProjectAsync("Lernen; .NET");
        await db.AddEntryAsync(p, new DateOnly(2026, 11, 2), 9, 90, breakMinutes: 15, description: "Mit \"Anführung\"");
        await db.AddEntryAsync(p, new DateOnly(2026, 11, 3), 9, 30, description: "=SUMME(A1)");

        var result = await db.Export().ExportCsvAsync(new TimeEntryFilter());
        var text = System.Text.Encoding.UTF8.GetString(result.Content).TrimStart('﻿');
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("zeiterfassung-", result.FileName);
        Assert.EndsWith(".csv", result.FileName);
        Assert.Equal("Datum;Projekt;Start;Ende;Pause (min);Dauer (h);Beschreibung;Notizen", lines[0]);
        Assert.Contains("02.11.2026;\"Lernen; .NET\";09:00;10:45;15;1,50;\"Mit \"\"Anführung\"\"\";", lines[1]);
        Assert.DoesNotContain(lines, l => l.Contains(";=SUMME"));      // Formel entschärft
        Assert.Contains("'=SUMME(A1)", text);
        Assert.StartsWith("Summe;;;;;2,00", lines[^1]);
    }

    [Fact]
    public async Task Seed_fuellt_die_Datenbank_und_Neu_Seeden_dupliziert_nichts()
    {
        var db = new TestDb();
        var now = new DateTime(2026, 11, 12, 14, 30, 0);
        var expected = DemoSeed.Build(now).Entries.Count;

        await DemoSeed.SeedAsync(db, now);
        await DemoSeed.SeedAsync(db, now); // Zurücksetzen

        var service = db.Service();
        Assert.Equal(expected, (await service.GetDashboardStatsAsync()).TotalEntryCount);
        Assert.Equal(4, (await service.GetProjectsAsync()).Count);
        var timer = await service.GetActiveSessionAsync();
        Assert.NotNull(timer);
        Assert.Equal("Lernen .NET", timer!.TimeProject!.Name);
        Assert.Single(timer.Pauses);
    }

    [Fact]
    public async Task Aenderungen_nach_dem_Seed_gehen_beim_Zuruecksetzen_verloren()
    {
        var db = new TestDb();
        var now = new DateTime(2026, 11, 12, 14, 30, 0);
        await DemoSeed.SeedAsync(db, now);
        var service = db.Service();
        var project = (await service.GetProjectsAsync()).First();
        await service.AddEntryAsync(new TimeEntry
        {
            TimeProjectId = project.Id, Date = DateOnly.FromDateTime(now),
            StartTime = new TimeOnly(1, 0), EndTime = new TimeOnly(2, 0), Description = "Neu"
        });
        await service.CancelTimerAsync();

        await DemoSeed.SeedAsync(db, now);

        Assert.Empty(await service.GetEntriesAsync(new TimeEntryFilter { Search = "Neu" }));
        Assert.NotNull(await service.GetActiveSessionAsync());
    }
}
