namespace EgyptTax.Web.Shared.Toasts;

/// <summary>
/// v5 DP.1 — singleton-per-circuit (Scoped in DI) toast notification
/// service. Pages inject + call <see cref="ShowSuccess"/> /
/// <see cref="ShowError"/> / <see cref="ShowWarn"/> / <see cref="ShowInfo"/>;
/// the <see cref="ToastContainer"/> in <c>MainLayout</c> subscribes
/// to <see cref="OnChange"/> and renders the active toast stack
/// fixed bottom-end of the viewport. Each toast auto-dismisses
/// after <see cref="Toast.AutoDismissMilliseconds"/>.
///
/// Replaces the scattered inline `flash-success` / `flash-error`
/// divs across 100+ pages with a single feedback channel.
/// </summary>
public sealed class ToastService
{
    private readonly List<Toast> _active = new();

    public IReadOnlyList<Toast> Active => _active;
    public event Action? OnChange;

    public void ShowSuccess(string message, string? title = null) =>
        Add(new Toast(ToastKind.Success, message, title));

    public void ShowError(string message, string? title = null) =>
        Add(new Toast(ToastKind.Error, message, title));

    public void ShowWarn(string message, string? title = null) =>
        Add(new Toast(ToastKind.Warn, message, title));

    public void ShowInfo(string message, string? title = null) =>
        Add(new Toast(ToastKind.Info, message, title));

    public void Dismiss(Guid id)
    {
        var idx = _active.FindIndex(t => t.Id == id);
        if (idx >= 0)
        {
            _active.RemoveAt(idx);
            OnChange?.Invoke();
        }
    }

    private void Add(Toast toast)
    {
        // Cap at 5 visible at once; oldest drops off so the operator
        // never gets a wall-of-toasts that obscures the page.
        _active.Add(toast);
        while (_active.Count > 5)
        {
            _active.RemoveAt(0);
        }
        OnChange?.Invoke();
    }
}

public sealed record Toast(ToastKind Kind, string Message, string? Title = null)
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;
    public const int AutoDismissMilliseconds = 5000;
}

public enum ToastKind
{
    Success,
    Error,
    Warn,
    Info,
}
