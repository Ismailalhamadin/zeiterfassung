using System.Globalization;

namespace Zeiterfassung.Web.Services;

/// <summary>
/// Koordinaten für das Verlaufsdiagramm (SVG). Zahlen immer mit Punkt: SVG kennt kein Dezimalkomma,
/// auch wenn die Oberfläche auf Deutsch läuft.
/// </summary>
public static class ChartGeometry
{
    public const double W = 1000;
    public const double H = 200;
    public const double Pad = 10;

    /// <summary>Punkte der Linie: „x,y x,y …“.</summary>
    public static string LinePoints(IReadOnlyList<PeriodChartItem> items)
    {
        if (items.Count == 0) return "";
        var max = Max(items);
        return string.Join(' ', items.Select((it, i) => Num(X(i, items.Count)) + "," + Num(Y(it.Minutes, max))));
    }

    /// <summary>Gefüllte Fläche unter der Linie als Pfad.</summary>
    public static string AreaPath(IReadOnlyList<PeriodChartItem> items)
    {
        if (items.Count == 0) return "";
        var max = Max(items);
        var pts = items.Select((it, i) => "L " + Num(X(i, items.Count)) + " " + Num(Y(it.Minutes, max)));
        return "M " + Num(X(0, items.Count)) + " " + Num(H) + " " + string.Join(' ', pts)
               + " L " + Num(X(items.Count - 1, items.Count)) + " " + Num(H) + " Z";
    }

    private static double Max(IReadOnlyList<PeriodChartItem> items) => Math.Max(1, items.Max(i => i.Minutes));

    private static double X(int i, int n) => n <= 1 ? W / 2 : i * (W / (n - 1));

    private static double Y(int minutes, double max) => Pad + (H - 2 * Pad) * (1 - minutes / max);

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
