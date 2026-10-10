namespace MediaGlass.Models;

internal sealed record MediaSnapshot(
    bool HasMedia,
    string Title,
    string Artist,
    string Source,
    byte[]? Thumbnail,
    TimeSpan Position,
    TimeSpan StartTime,
    TimeSpan EndTime,
    bool IsPlaying,
    bool CanPlay,
    bool CanPause,
    bool CanSeek,
    bool CanGoPrevious,
    bool CanGoNext)
{
    public static MediaSnapshot Empty { get; } = new(
        HasMedia: false,
        Title: AppStrings.WaitingTitle,
        Artist: AppStrings.WaitingSubtitle,
        Source: AppStrings.AlwaysOnTop,
        Thumbnail: null,
        Position: TimeSpan.Zero,
        StartTime: TimeSpan.Zero,
        EndTime: TimeSpan.Zero,
        IsPlaying: false,
        CanPlay: false,
        CanPause: false,
        CanSeek: false,
        CanGoPrevious: false,
        CanGoNext: false);
}
