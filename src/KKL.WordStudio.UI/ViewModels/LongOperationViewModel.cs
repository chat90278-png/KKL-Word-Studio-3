namespace KKL.WordStudio.UI.ViewModels;

using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Session-only shell state for genuinely long UI operations. Each caller owns
/// an independent cancellation source; the shell displays the newest active
/// operation while the full-screen overlay blocks accidental re-entry.
/// </summary>
public sealed partial class LongOperationViewModel : ViewModelBase
{
    public static LongOperationViewModel Shared { get; } = new();

    private readonly object _sync = new();
    private readonly Dictionary<long, ActiveOperation> _active = new();
    private long _nextId;

    private LongOperationViewModel()
    {
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _detail = string.Empty;

    [ObservableProperty]
    private bool _isCancellable;

    public LongOperationLease Begin(string title, string detail, bool isCancellable = true)
    {
        var cancellation = new CancellationTokenSource();
        long id;

        lock (_sync)
        {
            id = ++_nextId;
            _active.Add(id, new ActiveOperation(id, title, detail, isCancellable, cancellation));
            PublishNewestLocked();
        }

        // Setting IsBusy is not enough when the next operation performs
        // synchronous setup on the UI thread. Process WPF data binding and one
        // render turn now, after the shield became active, before returning to
        // the caller. Stop at Loaded priority: DataBind/Render have run, but a
        // new Input-priority click cannot re-enter the application here.
        FlushPresentationIfAvailable();

        return new LongOperationLease(this, id, cancellation.Token);
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        CancellationTokenSource[] sources;
        lock (_sync)
        {
            sources = _active.Values
                .Where(operation => operation.IsCancellable && !operation.Cancellation.IsCancellationRequested)
                .Select(operation => operation.Cancellation)
                .ToArray();

            foreach (var operation in _active.Values)
                operation.IsCancellable = false;

            Detail = "İptal isteği alındı · güvenli durma noktası bekleniyor…";
            IsCancellable = false;
            CancelCommand.NotifyCanExecuteChanged();
        }

        foreach (var source in sources)
            source.Cancel();
    }

    private bool CanCancel() => IsBusy && IsCancellable;

    internal void ReportDetail(long id, string detail)
    {
        lock (_sync)
        {
            if (!_active.TryGetValue(id, out var operation))
                return;

            operation.Detail = detail;
            if (id == _active.Keys.Max())
                Detail = detail;
        }
    }

    internal void End(long id)
    {
        CancellationTokenSource? cancellation = null;
        lock (_sync)
        {
            if (_active.Remove(id, out var operation))
                cancellation = operation.Cancellation;

            PublishNewestLocked();
        }

        cancellation?.Dispose();
    }

    private void PublishNewestLocked()
    {
        if (_active.Count == 0)
        {
            IsBusy = false;
            Title = string.Empty;
            Detail = string.Empty;
            IsCancellable = false;
            CancelCommand.NotifyCanExecuteChanged();
            return;
        }

        var newest = _active.Values.MaxBy(operation => operation.Id)!;
        IsBusy = true;
        Title = newest.Title;
        Detail = newest.Detail;
        IsCancellable = _active.Values.Any(operation =>
            operation.IsCancellable && !operation.Cancellation.IsCancellationRequested);
        CancelCommand.NotifyCanExecuteChanged();
    }

    private static void FlushPresentationIfAvailable()
    {
        var application = Application.Current;
        var dispatcher = application?.Dispatcher;
        if (dispatcher is null
            || !dispatcher.CheckAccess()
            || application?.MainWindow is not { IsVisible: true })
            return;

        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(
            new Action(() => frame.Continue = false),
            DispatcherPriority.Loaded);
        Dispatcher.PushFrame(frame);
    }

    private sealed class ActiveOperation(
        long id,
        string title,
        string detail,
        bool isCancellable,
        CancellationTokenSource cancellation)
    {
        public long Id { get; } = id;
        public string Title { get; } = title;
        public string Detail { get; set; } = detail;
        public bool IsCancellable { get; set; } = isCancellable;
        public CancellationTokenSource Cancellation { get; } = cancellation;
    }
}

public sealed class LongOperationLease : IDisposable
{
    private LongOperationViewModel? _owner;
    private readonly long _id;

    internal LongOperationLease(LongOperationViewModel owner, long id, CancellationToken token)
    {
        _owner = owner;
        _id = id;
        Token = token;
    }

    public CancellationToken Token { get; }

    public void ReportDetail(string detail) => _owner?.ReportDetail(_id, detail);

    public void Dispose() => Interlocked.Exchange(ref _owner, null)?.End(_id);
}
