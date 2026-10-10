using System.Diagnostics.CodeAnalysis;
using System.IO;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace MediaGlass.Services;

internal sealed class MediaSessionService : IDisposable
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private MediaMetadata _metadata = MediaMetadata.Empty;
    private TimeSpan _timelineStart;
    private bool _disposed;

    public event EventHandler<Models.MediaSnapshot>? SnapshotChanged;

    public async Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += OnCurrentSessionChanged;
        _manager.SessionsChanged += OnSessionsChanged;
        await SelectSessionAsync().ConfigureAwait(false);
    }

    public async Task TogglePlayPauseAsync()
    {
        GlobalSystemMediaTransportControlsSession? session = _session;
        if (session is null)
        {
            return;
        }

        GlobalSystemMediaTransportControlsSessionPlaybackStatus status =
            session.GetPlaybackInfo()?.PlaybackStatus ??
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed;

        if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
        {
            _ = await session.TryPauseAsync();
        }
        else
        {
            _ = await session.TryPlayAsync();
        }
    }

    public async Task PreviousAsync()
    {
        if (_session is not null)
        {
            _ = await _session.TrySkipPreviousAsync();
        }
    }

    public async Task NextAsync()
    {
        if (_session is not null)
        {
            _ = await _session.TrySkipNextAsync();
        }
    }

    public async Task SeekAsync(TimeSpan relativePosition)
    {
        if (_session is null)
        {
            return;
        }

        long absoluteTicks = (_timelineStart + relativePosition).Ticks;
        _ = await _session.TryChangePlaybackPositionAsync(Math.Max(0, absoluteTicks));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DetachSession();

        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _manager.SessionsChanged -= OnSessionsChanged;
            _manager = null;
        }

        _refreshGate.Dispose();
    }

    private async void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs arguments)
    {
        _ = sender;
        _ = arguments;
        await SelectSessionAsync().ConfigureAwait(false);
    }

    private async void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs arguments)
    {
        _ = sender;
        _ = arguments;
        await SelectSessionAsync().ConfigureAwait(false);
    }

    private async void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs arguments)
    {
        _ = sender;
        _ = arguments;
        await RefreshAsync(refreshMetadata: true).ConfigureAwait(false);
    }

    private async void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs arguments)
    {
        _ = sender;
        _ = arguments;
        await RefreshAsync(refreshMetadata: false).ConfigureAwait(false);
    }

    private async void OnTimelinePropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs arguments)
    {
        _ = sender;
        _ = arguments;
        await RefreshAsync(refreshMetadata: false).ConfigureAwait(false);
    }

    private async Task SelectSessionAsync()
    {
        GlobalSystemMediaTransportControlsSession? selected = SelectBestSession();
        if (!ReferenceEquals(selected, _session))
        {
            DetachSession();
            _session = selected;
            _metadata = MediaMetadata.Empty;
            AttachSession();
        }

        await RefreshAsync(refreshMetadata: true).ConfigureAwait(false);
    }

    private GlobalSystemMediaTransportControlsSession? SelectBestSession()
    {
        if (_manager is null)
        {
            return null;
        }

        GlobalSystemMediaTransportControlsSession? current = _manager.GetCurrentSession();
        if (IsPlaying(current))
        {
            return current;
        }

        IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions = _manager.GetSessions();
        GlobalSystemMediaTransportControlsSession? playing = null;
        foreach (GlobalSystemMediaTransportControlsSession session in sessions)
        {
            if (IsPlaying(session))
            {
                playing = session;
                break;
            }
        }

        return playing ?? current ?? (sessions.Count > 0 ? sessions[0] : null);
    }

    private static bool IsPlaying(GlobalSystemMediaTransportControlsSession? session) =>
        session?.GetPlaybackInfo()?.PlaybackStatus ==
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

    private void AttachSession()
    {
        if (_session is null)
        {
            return;
        }

        _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        _session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
    }

    private void DetachSession()
    {
        if (_session is null)
        {
            return;
        }

        _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        _session = null;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A media session can disappear between any two WinRT calls; the widget must recover without exiting.")]
    private async Task RefreshAsync(bool refreshMetadata)
    {
        await _refreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            GlobalSystemMediaTransportControlsSession? session = _session;
            if (session is null)
            {
                Publish(Models.MediaSnapshot.Empty);
                return;
            }

            if (refreshMetadata || _metadata == MediaMetadata.Empty)
            {
                GlobalSystemMediaTransportControlsSessionMediaProperties properties =
                    await session.TryGetMediaPropertiesAsync();
                byte[]? thumbnail = await ReadThumbnailAsync(properties.Thumbnail).ConfigureAwait(false);
                _metadata = new MediaMetadata(
                    string.IsNullOrWhiteSpace(properties.Title) ? AppStrings.WaitingTitle : properties.Title,
                    string.IsNullOrWhiteSpace(properties.Artist) ? AppStrings.UnknownArtist : properties.Artist,
                    thumbnail);
            }

            GlobalSystemMediaTransportControlsSessionPlaybackInfo? playback = session.GetPlaybackInfo();
            GlobalSystemMediaTransportControlsSessionTimelineProperties timeline = session.GetTimelineProperties();
            GlobalSystemMediaTransportControlsSessionPlaybackControls? controls = playback?.Controls;
            _timelineStart = timeline.StartTime;

            var snapshot = new Models.MediaSnapshot(
                HasMedia: true,
                Title: _metadata.Title,
                Artist: _metadata.Artist,
                Source: GetFriendlySourceName(session.SourceAppUserModelId),
                Thumbnail: _metadata.Thumbnail,
                Position: timeline.Position,
                StartTime: timeline.StartTime,
                EndTime: timeline.EndTime,
                IsPlaying: playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                CanPlay: controls?.IsPlayEnabled == true,
                CanPause: controls?.IsPauseEnabled == true,
                CanSeek: controls?.IsPlaybackPositionEnabled == true,
                CanGoPrevious: controls?.IsPreviousEnabled == true,
                CanGoNext: controls?.IsNextEnabled == true);
            Publish(snapshot);
        }
        catch (Exception)
        {
            Publish(Models.MediaSnapshot.Empty);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null)
        {
            return null;
        }

        using IRandomAccessStreamWithContentType stream = await reference.OpenReadAsync();
        if (stream.Size == 0 || stream.Size > 12 * 1024 * 1024)
        {
            return null;
        }

        uint length = checked((uint)stream.Size);
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        _ = await reader.LoadAsync(length);
        byte[] bytes = new byte[length];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private static string GetFriendlySourceName(string source)
    {
        string value = source.ToLowerInvariant();
        if (value.Contains("msedge", StringComparison.Ordinal))
        {
            return "YouTube · Microsoft Edge";
        }

        if (value.Contains("chrome", StringComparison.Ordinal))
        {
            return "YouTube · Google Chrome";
        }

        if (value.Contains("firefox", StringComparison.Ordinal))
        {
            return "YouTube · Mozilla Firefox";
        }

        return source.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(source)
            : source;
    }

    private void Publish(Models.MediaSnapshot snapshot) => SnapshotChanged?.Invoke(this, snapshot);

    private sealed record MediaMetadata(string Title, string Artist, byte[]? Thumbnail)
    {
        public static MediaMetadata Empty { get; } = new(string.Empty, string.Empty, null);
    }
}
