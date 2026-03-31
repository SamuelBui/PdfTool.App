using System.Windows;
using PdfTool.App.Models;
using PdfTool.App.Services;

namespace PdfTool.App.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly IAppSessionService _sessionService;
    private int _selectedTabIndex;

    public MainViewModel(
        ProtectViewModel protect,
        SplitViewModel split,
        MergeViewModel merge,
        CompressViewModel compress,
        IAppStatusService status,
        IAppSessionService sessionService)
    {
        Protect = protect;
        Split = split;
        Merge = merge;
        Compress = compress;
        Status = status;
        _sessionService = sessionService;
    }

    public ProtectViewModel Protect { get; }
    public SplitViewModel Split { get; }
    public MergeViewModel Merge { get; }
    public CompressViewModel Compress { get; }
    public IAppStatusService Status { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public void TryAutoRestoreSession(Window window)
    {
        var session = _sessionService.TryLoadAutoSession();
        if (session == null)
        {
            return;
        }

        RestoreSessionState(window, session);
        try
        {
            // Immediately rewrite the auto-session without secrets so older session files are scrubbed on first run.
            _sessionService.SaveAutoSession(CaptureSessionState(window));
        }
        catch
        {
            // Session scrubbing should never block startup.
        }
        Status.Complete("Last session restored.");
    }

    public void AutoSaveSession(Window window)
    {
        try
        {
            _sessionService.SaveAutoSession(CaptureSessionState(window));
        }
        catch
        {
            // Auto-save should never block shutdown.
        }
    }

    private AppSessionData CaptureSessionState(Window window)
    {
        var bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;

        return new AppSessionData
        {
            SelectedTabIndex = SelectedTabIndex,
            Window = new WindowSessionState
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                WindowState = window.WindowState
            },
            Protect = Protect.CaptureSessionState(),
            Split = Split.CaptureSessionState(),
            Merge = Merge.CaptureSessionState(),
            Compress = Compress.CaptureSessionState()
        };
    }

    private void RestoreSessionState(Window window, AppSessionData session)
    {
        ApplyWindowState(window, session.Window);
        Protect.RestoreSessionState(session.Protect);
        Split.RestoreSessionState(session.Split);
        Merge.RestoreSessionState(session.Merge);
        Compress.RestoreSessionState(session.Compress);
        SelectedTabIndex = Math.Clamp(session.SelectedTabIndex, 0, 4);
    }

    private static void ApplyWindowState(Window window, WindowSessionState state)
    {
        if (state.Width > 0)
        {
            window.Width = state.Width;
        }

        if (state.Height > 0)
        {
            window.Height = state.Height;
        }

        window.Left = state.Left;
        window.Top = state.Top;
        window.WindowState = state.WindowState;
    }
}
