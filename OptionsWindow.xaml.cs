using System.Windows;
using System.Windows.Input;

namespace DoubleSceensBlack;

public partial class OptionsWindow : Window
{
    private readonly int _defaultHotkeyModifiers;
    private readonly int _defaultHotkeyVirtualKey;
    private readonly Func<int, int, string?> _registerHotkey;
    private int _hotkeyModifiers;
    private int _hotkeyVirtualKey;
    private bool _isCapturingHotkey;

    public OptionsWindow(
        bool isBlueTheme,
        bool startWithWindows,
        bool minimizeOnClose,
        int hotkeyModifiers,
        int hotkeyVirtualKey,
        int defaultHotkeyModifiers,
        int defaultHotkeyVirtualKey,
        Func<int, int, string?> registerHotkey)
    {
        InitializeComponent();

        ThemeComboBox.SelectedIndex = isBlueTheme ? 0 : 1;
        StartupToggle.IsChecked = startWithWindows;
        MinimizeToggle.IsChecked = minimizeOnClose;
        _hotkeyModifiers = hotkeyModifiers;
        _hotkeyVirtualKey = hotkeyVirtualKey;
        _defaultHotkeyModifiers = defaultHotkeyModifiers;
        _defaultHotkeyVirtualKey = defaultHotkeyVirtualKey;
        _registerHotkey = registerHotkey;
        HotkeyTextBox.Text = MainWindow.FormatHotkey(hotkeyModifiers, hotkeyVirtualKey);
        ApplyTheme(isBlueTheme);
    }

    public bool IsBlueTheme => ThemeComboBox.SelectedIndex == 0;
    public bool StartWithWindows => StartupToggle.IsChecked == true;
    public bool MinimizeOnClose => MinimizeToggle.IsChecked == true;
    public int HotkeyModifiers => _hotkeyModifiers;
    public int HotkeyVirtualKey => _hotkeyVirtualKey;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void ThemeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ThemeComboBox is not null)
        {
            ApplyTheme(ThemeComboBox.SelectedIndex == 0);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void ChangeHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCapturingHotkey)
        {
            CancelHotkeyCapture();
            return;
        }

        _isCapturingHotkey = true;
        ChangeHotkeyButton.Content = "Annuler";
        HotkeyTextBox.Text = "Appuyez sur votre combinaison de touches…";
        HotkeyStatusText.Text = "Entrez une combinaison avec Ctrl, Alt, Maj ou Windows.";
        HotkeyTextBox.Focus();
    }

    private void ResetHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        _isCapturingHotkey = false;
        ChangeHotkeyButton.Content = "Modifier";
        string? error = _registerHotkey(_defaultHotkeyModifiers, _defaultHotkeyVirtualKey);
        if (error is not null)
        {
            HotkeyStatusText.Text = error;
            return;
        }

        _hotkeyModifiers = _defaultHotkeyModifiers;
        _hotkeyVirtualKey = _defaultHotkeyVirtualKey;
        HotkeyTextBox.Text = MainWindow.FormatHotkey(_hotkeyModifiers, _hotkeyVirtualKey);
        HotkeyStatusText.Text = "Raccourci rétabli par défaut : Ctrl + Alt + B.";
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_isCapturingHotkey)
        {
            return;
        }

        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            CancelHotkeyCapture();
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        int modifiers = MainWindow.GetNativeModifiers(Keyboard.Modifiers);
        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (modifiers == 0 || virtualKey == 0)
        {
            HotkeyStatusText.Text = "Le raccourci doit inclure Ctrl, Alt, Maj ou Windows.";
            return;
        }

        string newHotkey = MainWindow.FormatHotkey(modifiers, virtualKey);
        string? error = _registerHotkey(modifiers, virtualKey);
        if (error is not null)
        {
            HotkeyStatusText.Text = error;
            return;
        }

        ThemedDialogWindow confirmationDialog = new(
            "Confirmer le raccourci",
            $"Voulez-vous confirmer le nouveau raccourci clavier ?\n\n{newHotkey}",
            IsBlueTheme,
            "Oui",
            "Non")
        {
            Owner = this
        };
        bool confirmed = confirmationDialog.ShowDialog() == true;

        _isCapturingHotkey = false;
        ChangeHotkeyButton.Content = "Modifier";
        if (confirmed)
        {
            _hotkeyModifiers = modifiers;
            _hotkeyVirtualKey = virtualKey;
            HotkeyTextBox.Text = newHotkey;
            HotkeyStatusText.Text = "Nouveau raccourci sélectionné. Enregistrez les options pour le conserver.";
            return;
        }

        error = _registerHotkey(_hotkeyModifiers, _hotkeyVirtualKey);
        if (error is not null)
        {
            _hotkeyModifiers = modifiers;
            _hotkeyVirtualKey = virtualKey;
            HotkeyTextBox.Text = newHotkey;
            HotkeyStatusText.Text = $"L'ancien raccourci n'a pas pu être rétabli : {error}";
            return;
        }

        HotkeyTextBox.Text = MainWindow.FormatHotkey(_hotkeyModifiers, _hotkeyVirtualKey);
        HotkeyStatusText.Text = "Modification annulée. L'ancien raccourci a été conservé.";
    }

    private void CancelHotkeyCapture()
    {
        _isCapturingHotkey = false;
        ChangeHotkeyButton.Content = "Modifier";
        HotkeyTextBox.Text = MainWindow.FormatHotkey(_hotkeyModifiers, _hotkeyVirtualKey);
        HotkeyStatusText.Text = "Modification du raccourci annulée.";
    }

    private void ApplyTheme(bool isBlue)
    {
        Resources["WindowBackgroundBrush"] = isBlue
            ? CreateBrush(0x18, 0x20, 0x2B)
            : CreateBrush(0x00, 0x00, 0x00);
        Resources["PrimaryTextBrush"] = CreateBrush(0xF1, 0xF5, 0xF9);
        Resources["SecondaryTextBrush"] = CreateBrush(0xA0, 0xAE, 0xC0);
        Resources["CardBackgroundBrush"] = isBlue
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
        Resources["AccentBrush"] = CreateBrush(0x67, 0x9B, 0xF2);
        Resources["SwitchOffBrush"] = isBlue
            ? CreateBrush(0x58, 0x63, 0x73)
            : CreateBrush(0x48, 0x48, 0x48);
    }

    private static System.Windows.Media.SolidColorBrush CreateBrush(int red, int green, int blue) =>
        new(System.Windows.Media.Color.FromRgb((byte)red, (byte)green, (byte)blue));
}
