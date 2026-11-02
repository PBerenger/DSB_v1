using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfControl = System.Windows.Controls.Control;
using WpfCursors = System.Windows.Input.Cursors;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace DoubleSceensBlack;

internal sealed class ThemedDialogWindow : Window
{
    public ThemedDialogWindow(
        string title,
        string message,
        bool isBlueTheme,
        string confirmText = "OK",
        string? cancelText = null,
        bool confirmIsDefault = true)
    {
        WpfColor background = isBlueTheme
            ? WpfColor.FromRgb(0x18, 0x20, 0x2B)
            : WpfColor.FromRgb(0, 0, 0);
        WpfColor cardBackground = isBlueTheme
            ? WpfColor.FromRgb(0x24, 0x2D, 0x3A)
            : WpfColor.FromRgb(0x16, 0x16, 0x16);
        WpfColor borderColor = isBlueTheme
            ? WpfColor.FromRgb(0x4B, 0x55, 0x63)
            : WpfColor.FromRgb(0x48, 0x48, 0x48);
        WpfColor primaryText = WpfColor.FromRgb(0xF1, 0xF5, 0xF9);
        WpfColor secondaryText = WpfColor.FromRgb(0xA0, 0xAE, 0xC0);
        WpfColor accent = isBlueTheme
            ? WpfColor.FromRgb(0x67, 0x9B, 0xF2)
            : WpfColor.FromRgb(0x80, 0x80, 0x80);
        SolidColorBrush backgroundBrush = new(background);
        SolidColorBrush cardBrush = new(cardBackground);
        SolidColorBrush borderBrush = new(borderColor);
        SolidColorBrush primaryTextBrush = new(primaryText);
        SolidColorBrush secondaryTextBrush = new(secondaryText);
        SolidColorBrush accentBrush = new(accent);

        Title = title;
        Width = 520;
        MinWidth = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = backgroundBrush;
        Foreground = primaryTextBrush;

        Style buttonStyle = new(typeof(WpfButton));
        buttonStyle.Setters.Add(new Setter(WpfControl.PaddingProperty, new Thickness(14, 9, 14, 9)));
        buttonStyle.Setters.Add(new Setter(WpfControl.FontSizeProperty, 14d));
        buttonStyle.Setters.Add(new Setter(WpfControl.ForegroundProperty, primaryTextBrush));
        buttonStyle.Setters.Add(new Setter(WpfControl.BackgroundProperty, cardBrush));
        buttonStyle.Setters.Add(new Setter(WpfControl.BorderBrushProperty, borderBrush));
        buttonStyle.Setters.Add(new Setter(WpfControl.BorderThicknessProperty, new Thickness(1)));
        buttonStyle.Setters.Add(new Setter(WpfControl.CursorProperty, WpfCursors.Hand));
        buttonStyle.Setters.Add(new Setter(WpfControl.TemplateProperty, CreateButtonTemplate()));

        Grid layout = new();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid titleBar = new();
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleBar.Background = backgroundBrush;
        titleBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not WpfButton)
            {
                DragMove();
            }
        };
        TextBlock titleText = new()
        {
            Text = title,
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            Foreground = primaryTextBrush
        };
        titleBar.Children.Add(titleText);

        Style closeButtonStyle = new(typeof(WpfButton));
        closeButtonStyle.Setters.Add(new Setter(WpfControl.WidthProperty, 44d));
        closeButtonStyle.Setters.Add(new Setter(WpfControl.HeightProperty, 40d));
        closeButtonStyle.Setters.Add(new Setter(WpfControl.PaddingProperty, new Thickness(0)));
        closeButtonStyle.Setters.Add(new Setter(WpfControl.BackgroundProperty, WpfBrushes.Transparent));
        closeButtonStyle.Setters.Add(new Setter(WpfControl.ForegroundProperty, primaryTextBrush));
        closeButtonStyle.Setters.Add(new Setter(WpfControl.BorderThicknessProperty, new Thickness(0)));
        closeButtonStyle.Setters.Add(new Setter(WpfControl.FontSizeProperty, 16d));
        closeButtonStyle.Setters.Add(new Setter(WpfControl.CursorProperty, WpfCursors.Hand));
        closeButtonStyle.Setters.Add(new Setter(WpfControl.TemplateProperty, CreateButtonTemplate()));
        closeButtonStyle.Triggers.Add(new Trigger
        {
            Property = WpfControl.IsMouseOverProperty,
            Value = true,
            Setters =
            {
                new Setter(WpfControl.BackgroundProperty, new SolidColorBrush(WpfColor.FromRgb(0xC4, 0x2B, 0x1C))),
                new Setter(WpfControl.ForegroundProperty, WpfBrushes.White)
            }
        });

        WpfButton closeButton = new()
        {
            Content = "×",
            Style = closeButtonStyle,
            ToolTip = "Fermer"
        };
        closeButton.Click += (_, _) => DialogResult = false;
        Grid.SetColumn(closeButton, 1);
        titleBar.Children.Add(closeButton);
        Grid.SetRow(titleBar, 0);
        layout.Children.Add(titleBar);

        TextBlock messageText = new()
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = secondaryTextBrush,
            Margin = new Thickness(24, 24, 24, 20)
        };
        Grid.SetRow(messageText, 1);
        layout.Children.Add(messageText);

        StackPanel buttons = new()
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(24, 0, 24, 24)
        };

        if (cancelText is not null)
        {
            WpfButton cancelButton = new()
            {
                Content = cancelText,
                MinWidth = 140,
                Margin = new Thickness(0, 0, 10, 0),
                IsCancel = true,
                IsDefault = !confirmIsDefault,
                Style = buttonStyle
            };
            cancelButton.Click += (_, _) => DialogResult = false;
            buttons.Children.Add(cancelButton);
        }

        WpfButton confirmButton = new()
        {
            Content = confirmText,
            MinWidth = 120,
            IsDefault = confirmIsDefault,
            Background = accentBrush,
            BorderBrush = accentBrush,
            Foreground = WpfBrushes.White,
            Style = buttonStyle
        };
        confirmButton.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(confirmButton);
        Grid.SetRow(buttons, 2);
        layout.Children.Add(buttons);

        Border outerBorder = new()
        {
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            Background = backgroundBrush,
            Child = layout
        };
        Content = outerBorder;
    }

    private static ControlTemplate CreateButtonTemplate()
    {
        FrameworkElementFactory border = new(typeof(Border));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(WpfControl.Background))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(WpfControl.BorderBrush))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(WpfControl.BorderThickness))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

        FrameworkElementFactory presenter = new(typeof(ContentPresenter));
        presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding(nameof(ContentControl.Content))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        presenter.SetBinding(ContentPresenter.MarginProperty, new System.Windows.Data.Binding(nameof(WpfControl.Padding))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        border.AppendChild(presenter);

        return new ControlTemplate(typeof(WpfButton)) { VisualTree = border };
    }
}
