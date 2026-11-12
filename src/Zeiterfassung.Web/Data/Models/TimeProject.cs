using System.ComponentModel.DataAnnotations;

namespace Zeiterfassung.Web.Data.Models;

/// <summary>
/// Ein Projekt der Zeiterfassung. Bewusst getrennt vom Portfolio-<see cref="Project"/>,
/// damit sich beide Bereiche unabhängig entwickeln können.
/// </summary>
public class TimeProject
{
    public int Id { get; set; }

    /// <summary>Anzeigename des Projekts.</summary>
    [Required(ErrorMessage = "Bitte einen Projektnamen angeben.")]
    [StringLength(200, ErrorMessage = "Der Name darf maximal 200 Zeichen lang sein.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optionale Beschreibung.</summary>
    [StringLength(1000, ErrorMessage = "Die Beschreibung darf maximal 1000 Zeichen lang sein.")]
    public string? Description { get; set; }

    /// <summary>Akzentfarbe als Hex-Wert (z. B. <c>#732F3B</c>).</summary>
    [Required]
    [StringLength(20)]
    [RegularExpression("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", ErrorMessage = "Bitte eine gültige Hex-Farbe angeben (z. B. #732F3B).")]
    public string Color { get; set; } = "#732F3B";

    /// <summary>Optionale Icon-Kennung (Bootstrap-Icons-Klasse, z. B. <c>bi-kanban</c>).</summary>
    [StringLength(50)]
    public string? Icon { get; set; }

    /// <summary>Aktiv oder abgeschlossen.</summary>
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    /// <summary>Archivierte Projekte werden standardmäßig ausgeblendet.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Erstellungszeitpunkt (lokale Zeit des Servers).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Alle Zeiteinträge dieses Projekts.</summary>
    public ICollection<TimeEntry> Entries { get; set; } = new List<TimeEntry>();
}
