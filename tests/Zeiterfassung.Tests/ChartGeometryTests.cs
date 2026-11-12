using System.Globalization;
using Zeiterfassung.Web.Services;

namespace Zeiterfassung.Tests;

/// <summary>Das Verlaufsdiagramm muss auch mit deutscher Kultur gültige SVG-Zahlen liefern.</summary>
public class ChartGeometryTests
{
    private static List<PeriodChartItem> Items(params int[] minutes) =>
        minutes.Select((m, i) => new PeriodChartItem("T" + i, m, new DateOnly(2026, 11, 1).AddDays(i))).ToList();

    [Fact]
    public void Punkte_verwenden_Punkt_statt_Dezimalkomma_in_deutscher_Kultur()
    {
        var old = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var line = ChartGeometry.LinePoints(Items(30, 90, 0));
            var area = ChartGeometry.AreaPath(Items(30, 90, 0));

            // Jeder Punkt ist "x,y" mit genau einem Komma und Zahlen mit Punkt.
            Assert.All(line.Split(' '), p => Assert.Matches(@"^\d+(\.\d+)?,\d+(\.\d+)?$", p));
            Assert.Matches(@"^M \d+(\.\d+)? \d+(\.\d+)? (L \d+(\.\d+)? \d+(\.\d+)? )+Z$", area);
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void Linie_laeuft_von_links_nach_rechts_und_hoechster_Wert_liegt_oben()
    {
        var pts = ChartGeometry.LinePoints(Items(0, 60, 120)).Split(' ')
            .Select(p => p.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray()).ToList();

        Assert.Equal(0, pts[0][0]);
        Assert.Equal(500, pts[1][0]);
        Assert.Equal(ChartGeometry.W, pts[2][0]);
        Assert.True(pts[2][1] < pts[1][1] && pts[1][1] < pts[0][1], "Größerer Wert = kleineres y (weiter oben)");
        Assert.Equal(ChartGeometry.H - ChartGeometry.Pad, pts[0][1]);
    }

    [Fact]
    public void Leere_Liste_und_einzelner_Punkt()
    {
        Assert.Equal("", ChartGeometry.LinePoints([]));
        Assert.Equal("", ChartGeometry.AreaPath([]));
        Assert.StartsWith("500,", ChartGeometry.LinePoints(Items(10)));
    }
}
