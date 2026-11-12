using System.Windows;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;

namespace TamaNotch.Helpers;

/// <summary>
/// Tombol × di dalam kolom pencarian: <c>icons:ClearTextButton.Target="{Binding ElementName=SearchBox}"</c>
/// mengosongkan TextBox tersebut (binding UpdateSourceTrigger=PropertyChanged ikut kosong) lalu memfokuskannya lagi.
/// </summary>
public static class ClearTextButton
{
    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target", typeof(TextBox), typeof(ClearTextButton), new PropertyMetadata(null, OnTargetChanged));

    public static TextBox? GetTarget(DependencyObject d) => (TextBox?)d.GetValue(TargetProperty);
    public static void SetTarget(DependencyObject d, TextBox? value) => d.SetValue(TargetProperty, value);

    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Button button) return;
        button.Click -= OnClick;
        if (e.NewValue != null) button.Click += OnClick;
    }

    private static void OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || GetTarget(button) is not { } box) return;
        box.Clear();
        box.Focus();
    }
}
