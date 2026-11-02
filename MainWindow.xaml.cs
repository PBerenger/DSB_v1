using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Drawing = System.Drawing;
using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace DoubleSceensBlack;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0xD551;
    private const int WmHotkey = 0x0312;
    private const int ModAlt = 0x0001;
    private const int ModControl = 0x0002;
    private const int ModShift = 0x0004;
    private const int ModWindows = 0x0008;
    private const int ModNoRepeat = 0x4000;
    private const int DefaultHotkeyModifiers = ModControl | ModAlt;
    private const int DefaultHotkeyVirtualKey = 0x42;
    private const int WhMouseLowLevel = 14;
    private const int WmMouseMove = 0x0200;
    private static readonly TimeSpan CursorHideDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SettingsSaveDelay = TimeSpan.FromMilliseconds(350);

    private readonly List<ScreenOverlayWindow> _overlays = [];
    private readonly List<ScreenIdentificationWindow> _screenIdentificationWindows = [];
    private List<MonitorChoice> _monitorChoices = [];
    private readonly DispatcherTimer _settingsSaveTimer = new()
    {
        Interval = SettingsSaveDelay
    };
    private readonly DispatcherTimer _mouseInactivityTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(100)
    };
    private AppSettings _settings = new();
    private HwndSource? _source;
    private LowLevelMouseProcedure? _mouseHookProcedure;
    private IntPtr _mouseHookHandle;
    private IntPtr _windowHandle;
    private long _lastMouseMoveTick;
    private int _registeredKey;
    private int _registeredModifiers;
    private bool _hotkeyRegistered;
    private bool _isDimmed;
    private bool _isRestoringIntensity;
    private bool _cursorHidden;
    private bool _cursorHideUnavailable;
    private bool _isInitializing = true;
    private bool _allowClose;
    private bool _isMovesPanelVisible;
    private bool _hasHandledInitialLoad;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _notifyContextMenu;
    private DrawingIcon? _trayIconImage;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        _settingsSaveTimer.Tick += SettingsSaveTimer_Tick;

        Rect workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, Math.Max(MinWidth, workArea.Width - 24));
        Height = Math.Min(Height, Math.Max(MinHeight, workArea.Height - 48));
        MinWidth = Math.Min(MinWidth, Width);
        MinHeight = Math.Min(MinHeight, Height);

        try
        {
            _settings = SettingsStore.Load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _settings = new AppSettings();
            ShowInformationDialog(
                "Paramètres illisibles",
                $"Les paramètres n'ont pas pu être lus. Les valeurs par défaut seront utilisées.\n\n{exception.Message}",
                _settings.IsBlueTheme);
        }

        NormalizeSettings();
        StatusText.Text = GetReadyStatusText();
        ApplyTheme();
        SharedIntensitySlider.Value = _settings.BlackIntensity;
        SharedIntensityValueText.Text = $"{_settings.BlackIntensity} %";
        AllScreensToggle.IsChecked = _settings.UseSharedIntensity;
        SharedIntensityControls.IsEnabled = _settings.UseSharedIntensity;
        UpdateSharedIntensityTextColors();
        RefreshMonitors();
        _isInitializing = false;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _windowHandle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source.AddHook(WindowProcedure);
        _mouseInactivityTimer.Tick += MouseInactivityTimer_Tick;
        if (!TryRegisterHotkey(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey, out string errorMessage))
        {
            StatusText.Text = errorMessage;
        }

        InitializeNotifyIcon();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasHandledInitialLoad)
        {
            return;
        }

        _hasHandledInitialLoad = true;
        try
        {
            StartupRegistration.UpgradeLegacyRegistration();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            StatusText.Text = $"Le lancement automatique n'a pas pu être actualisé : {exception.Message}";
        }

        if (StartupRegistration.IsStartupLaunch() && _settings.MinimizeOnClose)
        {
            MinimizeToTray();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_settingsSaveTimer.IsEnabled)
        {
            _settingsSaveTimer.Stop();
            SaveSettings();
        }

        if (_hotkeyRegistered)
        {
            UnregisterHotKey(_windowHandle, HotkeyId);
        }

        _source?.RemoveHook(WindowProcedure);
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        _notifyContextMenu?.Dispose();
        _trayIconImage?.Dispose();
        RemoveOverlays();
        base.OnClosed(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel && !_allowClose && _settings.MinimizeOnClose)
        {
            e.Cancel = true;
            if (!MinimizeToTray())
            {
                e.Cancel = false;
                ShowInformationDialog(
                    "Réduction indisponible",
                    "L'icône de notification n'est pas disponible. L'application va se fermer.",
                    _settings.IsBlueTheme);
            }
        }

    }

    private bool MinimizeToTray()
    {
        if (_notifyIcon is null)
        {
            return false;
        }

        _notifyIcon.Visible = true;
        Hide();
        StatusText.Text = "L'application est réduite dans la zone de notification.";
        return true;
    }

    private void InitializeNotifyIcon()
    {
        try
        {
            _trayIconImage = CreateTrayIcon();
            _notifyContextMenu = new Forms.ContextMenuStrip();
            _notifyContextMenu.Items.Add("Ouvrir Double Sceens Black", null, (_, _) => RestoreFromTray());
            _notifyContextMenu.Items.Add(new Forms.ToolStripSeparator());
            _notifyContextMenu.Items.Add("Quitter", null, (_, _) =>
            {
                _allowClose = true;
                Close();
            });
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = _trayIconImage,
                Text = "Double Sceens Black",
                ContextMenuStrip = _notifyContextMenu,
                Visible = false
            };
            _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ExternalException or ArgumentException)
        {
            _notifyIcon?.Dispose();
            _notifyIcon = null;
            _notifyContextMenu?.Dispose();
            _notifyContextMenu = null;
            _trayIconImage?.Dispose();
            _trayIconImage = null;
            StatusText.Text = $"L'icône de notification n'a pas pu être créée : {exception.Message}";
        }
    }

    private static DrawingIcon CreateTrayIcon()
    {
        Uri iconUri = new("pack://application:,,,/DoubleSceensBlack;component/Ressources/Logos/Logo_DSB.png");
        System.Windows.Resources.StreamResourceInfo resource = System.Windows.Application.GetResourceStream(iconUri)
            ?? throw new IOException("Le fichier logo_DSB.png est introuvable.");

        using Stream stream = resource.Stream;
        using Drawing.Bitmap source = new(stream);
        using Drawing.Bitmap bitmap = new(32, 32);
        using (Drawing.Graphics graphics = Drawing.Graphics.FromImage(bitmap))
        {
            graphics.Clear(Drawing.Color.Transparent);
            graphics.DrawImage(source, 0, 0, 32, 32);
        }

        IntPtr iconHandle = bitmap.GetHicon();
        try
        {
            return (DrawingIcon)DrawingIcon.FromHandle(iconHandle).Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private void RestoreFromTray()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
        }

        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        bool startWithWindows;
        try
        {
            startWithWindows = StartupRegistration.IsEnabled();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            StatusText.Text = $"L'état du démarrage Windows n'a pas pu être lu : {exception.Message}";
            return;
        }

        OptionsWindow options = new(
            _settings.IsBlueTheme,
            startWithWindows,
            _settings.MinimizeOnClose,
            _settings.HotkeyModifiers,
            _settings.HotkeyVirtualKey,
            DefaultHotkeyModifiers,
            DefaultHotkeyVirtualKey,
            RegisterHotkeyFromOptions)
        {
            Owner = this
        };
        Opacity = 0.55;
        bool? result;
        try
        {
            result = options.ShowDialog();
        }
        finally
        {
            Opacity = 1;
        }

        if (result != true)
        {
            RestoreHotkeyAfterOptionsCancel();
            return;
        }

        bool previousStartupSetting = startWithWindows;
        bool previousTheme = _settings.IsBlueTheme;
        bool previousMinimizeSetting = _settings.MinimizeOnClose;
        int previousHotkeyModifiers = _settings.HotkeyModifiers;
        int previousHotkeyVirtualKey = _settings.HotkeyVirtualKey;
        try
        {
            if (options.StartWithWindows != startWithWindows)
            {
                StartupRegistration.SetEnabled(options.StartWithWindows);
            }

            _settings.IsBlueTheme = options.IsBlueTheme;
            _settings.MinimizeOnClose = options.MinimizeOnClose;
            _settings.HotkeyModifiers = options.HotkeyModifiers;
            _settings.HotkeyVirtualKey = options.HotkeyVirtualKey;
            if (!SaveSettings())
            {
                _settings.IsBlueTheme = previousTheme;
                _settings.MinimizeOnClose = previousMinimizeSetting;
                _settings.HotkeyModifiers = previousHotkeyModifiers;
                _settings.HotkeyVirtualKey = previousHotkeyVirtualKey;
                RestoreHotkey(previousHotkeyModifiers, previousHotkeyVirtualKey);
                ApplyTheme();
                if (options.StartWithWindows != previousStartupSetting)
                {
                    StartupRegistration.SetEnabled(previousStartupSetting);
                }

                return;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
        {
            _settings.IsBlueTheme = previousTheme;
            _settings.MinimizeOnClose = previousMinimizeSetting;
            _settings.HotkeyModifiers = previousHotkeyModifiers;
            _settings.HotkeyVirtualKey = previousHotkeyVirtualKey;
            RestoreHotkey(previousHotkeyModifiers, previousHotkeyVirtualKey);
            ApplyTheme();
            StatusText.Text = $"Les options n'ont pas pu être enregistrées : {exception.Message}";
            return;
        }

        ApplyTheme();
        StatusText.Text = $"Options enregistrées. {GetReadyStatusText()}";
    }

    private void RefreshMonitors_Click(object sender, RoutedEventArgs e) => RefreshMonitors();

    private void ShowMovesPanel_Click(object sender, RoutedEventArgs e) =>
        SetMovesPanelVisible(true);

    private void ShowScreensPanel_Click(object sender, RoutedEventArgs e) =>
        SetMovesPanelVisible(false);

    private void SetMovesPanelVisible(bool isVisible)
    {
        if (_isMovesPanelVisible == isVisible)
        {
            return;
        }

        _isMovesPanelVisible = isVisible;
        ShowMovesPanelButton.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
        if (isVisible)
        {
            RefreshOpenWindows();
        }

        double screensStart = ScreensPageTransform.X;
        double movesStart = MovesPageTransform.X;
        ScreensPageTransform.BeginAnimation(TranslateTransform.XProperty, null);
        MovesPageTransform.BeginAnimation(TranslateTransform.XProperty, null);
        ScreensPageTransform.X = screensStart;
        MovesPageTransform.X = movesStart;

        TimeSpan duration = TimeSpan.FromMilliseconds(240);
        DoubleAnimation screensSlide = new(
            screensStart,
            isVisible ? -530 : 0,
            duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        DoubleAnimation movesSlide = new(
            movesStart,
            isVisible ? 0 : 580,
            duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        ScreensPageTransform.BeginAnimation(TranslateTransform.XProperty, screensSlide);
        MovesPageTransform.BeginAnimation(TranslateTransform.XProperty, movesSlide);
    }

    private void RefreshOpenWindows_Click(object sender, RoutedEventArgs e) =>
        RefreshOpenWindows();

    private void OpenWindowComboBox_DropDownOpened(object sender, EventArgs e) =>
        RefreshOpenWindows();

    private void RefreshOpenWindows()
    {
        try
        {
            IntPtr selectedHandle = (OpenWindowComboBox.SelectedItem as OpenWindowChoice)?.Handle ?? IntPtr.Zero;
            List<OpenWindowChoice> windows = WindowDiscovery.GetOpenWindows(_windowHandle);
            OpenWindowComboBox.ItemsSource = windows;
            int selectedIndex = windows.FindIndex(window => window.Handle == selectedHandle);
            OpenWindowComboBox.SelectedIndex = selectedIndex >= 0
                ? selectedIndex
                : windows.Count > 0 ? 0 : -1;
            NoOpenWindowsText.Visibility = OpenWindowComboBox.HasItems
                ? Visibility.Collapsed
                : Visibility.Visible;
            UpdateMoveControlsState();
        }
        catch (Win32Exception exception)
        {
            OpenWindowComboBox.ItemsSource = null;
            NoOpenWindowsText.Visibility = Visibility.Visible;
            UpdateMoveControlsState();
            StatusText.Text = $"Les fenêtres ouvertes n'ont pas pu être listées : {exception.Message}";
        }
    }

    private void MoveSelection_Changed(object sender, SelectionChangedEventArgs e) =>
        UpdateMoveControlsState();

    private void UpdateMoveControlsState()
    {
        MoveWindowButton.IsEnabled =
            OpenWindowComboBox.SelectedItem is OpenWindowChoice
            && DestinationMonitorComboBox.SelectedItem is MonitorChoice;
    }

    private void MoveWindow_Click(object sender, RoutedEventArgs e)
    {
        if (OpenWindowComboBox.SelectedItem is not OpenWindowChoice window)
        {
            StatusText.Text = "Sélectionnez une fenêtre ouverte.";
            return;
        }

        if (DestinationMonitorComboBox.SelectedItem is not MonitorChoice monitor)
        {
            StatusText.Text = "Sélectionnez un écran de destination.";
            return;
        }

        bool maximize = MaximizeMoveRadioButton.IsChecked == true;
        if (!WindowMover.TryMove(window.Handle, monitor.Bounds, maximize, out string errorMessage))
        {
            StatusText.Text = errorMessage;
            return;
        }

        StatusText.Text = $"« {window.DisplayName} » déplacée vers {monitor.DisplayName}"
            + (maximize ? " et agrandie." : ".");
    }

    private void IdentifyScreens(MonitorChoice choice)
    {
        foreach (ScreenIdentificationWindow identificationWindow in _screenIdentificationWindows.ToArray())
        {
            identificationWindow.Close();
        }

        MonitorChoice? screen = _monitorChoices
            .FirstOrDefault(candidate => candidate.DeviceName == choice.DeviceName);
        if (screen is null)
        {
            return;
        }

        int screenNumber = _monitorChoices
            .TakeWhile(candidate => candidate.DeviceName != screen.DeviceName)
            .Count() + 1;
        ScreenIdentificationWindow newIdentificationWindow = new(screen, screenNumber);
        newIdentificationWindow.Closed += (_, _) => _screenIdentificationWindows.Remove(newIdentificationWindow);
        _screenIdentificationWindows.Add(newIdentificationWindow);
        newIdentificationWindow.Show();
    }

    private void BuildMonitorControls()
    {
        MonitorControlsPanel.Children.Clear();
        NoMonitorsText.Visibility = _monitorChoices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (MonitorChoice monitor in _monitorChoices)
        {
            DockPanel heading = new();
            TextBlock name = new()
            {
                Text = monitor.DisplayName,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            name.SetResourceReference(
                TextBlock.ForegroundProperty,
                _settings.UseSharedIntensity ? "DisabledTextBrush" : "PrimaryTextBrush");
            System.Windows.Controls.Button identifyButton = new()
            {
                Content = "Identifier",
                Tag = monitor,
                Padding = new Thickness(10, 5, 10, 5),
                IsEnabled = _settings.UseSharedIntensity != true,
                VerticalAlignment = VerticalAlignment.Center
            };
            identifyButton.Click += (_, _) => IdentifyScreens(monitor);
            DockPanel.SetDock(identifyButton, Dock.Right);
            heading.Children.Add(identifyButton);

            heading.Children.Add(name);

            int intensity = GetMonitorIntensity(monitor);
            TextBlock value = new()
            {
                Text = $"{intensity} %",
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            value.SetResourceReference(
                TextBlock.ForegroundProperty,
                _settings.UseSharedIntensity ? "DisabledTextBrush" : "PrimaryTextBrush");
            Slider slider = new()
            {
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 10,
                IsSnapToTickEnabled = true,
                Value = intensity,
                IsEnabled = _settings.UseSharedIntensity != true,
                Tag = monitor.DeviceName,
                Margin = new Thickness(0, 4, 8, 0)
            };
            slider.ValueChanged += (_, e) =>
            {
                value.Text = $"{(int)Math.Round(e.NewValue)} %";
                MonitorIntensitySlider_ValueChanged(slider, e);
            };

            Grid sliderRow = new();
            sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sliderRow.Children.Add(slider);
            Grid.SetColumn(value, 1);
            sliderRow.Children.Add(value);

            Border row = new()
            {
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 6),
                CornerRadius = new CornerRadius(6),
                Child = new StackPanel
                {
                    Children =
                    {
                        heading,
                        sliderRow
                    }
                }
            };
            row.SetResourceReference(
                Border.BackgroundProperty,
                _settings.UseSharedIntensity ? "DisabledMonitorBackgroundBrush" : "WindowBackgroundBrush");
            MonitorControlsPanel.Children.Add(row);
        }
    }

    private void AllScreensToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _isRestoringIntensity || _settings is null)
        {
            return;
        }

        bool useSharedIntensity = AllScreensToggle.IsChecked == true;
        bool previousWouldDimAll = WouldDimEveryScreenFully();
        if (_isDimmed
            && !previousWouldDimAll
            && WouldDimEveryScreenFully(useSharedIntensity: useSharedIntensity)
            && !ConfirmFullScreenDimming())
        {
            _isRestoringIntensity = true;
            AllScreensToggle.IsChecked = !useSharedIntensity;
            _isRestoringIntensity = false;
            StatusText.Text = "Modification annulée. Les intensités précédentes ont été conservées.";
            return;
        }

        _settings.UseSharedIntensity = useSharedIntensity;
        SharedIntensityControls.IsEnabled = useSharedIntensity;
        UpdateSharedIntensityTextColors();
        UpdateMonitorControls();

        if (_isDimmed)
        {
            UpdateOverlayIntensities();
        }

        SaveSettings();
    }

    private void SharedIntensitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int intensity = (int)Math.Round(e.NewValue);
        SharedIntensityValueText.Text = $"{intensity} %";
        if (_isInitializing || _isRestoringIntensity || _settings is null)
        {
            return;
        }

        bool previousWouldDimAll = WouldDimEveryScreenFully();
        if (_isDimmed
            && !previousWouldDimAll
            && WouldDimEveryScreenFully(sharedIntensity: intensity)
            && !ConfirmFullScreenDimming())
        {
            _isRestoringIntensity = true;
            SharedIntensitySlider.Value = _settings.BlackIntensity;
            _isRestoringIntensity = false;
            StatusText.Text = "Modification annulée. L'intensité précédente a été conservée.";
            return;
        }

        _settings.BlackIntensity = intensity;
        if (_settings.UseSharedIntensity == true)
        {
            UpdateMonitorControls();
            if (_isDimmed)
            {
                UpdateOverlayIntensities();
            }
        }

        ScheduleSettingsSave();
    }

    private void MonitorIntensitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider { Tag: string deviceName })
        {
            return;
        }

        int intensity = (int)Math.Round(e.NewValue);
        if (_isInitializing || _isRestoringIntensity || _settings is null)
        {
            return;
        }

        bool previousWouldDimAll = WouldDimEveryScreenFully();
        if (_isDimmed
            && !previousWouldDimAll
            && WouldDimEveryScreenFully(
                monitorDeviceName: deviceName,
                monitorIntensity: intensity)
            && !ConfirmFullScreenDimming())
        {
            _isRestoringIntensity = true;
            ((Slider)sender).Value = GetMonitorIntensity(deviceName);
            _isRestoringIntensity = false;
            StatusText.Text = "Modification annulée. L'intensité précédente a été conservée.";
            return;
        }

        _settings.MonitorIntensities[deviceName] = intensity;
        if (_isDimmed && _settings.UseSharedIntensity != true)
        {
            ScreenOverlayWindow? overlay = _overlays.FirstOrDefault(item => item.DeviceName == deviceName);
            overlay?.SetIntensity(intensity);
        }

        ScheduleSettingsSave();
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isDimmed && _monitorChoices.Count == 0)
        {
            StatusText.Text = "Aucun écran n'est connecté. Actualisez la liste des écrans.";
            return;
        }

        if (!_isDimmed && WouldDimEveryScreenFully() && !ConfirmFullScreenDimming())
        {
            StatusText.Text = "Assombrissement annulé. Aucun écran n'a été modifié.";
            return;
        }

        _isDimmed = !_isDimmed;
        if (_isDimmed)
        {
            if (_monitorChoices.Count == 0)
            {
                _isDimmed = false;
                RemoveOverlays();
                UpdateToggleButton();
            }
            else
            {
                ShowOverlays();
            }
        }
        else
        {
            RemoveOverlays();
        }

        UpdateToggleButton();
    }

    private bool ConfirmFullScreenDimming()
    {
        string hotkey = FormatHotkey(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey);
        ThemedDialogWindow warning = new(
            "Prévention",
            $"Attention, vous voulez assombrir tous les écrans à 100 %. "
            + $"Il sera donc difficile de revenir en arrière si vous ne connaissez pas votre raccourci global : « {hotkey} ».",
            _settings.IsBlueTheme,
            "Continuer quand même",
            "Revenir en arrière",
            confirmIsDefault: false);
        warning.Owner = this;
        return warning.ShowDialog() == true;
    }

    private void ShowInformationDialog(string title, string message, bool isBlueTheme)
    {
        ThemedDialogWindow dialog = new(title, message, isBlueTheme)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            ToggleButton_Click(this, new RoutedEventArgs());
            handled = true;
        }

        return IntPtr.Zero;
    }

    private string? RegisterHotkeyFromOptions(int modifiers, int virtualKey) =>
        TryRegisterHotkey(modifiers, virtualKey, out string errorMessage) ? null : errorMessage;

    private void RestoreHotkeyAfterOptionsCancel() =>
        RestoreHotkey(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey);

    private void RestoreHotkey(int modifiers, int virtualKey)
    {
        if (_hotkeyRegistered && _registeredModifiers == modifiers && _registeredKey == virtualKey)
        {
            return;
        }

        if (TryRegisterHotkey(modifiers, virtualKey, out string errorMessage))
        {
            return;
        }

        if (_hotkeyRegistered)
        {
            _settings.HotkeyModifiers = _registeredModifiers;
            _settings.HotkeyVirtualKey = _registeredKey;
            SaveSettings();
            StatusText.Text = $"Le raccourci précédent n'a pas pu être rétabli. Le raccourci actif est {FormatHotkey(_registeredModifiers, _registeredKey)}.";
        }
        else
        {
            StatusText.Text = $"Aucun raccourci n'est actif. {errorMessage}";
        }
    }

    private bool TryRegisterHotkey(int modifiers, int virtualKey, out string errorMessage)
    {
        errorMessage = string.Empty;
        bool hadPreviousHotkey = _hotkeyRegistered;
        int previousModifiers = _registeredModifiers;
        int previousKey = _registeredKey;

        if (hadPreviousHotkey)
        {
            UnregisterHotKey(_windowHandle, HotkeyId);
            _hotkeyRegistered = false;
        }

        if (RegisterHotKey(_windowHandle, HotkeyId, modifiers | ModNoRepeat, virtualKey))
        {
            _registeredModifiers = modifiers;
            _registeredKey = virtualKey;
            _hotkeyRegistered = true;
            return true;
        }

        int errorCode = Marshal.GetLastWin32Error();
        if (hadPreviousHotkey
            && RegisterHotKey(_windowHandle, HotkeyId, previousModifiers | ModNoRepeat, previousKey))
        {
            _registeredModifiers = previousModifiers;
            _registeredKey = previousKey;
            _hotkeyRegistered = true;
        }

        errorMessage = $"Windows n'a pas pu enregistrer ce raccourci (code {errorCode}). Essayez une autre combinaison.";
        StatusText.Text = errorMessage;
        return false;
    }

    private void RefreshMonitors()
    {
        string? selectedDeviceName = (DestinationMonitorComboBox.SelectedItem as MonitorChoice)?.DeviceName;
        int displayNumber = 1;
        _monitorChoices = [];
        foreach (MonitorChoice screen in MonitorDiscovery.GetScreens())
        {
            MonitorChoice monitor = new(
                screen.DeviceName,
                $"Écran {displayNumber} ({screen.Bounds.Width} × {screen.Bounds.Height})",
                screen.Bounds);
            _monitorChoices.Add(monitor);
            _settings.MonitorIntensities.TryAdd(monitor.DeviceName, _settings.BlackIntensity);
            displayNumber++;
        }

        DestinationMonitorComboBox.ItemsSource = _monitorChoices;
        DestinationMonitorComboBox.SelectedItem = _monitorChoices.FirstOrDefault(
            monitor => monitor.DeviceName == selectedDeviceName);
        if (DestinationMonitorComboBox.SelectedItem is null && _monitorChoices.Count > 0)
        {
            DestinationMonitorComboBox.SelectedIndex = 0;
        }

        NoDestinationMonitorsText.Visibility = _monitorChoices.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateMoveControlsState();
        BuildMonitorControls();
        UpdateToggleButton();
        SharedIntensityControls.IsEnabled = _settings.UseSharedIntensity;
        UpdateSharedIntensityTextColors();
        UpdateMonitorControls();
        if (_isDimmed)
        {
            if (_monitorChoices.Count == 0)
            {
                _isDimmed = false;
                RemoveOverlays();
                UpdateToggleButton();
            }
            else
            {
                ShowOverlays();
            }
        }

        SaveSettings();
    }

    private void ShowOverlays()
    {
        RemoveOverlays();

        foreach (MonitorChoice screen in _monitorChoices)
        {
            ScreenOverlayWindow overlay = new(screen, GetMonitorIntensity(screen));
            overlay.Show();
            _overlays.Add(overlay);
        }

        if (_overlays.Count == 0)
        {
            StatusText.Text = "Aucun écran n'est connecté. Actualisez la liste des écrans.";
        }
        else
        {
            StartMouseInactivityMonitoring();
        }

        UpdateToggleButton();
    }

    private int GetMonitorIntensity(MonitorChoice monitor) =>
        GetMonitorIntensity(monitor.DeviceName);

    private int GetMonitorIntensity(string deviceName) =>
        _settings.UseSharedIntensity
            ? _settings.BlackIntensity
            : GetIndividualMonitorIntensity(deviceName);

    private int GetIndividualMonitorIntensity(string deviceName) =>
        _settings.MonitorIntensities.TryGetValue(deviceName, out int intensity)
            ? intensity
            : _settings.BlackIntensity;

    private bool WouldDimEveryScreenFully(
        bool? useSharedIntensity = null,
        int? sharedIntensity = null,
        string? monitorDeviceName = null,
        int? monitorIntensity = null)
    {
        if (_monitorChoices.Count == 0)
        {
            return false;
        }

        bool sharedMode = useSharedIntensity ?? _settings.UseSharedIntensity;
        return _monitorChoices.All(monitor =>
        {
            int intensity = sharedMode
                ? sharedIntensity ?? _settings.BlackIntensity
                : monitor.DeviceName == monitorDeviceName
                    ? monitorIntensity ?? GetIndividualMonitorIntensity(monitor.DeviceName)
                    : GetIndividualMonitorIntensity(monitor.DeviceName);
            return intensity == 100;
        });
    }

    private void UpdateOverlayIntensities()
    {
        foreach (ScreenOverlayWindow overlay in _overlays)
        {
            overlay.SetIntensity(_settings.UseSharedIntensity
                ? _settings.BlackIntensity
                : GetMonitorIntensity(overlay.DeviceName));
        }
    }

    private void UpdateMonitorControls()
    {
        bool useSharedIntensity = _settings.UseSharedIntensity;
        _isRestoringIntensity = true;
        foreach (Border row in MonitorControlsPanel.Children.OfType<Border>())
        {
            StackPanel content = (StackPanel)row.Child;
            DockPanel heading = content.Children.OfType<DockPanel>().First();
            System.Windows.Controls.Button identifyButton = heading.Children
                .OfType<System.Windows.Controls.Button>()
                .First();

            Grid sliderRow = content.Children.OfType<Grid>().First();
            Slider slider = sliderRow.Children.OfType<Slider>().First();
            TextBlock value = sliderRow.Children.OfType<TextBlock>().First();
            TextBlock name = heading.Children.OfType<TextBlock>().First();
            string deviceName = slider.Tag as string
                ?? throw new InvalidOperationException("Un identifiant d'écran manque dans les contrôles.");
            identifyButton.IsEnabled = !useSharedIntensity;
            slider.IsEnabled = !useSharedIntensity;
            row.SetResourceReference(
                Border.BackgroundProperty,
                useSharedIntensity ? "DisabledMonitorBackgroundBrush" : "WindowBackgroundBrush");
            string textBrushKey = useSharedIntensity ? "DisabledTextBrush" : "PrimaryTextBrush";
            name.SetResourceReference(TextBlock.ForegroundProperty, textBrushKey);
            value.SetResourceReference(TextBlock.ForegroundProperty, textBrushKey);
            name.Text = _monitorChoices.First(item => item.DeviceName == deviceName).DisplayName;
            int intensity = useSharedIntensity
                ? _settings.BlackIntensity
                : GetIndividualMonitorIntensity(deviceName);
            slider.Value = intensity;
            value.Text = $"{intensity} %";
        }

        _isRestoringIntensity = false;
    }

    private void UpdateSharedIntensityTextColors()
    {
        bool isEnabled = _settings.UseSharedIntensity;
        AllScreensTitleText.SetResourceReference(
            TextBlock.ForegroundProperty,
            isEnabled ? "PrimaryTextBrush" : "DisabledTextBrush");
        AllScreensDescriptionText.SetResourceReference(
            TextBlock.ForegroundProperty,
            isEnabled ? "SecondaryTextBrush" : "DisabledTextBrush");
        SharedIntensityLabelText.SetResourceReference(
            TextBlock.ForegroundProperty,
            isEnabled ? "SecondaryTextBrush" : "DisabledTextBrush");
        SharedIntensityValueText.SetResourceReference(
            TextBlock.ForegroundProperty,
            isEnabled ? "PrimaryTextBrush" : "DisabledTextBrush");
    }

    private void RemoveOverlays()
    {
        StopMouseInactivityMonitoring();

        foreach (ScreenOverlayWindow overlay in _overlays)
        {
            overlay.Close();
        }

        _overlays.Clear();
    }

    private void StartMouseInactivityMonitoring()
    {
        StopMouseInactivityMonitoring();
        _cursorHideUnavailable = false;
        _mouseHookProcedure = MouseHookProcedure;
        _mouseHookHandle = SetWindowsHookEx(
            WhMouseLowLevel,
            _mouseHookProcedure,
            GetModuleHandle(null),
            0);

        if (_mouseHookHandle == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            _mouseHookProcedure = null;
            StatusText.Text = $"Le suivi de la souris n'a pas pu démarrer (code {errorCode}). Le curseur restera visible.";
            return;
        }

        Interlocked.Exchange(ref _lastMouseMoveTick, Environment.TickCount64);
        _mouseInactivityTimer.Start();
        StatusText.Text = "Le curseur se masquera après 3 secondes sans mouvement.";
    }

    private void StopMouseInactivityMonitoring()
    {
        _mouseInactivityTimer.Stop();
        RestoreMouseCursor();

        if (_mouseHookHandle == IntPtr.Zero)
        {
            _mouseHookProcedure = null;
            return;
        }

        if (!UnhookWindowsHookEx(_mouseHookHandle))
        {
            int errorCode = Marshal.GetLastWin32Error();
            StatusText.Text = $"Le suivi de la souris n'a pas pu s'arrêter correctement (code {errorCode}).";
            return;
        }

        _mouseHookHandle = IntPtr.Zero;
        _mouseHookProcedure = null;
    }

    private IntPtr MouseHookProcedure(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message.ToInt32() == WmMouseMove)
        {
            Interlocked.Exchange(ref _lastMouseMoveTick, Environment.TickCount64);
        }

        return CallNextHookEx(_mouseHookHandle, code, message, data);
    }

    private void MouseInactivityTimer_Tick(object? sender, EventArgs e)
    {
        long elapsed = Environment.TickCount64 - Interlocked.Read(ref _lastMouseMoveTick);
        if (elapsed >= CursorHideDelay.TotalMilliseconds)
        {
            HideMouseCursor();
        }
        else
        {
            RestoreMouseCursor();
        }
    }

    private void HideMouseCursor()
    {
        if (_cursorHidden || _cursorHideUnavailable)
        {
            return;
        }

        if (!CursorVisibility.TryHide(out int errorCode))
        {
            _cursorHideUnavailable = true;
            StatusText.Text = $"Windows n'a pas pu masquer le curseur (code {errorCode}). L'assombrissement reste actif.";
            return;
        }

        _cursorHidden = true;
        StatusText.Text = "Le curseur est masqué après 3 secondes d'inactivité.";
    }

    private void RestoreMouseCursor()
    {
        if (!_cursorHidden)
        {
            return;
        }

        if (!CursorVisibility.TryRestore(out int errorCode))
        {
            StatusText.Text = $"Windows n'a pas pu rétablir le curseur (code {errorCode}).";
            return;
        }

        _cursorHidden = false;
        StatusText.Text = "Le curseur est de nouveau visible.";
    }

    private void UpdateToggleButton()
    {
        ToggleButton.Content = _isDimmed ? "Eclairer les écrans" : "Assombrir les écrans";
        ToggleButton.Tag = _isDimmed ? "Wake" : "Dim";
        ToggleButton.IsEnabled = _monitorChoices.Count > 0;
        ToggleButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x39, 0x78, 0xD4));
    }

    private string GetReadyStatusText() =>
        $"Prêt. Le raccourci pour assombrir ou éclairer est {FormatHotkey(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey)}.";

    private void NormalizeSettings()
    {
        _settings.BlackIntensity = Math.Clamp(_settings.BlackIntensity, 0, 100);
        _settings.MonitorIntensities ??= [];
        foreach (string deviceName in _settings.MonitorIntensities.Keys.ToArray())
        {
            _settings.MonitorIntensities[deviceName] =
                Math.Clamp(_settings.MonitorIntensities[deviceName], 0, 100);
        }

        const int validModifiers = ModAlt | ModControl | ModShift | ModWindows;
        if (_settings.HotkeyVirtualKey is < 1 or > 255
            || _settings.HotkeyModifiers == 0
            || (_settings.HotkeyModifiers & ~validModifiers) != 0)
        {
            _settings.HotkeyModifiers = ModControl | ModAlt;
            _settings.HotkeyVirtualKey = 0x42;
        }
    }

    private void ApplyTheme()
    {
        bool isBlue = _settings.IsBlueTheme;
        Resources["WindowBackgroundBrush"] = isBlue
            ? CreateBrush(0x18, 0x20, 0x2B)
            : CreateBrush(0x00, 0x00, 0x00);
        Resources["PrimaryTextBrush"] = CreateBrush(0xF1, 0xF5, 0xF9);
        Resources["SecondaryTextBrush"] = CreateBrush(0xA0, 0xAE, 0xC0);
        Resources["DisabledTextBrush"] = isBlue
            ? CreateBrush(0xA0, 0xAE, 0xC0)
            : CreateBrush(0xA0, 0xA0, 0xA0);
        Resources["CardBackgroundBrush"] = isBlue
            ? CreateBrush(0x24, 0x2D, 0x3A)
            : CreateBrush(0x16, 0x16, 0x16);
        Resources["DisabledMonitorBackgroundBrush"] = isBlue
            ? CreateBrush(0x24, 0x2D, 0x3A)
            : CreateBrush(0x16, 0x16, 0x16);
        Resources["FieldBackgroundBrush"] = isBlue
            ? CreateBrush(0x30, 0x39, 0x47)
            : CreateBrush(0x1F, 0x1F, 0x1F);
        Resources["FieldBorderBrush"] = isBlue
            ? CreateBrush(0x4B, 0x55, 0x63)
            : CreateBrush(0x48, 0x48, 0x48);
        Resources["ButtonBorderBrush"] = isBlue
            ? CreateBrush(0x4B, 0x55, 0x63)
            : CreateBrush(0x80, 0x80, 0x80);
        Resources["ButtonHoverBorderBrush"] = isBlue
            ? CreateBrush(0x67, 0x9B, 0xF2)
            : CreateBrush(0xA0, 0xA0, 0xA0);
        Resources["ButtonBackgroundBrush"] = isBlue
            ? CreateBrush(0x37, 0x41, 0x50)
            : CreateBrush(0x2B, 0x2B, 0x2B);
        Resources["AccentBrush"] = CreateBrush(0x67, 0x9B, 0xF2);
        Resources["SwitchOffBrush"] = isBlue
            ? CreateBrush(0x58, 0x63, 0x73)
            : CreateBrush(0x48, 0x48, 0x48);
    }

    private static SolidColorBrush CreateBrush(int red, int green, int blue) =>
        new(System.Windows.Media.Color.FromRgb((byte)red, (byte)green, (byte)blue));

    private bool SaveSettings()
    {
        _settingsSaveTimer.Stop();
        try
        {
            SettingsStore.Save(_settings);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"Les paramètres n'ont pas pu être enregistrés : {exception.Message}";
            return false;
        }
    }

    private void ScheduleSettingsSave() => _settingsSaveTimer.Start();

    private void SettingsSaveTimer_Tick(object? sender, EventArgs e) => SaveSettings();

    internal static int GetNativeModifiers(ModifierKeys modifiers)
    {
        int nativeModifiers = 0;
        if ((modifiers & ModifierKeys.Alt) != 0) nativeModifiers |= ModAlt;
        if ((modifiers & ModifierKeys.Control) != 0) nativeModifiers |= ModControl;
        if ((modifiers & ModifierKeys.Shift) != 0) nativeModifiers |= ModShift;
        if ((modifiers & ModifierKeys.Windows) != 0) nativeModifiers |= ModWindows;
        return nativeModifiers;
    }

    internal static string FormatHotkey(int modifiers, int virtualKey)
    {
        List<string> parts = [];
        if ((modifiers & ModControl) != 0) parts.Add("Ctrl");
        if ((modifiers & ModAlt) != 0) parts.Add("Alt");
        if ((modifiers & ModShift) != 0) parts.Add("Maj");
        if ((modifiers & ModWindows) != 0) parts.Add("Windows");
        parts.Add(KeyInterop.KeyFromVirtualKey(virtualKey).ToString());
        return string.Join(" + ", parts);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr handle, int id, int modifiers, int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr handle, int id);

    private delegate IntPtr LowLevelMouseProcedure(int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookType,
        LowLevelMouseProcedure callback,
        IntPtr module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
