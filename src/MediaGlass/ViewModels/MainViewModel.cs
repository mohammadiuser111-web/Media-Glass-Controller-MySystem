using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MediaGlass.Infrastructure;
using MediaGlass.Models;
using MediaGlass.Services;

namespace MediaGlass.ViewModels;

internal sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly MediaSessionService _mediaService;
    private readonly DispatcherTimer _positionTimer;
    private byte[]? _thumbnailBytes;
    private string _title = AppStrings.WaitingTitle;
    private string _artist = AppStrings.WaitingSubtitle;
    private string _source = AppStrings.AlwaysOnTop;
    private ImageSource? _artwork;
    private Color _themeStart = Color.FromArgb(220, 20, 27, 42);
    private Color _themeEnd = Color.FromArgb(232, 8, 11, 19);
    private Color _accent = Color.FromRgb(96, 165, 250);
    private double _positionSeconds;
    private double _durationSeconds = 1;
    private string _positionText = "0:00";
    private string _durationText = "0:00";
    private string _playPauseGlyph = "\uE768";
    private bool _isPlaying;
    private bool _hasMedia;
    private bool _canSeek;
    private bool _canStop;
    private bool _canGoPrevious;
    private bool _canGoNext;
    private bool _isSeeking;
    private DateTimeOffset _lastPositionUpdate = DateTimeOffset.UtcNow;
    private bool _disposed;

    public MainViewModel(MediaSessionService mediaService)
    {
        _mediaService = mediaService;
        _mediaService.SnapshotChanged += OnSnapshotChanged;
        TogglePlayPauseCommand = new AsyncCommand(
            _mediaService.TogglePlayPauseAsync,
            () => HasMedia);
        StopCommand = new AsyncCommand(
            _mediaService.StopAsync,
            () => CanStop);
        PreviousCommand = new AsyncCommand(
            _mediaService.PreviousAsync,
            () => CanGoPrevious);
        NextCommand = new AsyncCommand(
            _mediaService.NextAsync,
            () => CanGoNext);

        _positionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(400),
        };
        _positionTimer.Tick += OnPositionTimerTick;
        _positionTimer.Start();
    }

    public AsyncCommand TogglePlayPauseCommand { get; }

    public AsyncCommand StopCommand { get; }

    public AsyncCommand PreviousCommand { get; }

    public AsyncCommand NextCommand { get; }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Artist
    {
        get => _artist;
        private set => SetProperty(ref _artist, value);
    }

    public string Source
    {
        get => _source;
        private set => SetProperty(ref _source, value);
    }

    public ImageSource? Artwork
    {
        get => _artwork;
        private set => SetProperty(ref _artwork, value);
    }

    public Color ThemeStart
    {
        get => _themeStart;
        private set => SetProperty(ref _themeStart, value);
    }

    public Color ThemeEnd
    {
        get => _themeEnd;
        private set => SetProperty(ref _themeEnd, value);
    }

    public Color Accent
    {
        get => _accent;
        private set => SetProperty(ref _accent, value);
    }

    public double PositionSeconds
    {
        get => _positionSeconds;
        private set
        {
            if (SetProperty(ref _positionSeconds, Math.Clamp(value, 0, DurationSeconds)))
            {
                PositionText = FormatTime(TimeSpan.FromSeconds(_positionSeconds));
            }
        }
    }

    public double DurationSeconds
    {
        get => _durationSeconds;
        private set => SetProperty(ref _durationSeconds, Math.Max(1, value));
    }

    public string PositionText
    {
        get => _positionText;
        private set => SetProperty(ref _positionText, value);
    }

    public string DurationText
    {
        get => _durationText;
        private set => SetProperty(ref _durationText, value);
    }

    public string PlayPauseGlyph
    {
        get => _playPauseGlyph;
        private set => SetProperty(ref _playPauseGlyph, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    public bool HasMedia
    {
        get => _hasMedia;
        private set => SetProperty(ref _hasMedia, value);
    }

    public bool CanSeek
    {
        get => _canSeek;
        private set => SetProperty(ref _canSeek, value);
    }

    public bool CanStop
    {
        get => _canStop;
        private set => SetProperty(ref _canStop, value);
    }

    public bool CanGoPrevious
    {
        get => _canGoPrevious;
        private set => SetProperty(ref _canGoPrevious, value);
    }

    public bool CanGoNext
    {
        get => _canGoNext;
        private set => SetProperty(ref _canGoNext, value);
    }

    public Task InitializeAsync() => _mediaService.InitializeAsync();

    public void BeginSeek() => _isSeeking = true;

    public async Task CompleteSeekAsync(double seconds)
    {
        PositionSeconds = seconds;
        _lastPositionUpdate = DateTimeOffset.UtcNow;
        try
        {
            await _mediaService.SeekAsync(TimeSpan.FromSeconds(seconds));
        }
        finally
        {
            _isSeeking = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _positionTimer.Stop();
        _positionTimer.Tick -= OnPositionTimerTick;
        _mediaService.SnapshotChanged -= OnSnapshotChanged;
        _mediaService.Dispose();
    }

    private void OnSnapshotChanged(object? sender, MediaSnapshot snapshot)
    {
        _ = sender;
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ApplySnapshot(snapshot));
    }

    private void ApplySnapshot(MediaSnapshot snapshot)
    {
        HasMedia = snapshot.HasMedia;
        Title = snapshot.Title;
        Artist = snapshot.Artist;
        Source = snapshot.Source;
        IsPlaying = snapshot.IsPlaying;
        PlayPauseGlyph = snapshot.IsPlaying ? "\uE769" : "\uE768";
        CanSeek = snapshot.CanSeek;
        CanStop = snapshot.CanStop;
        CanGoPrevious = snapshot.CanGoPrevious;
        CanGoNext = snapshot.CanGoNext;

        TimeSpan duration = snapshot.EndTime - snapshot.StartTime;
        TimeSpan relativePosition = snapshot.Position - snapshot.StartTime;
        DurationSeconds = Math.Max(1, duration.TotalSeconds);
        DurationText = FormatTime(duration < TimeSpan.Zero ? TimeSpan.Zero : duration);
        if (!_isSeeking)
        {
            PositionSeconds = Math.Clamp(relativePosition.TotalSeconds, 0, DurationSeconds);
        }

        _lastPositionUpdate = DateTimeOffset.UtcNow;
        if (!ReferenceEquals(_thumbnailBytes, snapshot.Thumbnail))
        {
            _thumbnailBytes = snapshot.Thumbnail;
            ThumbnailTheme theme = ThumbnailThemeService.Create(snapshot.Thumbnail);
            Artwork = theme.Artwork;
            ThemeStart = theme.Start;
            ThemeEnd = theme.End;
            Accent = theme.Accent;
        }

        RaiseCommandStates();
    }

    private void OnPositionTimerTick(object? sender, EventArgs eventArgs)
    {
        if (!IsPlaying || _isSeeking || !HasMedia)
        {
            _lastPositionUpdate = DateTimeOffset.UtcNow;
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        double elapsed = (now - _lastPositionUpdate).TotalSeconds;
        _lastPositionUpdate = now;
        PositionSeconds = Math.Min(DurationSeconds, PositionSeconds + elapsed);
    }

    private void RaiseCommandStates()
    {
        TogglePlayPauseCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        PreviousCommand.RaiseCanExecuteChanged();
        NextCommand.RaiseCanExecuteChanged();
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }
}
