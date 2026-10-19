using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Sermon;

/// <summary>Obsidian 式工作台对话框：全局搜索 / 大纲 / 命令面板三合一。</summary>
public sealed class WorkbenchDialog : Window
{
    private readonly MainWindow _owner;
    private readonly TextBox _input;
    private readonly ListBox _results;
    private readonly TextBlock _hint;
    private WorkbenchMode _mode;
    private List<VaultSearchEngine.SearchHit> _searchHits = new();
    private List<(int Line, string Title, int Level)> _outline = new();
    private List<WorkbenchCommand> _commands = new();
    private CancellationTokenSource? _searchCts;

    public enum WorkbenchMode
    {
        Search,
        Outline,
        Command
    }

    public sealed record WorkbenchCommand(string Title, string Hint, Action Run);

    private WorkbenchDialog(MainWindow owner, WorkbenchMode mode)
    {
        _owner = owner;
        _mode = mode;
        Width = 640;
        Height = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Title = ModeTitle(mode);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*") };
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(14, 14, 14, 0) };
        tabs.Children.Add(MakeModeButton("搜索", WorkbenchMode.Search));
        tabs.Children.Add(MakeModeButton("大纲", WorkbenchMode.Outline));
        tabs.Children.Add(MakeModeButton("命令", WorkbenchMode.Command));
        root.Children.Add(tabs);

        _hint = new TextBlock { Margin = new Thickness(14, 8, 14, 0), Foreground = new SolidColorBrush(Color.FromRgb(106, 106, 100)) };
        Grid.SetRow(_hint, 1);
        root.Children.Add(_hint);

        _input = new TextBox { Margin = new Thickness(14, 8, 14, 0), Watermark = "输入关键词，回车跳到首个结果" };
        _input.TextChanged += (_, _) => _ = RefreshAsync();
        _input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ActivateFirst();
                e.Handled = true;
            }
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
        Grid.SetRow(_input, 2);
        root.Children.Add(_input);

        _results = new ListBox { Margin = new Thickness(14) };
        _results.DoubleTapped += (_, _) => ActivateFirst();
        Grid.SetRow(_results, 3);
        root.Children.Add(_results);

        Content = root;
        Opened += (_, _) =>
        {
            _input.Focus();
            _ = RefreshAsync();
        };
    }

    public static async Task ShowAsync(MainWindow owner, int mode)
    {
        var dialog = new WorkbenchDialog(owner, (WorkbenchMode)mode);
        await dialog.ShowDialog(owner);
    }

    private static string ModeTitle(WorkbenchMode mode)
    {
        return mode switch
        {
            WorkbenchMode.Search => "全局搜索 (Ctrl+Shift+F)",
            WorkbenchMode.Outline => "文档大纲 (Ctrl+Shift+O)",
            _ => "命令面板 (Ctrl+P)"
        };
    }

    private Button MakeModeButton(string title, WorkbenchMode mode)
    {
        var button = new Button { Content = title, Padding = new Thickness(12, 6) };
        button.Click += (_, _) =>
        {
            _mode = mode;
            Title = ModeTitle(mode);
            _ = RefreshAsync();
        };
        return button;
    }

    private async Task RefreshAsync()
    {
        var query = _input.Text?.Trim() ?? string.Empty;
        if (_mode == WorkbenchMode.Search)
        {
            _hint.Text = string.IsNullOrEmpty(_owner.CurrentVaultForDialog)
                ? "请先打开知识库（Vault）"
                : "全文搜索 Vault 内 Markdown，文件名优先";
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            try
            {
                _searchHits = string.IsNullOrEmpty(_owner.CurrentVaultForDialog)
                    ? new List<VaultSearchEngine.SearchHit>()
                    : await VaultSearchEngine.SearchAsync(_owner.CurrentVaultForDialog, query, 60, _searchCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _results.ItemsSource = _searchHits.Count == 0
                ? new List<string> { string.IsNullOrEmpty(query) ? "（输入关键词开始搜索）" : "（无匹配结果）" }
                : _searchHits.Select(h => $"{h.FileName}\n{h.Snippet}").ToList();
            return;
        }

        if (_mode == WorkbenchMode.Outline)
        {
            _outline = VaultSearchEngine.ExtractOutline(_owner.CurrentDocumentForDialog);
            var filtered = string.IsNullOrEmpty(query)
                ? _outline
                : _outline.Where(o => o.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            _hint.Text = $"共 {filtered.Count} 个标题";
            _results.ItemsSource = filtered.Count == 0
                ? new List<string> { "（当前文档无标题）" }
                : filtered.Select(o => $"{new string(' ', (o.Level - 1) * 2)}H{o.Level} · L{o.Line}  {o.Title}").ToList();
            return;
        }

        _commands = _owner.BuildWorkbenchCommands(query, Close);
        _hint.Text = $"共 {_commands.Count} 条命令";
        _results.ItemsSource = _commands.Count == 0
            ? new List<string> { "（无匹配命令）" }
            : _commands.Select(c => $"{c.Title}\n{c.Hint}").ToList();
    }

    private void ActivateFirst()
    {
        if (_mode == WorkbenchMode.Search && _searchHits.Count > 0)
        {
            var hit = _searchHits[_results.SelectedIndex >= 0 ? _results.SelectedIndex : 0];
            Close();
            _owner.OpenFileAndReveal(hit.FilePath);
            return;
        }

        if (_mode == WorkbenchMode.Outline)
        {
            var query = _input.Text?.Trim() ?? string.Empty;
            var filtered = string.IsNullOrEmpty(query)
                ? _outline
                : _outline.Where(o => o.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            if (filtered.Count > 0)
            {
                var item = filtered[_results.SelectedIndex >= 0 ? _results.SelectedIndex : 0];
                Close();
                _owner.RevealLine(item.Line);
            }

            return;
        }

        if (_commands.Count > 0)
        {
            var command = _commands[_results.SelectedIndex >= 0 ? _results.SelectedIndex : 0];
            Close();
            command.Run();
        }
    }
}
