using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MediaGlass.Interop;
using MediaGlass.Models;
using MediaGlass.Services;
using MediaGlass.ViewModels;

namespace MediaGlass;

public partial class MainWindow : Window, IDisposable
{
    private readonly SettingsService _settingsService = new();
    private readonly MainViewModel _viewModel;
    private WidgetSettings _settings;
    private bool _isPositionLocked;
    private bool _isTimelineDragging;
    private bool _disposed;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        _viewModel = new MainViewModel(new MediaSessionService());
        DataContext = _viewModel;
        ApplySettings(_settings);
        ApplyLocalizedTooltips();
    }

    private void OnSourceInitialized(object? sender, EventArgs eventArgs) => WindowBackdrop.Apply(this);

    private async void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        EnsureVisiblePosition();
        await _viewModel.InitializeAsync();
    }

    private void OnClosing(object? sender, CancelEventArgs eventArgs)
    {
        _ = eventArgs;
        _settings = new WidgetSettings
        {
            SchemaVersion = WidgetSettings.CurrentSchemaVersion,
            Left = Left,
            Top = Top,
            Width = Width,
            Height = Height,
            IsPositionLocked = _isPositionLocked,
        };
        _settingsService.Save(_settings);
    }

    private void OnClosed(object? sender, EventArgs eventArgs) => Dispose();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnWindowMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (_isPositionLocked || eventArgs.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (IsInteractiveElement(eventArgs.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The mouse button can be released between the event and DragMove.
        }
    }

    private void OnLockButtonClick(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        SetPositionLocked(!_isPositionLocked);
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        Close();
    }

    private void OnTimelineMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        _isTimelineDragging = true;
        _viewModel.BeginSeek();
    }

    private async void OnTimelineMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (!_isTimelineDragging)
        {
            return;
        }

        _isTimelineDragging = false;
        await _viewModel.CompleteSeekAsync(TimelineSlider.Value);
    }

    private void ApplySettings(WidgetSettings settings)
    {
        bool usesCompactLayout = settings.SchemaVersion >= WidgetSettings.CurrentSchemaVersion;
        double savedWidth = usesCompactLayout ? settings.Width : 330;
        double savedHeight = usesCompactLayout ? settings.Height : 112;
        Width = Math.Clamp(savedWidth, MinWidth, MaxWidth);
        Height = Math.Clamp(savedHeight, MinHeight, MaxHeight);
        WindowStartupLocation = WindowStartupLocation.Manual;
        if (!double.IsNaN(settings.Left) && !double.IsNaN(settings.Top))
        {
            Left = settings.Left;
            Top = settings.Top;
        }
        else
        {
            Rect workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 20;
            Top = workArea.Top + 20;
        }

        SetPositionLocked(settings.IsPositionLocked);
    }

    private void EnsureVisiblePosition()
    {
        Rect virtualScreen = new(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        var current = new Rect(Left, Top, Width, Height);
        if (virtualScreen.IntersectsWith(current))
        {
            return;
        }

        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Top + 20;
    }

    private void SetPositionLocked(bool locked)
    {
        _isPositionLocked = locked;
        ResizeMode = locked ? ResizeMode.NoResize : ResizeMode.CanResize;
        LockButton.Content = locked ? "\uE785" : "\uE72E";
        LockButton.ToolTip = locked ? AppStrings.UnlockPosition : AppStrings.LockPosition;
    }

    private void ApplyLocalizedTooltips()
    {
        CloseButton.ToolTip = AppStrings.Close;
    }

    private static bool IsInteractiveElement(DependencyObject? element)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is ButtonBase or Slider)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
