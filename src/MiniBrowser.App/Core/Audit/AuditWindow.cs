using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MiniBrowser.Core.Audit;

public sealed class AuditWindow : Window
{
    private readonly NetworkAuditService _audit;
    private readonly DataGrid _grid;

    public AuditWindow(NetworkAuditService audit)
    {
        _audit = audit;
        Title = "MiniBrowser — Network Audit (local)";
        Width = 1100;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel();
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        var refresh = new Button { Content = "Atualizar", Padding = new Thickness(12, 4, 12, 4) };
        refresh.Click += (_, _) => Refresh();
        toolbar.Children.Add(refresh);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        _grid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "UTC", Binding = new Binding(nameof(NetworkAuditEntry.TimestampUtc)) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Tab", Binding = new Binding(nameof(NetworkAuditEntry.TabId)) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Frame", Binding = new Binding(nameof(NetworkAuditEntry.FrameId)) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Tipo", Binding = new Binding(nameof(NetworkAuditEntry.ResourceType)) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Método", Binding = new Binding(nameof(NetworkAuditEntry.Method)) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Domínio", Binding = new Binding(nameof(NetworkAuditEntry.Domain)) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "URL SHA-256", Binding = new Binding(nameof(NetworkAuditEntry.UrlSha256)) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Initiator", Binding = new Binding(nameof(NetworkAuditEntry.Initiator)) });
        root.Children.Add(_grid);

        Content = root;
        Loaded += (_, _) => Refresh();
    }

    private void Refresh() => _grid.ItemsSource = _audit.Snapshot();
}
