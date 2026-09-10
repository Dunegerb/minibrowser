using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MiniBrowser.Core.History;

public sealed class HistoryWindow : Window
{
    private readonly SessionHistoryService _history;
    private readonly DataGrid _grid;

    public HistoryWindow(SessionHistoryService history, Action<string> openUrl)
    {
        _history = history;
        Title = "MiniBrowser — Histórico desta sessão";
        Width = 900;
        Height = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;

        var root = new DockPanel { LastChildFill = true };
        var text = new TextBlock
        {
            Text = "Histórico temporário: não é salvo entre reinicializações.",
            Margin = new Thickness(14, 12, 14, 8),
            Foreground = System.Windows.Media.Brushes.DimGray
        };
        DockPanel.SetDock(text, Dock.Top);
        root.Children.Add(text);

        _grid = new DataGrid
        {
            Margin = new Thickness(12),
            IsReadOnly = true,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Hora", Binding = new Binding(nameof(SessionHistoryEntry.TimestampUtc)) { StringFormat = "HH:mm:ss" }, Width = 90 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Capsule", Binding = new Binding(nameof(SessionHistoryEntry.CapsuleName)), Width = 100 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Título", Binding = new Binding(nameof(SessionHistoryEntry.Title)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "URL", Binding = new Binding(nameof(SessionHistoryEntry.Url)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        _grid.MouseDoubleClick += (_, _) =>
        {
            if (_grid.SelectedItem is SessionHistoryEntry entry)
            {
                openUrl(entry.Url);
                Close();
            }
        };
        root.Children.Add(_grid);
        Content = root;
        Refresh();
    }

    private void Refresh() => _grid.ItemsSource = _history.Snapshot();
}
