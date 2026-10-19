using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Sermon;

/// <summary>设置对话框：主题/字号/自动保存，写入 settings.json。</summary>
public sealed class SettingsDialog : Window
{
    private SettingsDialog(MainWindow owner, AppSettings settings, Action onChanged)
    {
        Width = 420;
        Height = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Title = "设置";

        var root = new StackPanel { Margin = new Thickness(18), Spacing = 12 };
        var themeBox = new ComboBox
        {
            ItemsSource = new[] { "明亮主题", "暗黑主题" },
            SelectedIndex = settings.IsDark ? 1 : 0
        };
        root.Children.Add(new TextBlock { Text = "主题" });
        root.Children.Add(themeBox);

        var fontSlider = new Slider { Minimum = 11, Maximum = 26, Value = settings.EditorFontSize, TickFrequency = 1, IsSnapToTickEnabled = true };
        var fontLabel = new TextBlock { Text = $"编辑器字号：{settings.EditorFontSize:F0}" };
        fontSlider.ValueChanged += (_, _) => fontLabel.Text = $"编辑器字号：{fontSlider.Value:F0}";
        root.Children.Add(fontLabel);
        root.Children.Add(fontSlider);

        var autoSaveSlider = new Slider { Minimum = 1, Maximum = 30, Value = settings.AutoSaveMinutes, TickFrequency = 1, IsSnapToTickEnabled = true };
        var autoSaveLabel = new TextBlock { Text = $"自动保存间隔：{settings.AutoSaveMinutes} 分钟" };
        autoSaveSlider.ValueChanged += (_, _) => autoSaveLabel.Text = $"自动保存间隔：{autoSaveSlider.Value:F0} 分钟";
        root.Children.Add(autoSaveLabel);
        root.Children.Add(autoSaveSlider);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "保存", Padding = new Thickness(16, 6) };
        ok.Click += (_, _) =>
        {
            settings.Theme = themeBox.SelectedIndex == 1 ? "dark" : "light";
            settings.EditorFontSize = fontSlider.Value;
            settings.AutoSaveMinutes = (int)autoSaveSlider.Value;
            onChanged();
            Close();
        };
        var cancel = new Button { Content = "取消", Padding = new Thickness(16, 6) };
        cancel.Click += (_, _) => Close();
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        Content = new ScrollViewer { Content = root };
    }

    public static async Task ShowAsync(MainWindow owner, AppSettings settings, Action onChanged)
    {
        var dialog = new SettingsDialog(owner, settings, onChanged);
        await dialog.ShowDialog(owner);
    }
}
