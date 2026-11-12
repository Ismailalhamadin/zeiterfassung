namespace Zeiterfassung.Web.Services;

public enum ToastLevel { Info, Success, Warning, Error }

public record ToastMessage(Guid Id, string Text, ToastLevel Level);

/// <summary>
/// Schlanker Benachrichtigungsdienst (Toasts) .
/// Pro Browser-Tab als Scoped registriert; Komponenten abonnieren <see cref="OnChange"/>.
/// </summary>
public class ToastService : IDisposable
{
    private readonly List<ToastMessage> _messages = new();
    private readonly List<Timer> _timers = new();
    private readonly object _gate = new();

    public event Action? OnChange;

    public IReadOnlyList<ToastMessage> Messages
    {
        get { lock (_gate) return _messages.ToList(); }
    }

    public void Info(string text) => Show(text, ToastLevel.Info);
    public void Success(string text) => Show(text, ToastLevel.Success);
    public void Warning(string text) => Show(text, ToastLevel.Warning);
    public void Error(string text) => Show(text, ToastLevel.Error);

    public void Show(string text, ToastLevel level = ToastLevel.Info)
    {
        var toast = new ToastMessage(Guid.NewGuid(), text, level);
        lock (_gate)
        {
            _messages.Add(toast);
            // Auto-Ausblenden nach 4 Sekunden.
            _timers.Add(new Timer(_ => Remove(toast.Id), null, 4000, Timeout.Infinite));
        }
        OnChange?.Invoke();
    }

    public void Remove(Guid id)
    {
        lock (_gate)
        {
            _messages.RemoveAll(m => m.Id == id);
        }
        OnChange?.Invoke();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var t in _timers) t.Dispose();
            _timers.Clear();
        }
    }
}
