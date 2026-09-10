using CefSharp;
using CefSharp.Wpf;
using MiniBrowser.Cef;
using MiniBrowser.Core;
using MiniBrowser.Core.Audit;
using MiniBrowser.Core.Machine;
using MiniBrowser.Core.Navigation;
using MiniBrowser.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MiniBrowser;

public partial class MainWindow : Window
{
    private readonly RuntimeServices _services;
    private readonly Dictionary<TabItem, BrowserTab> _tabs = new();
    private int _nextTabId;

    public MainWindow(RuntimeServices services)
    {
        _services = services;
        InitializeComponent();
        SessionText.Text = $"session {_services.Session.SessionId:N}";
        CreateTab("about:blank", select: true);
    }

    private BrowserTab? CurrentTab =>
        Tabs.SelectedItem is TabItem item && _tabs.TryGetValue(item, out var tab) ? tab : null;

    private ChromiumWebBrowser? CurrentBrowser => CurrentTab?.Browser;

    private BrowserTab CreateTab(string address, bool select)
    {
        var tabId = Interlocked.Increment(ref _nextTabId);
        var browser = new ChromiumWebBrowser(address)
        {
            RequestHandler = new MiniRequestHandler(_services.Session, tabId, _services.NetworkAudit, _services.MachineBridge),
            LifeSpanHandler = new MiniLifeSpanHandler(url => Dispatcher.BeginInvoke(() => CreateTab(url, select: true)))
        };

        var item = new TabItem { Header = "Nova aba", Content = browser };
        var tab = new BrowserTab(tabId, item, browser);
        _tabs[item] = tab;
        Tabs.Items.Add(item);

        browser.AddressChanged += (_, args) => Dispatcher.BeginInvoke(() =>
        {
            if (CurrentTab == tab && !Omnibox.IsKeyboardFocusWithin)
            {
                Omnibox.Text = args.NewValue as string ?? string.Empty;
            }
        });

        browser.TitleChanged += (_, args) => Dispatcher.BeginInvoke(() =>
        {
            var changedTitle = args.NewValue as string;
            var title = string.IsNullOrWhiteSpace(changedTitle) ? "Nova aba" : changedTitle;
            item.Header = title.Length > 28 ? title[..28] + "…" : title;
        });

        browser.LoadingStateChanged += (_, args) => Dispatcher.BeginInvoke(() =>
        {
            if (CurrentTab != tab) return;
            BackButton.IsEnabled = args.CanGoBack;
            ForwardButton.IsEnabled = args.CanGoForward;
            StatusText.Text = args.IsLoading ? "Carregando… | Machine: OFFLINE/STUB" : "Machine: OFFLINE/STUB";
        });

        if (select)
        {
            Tabs.SelectedItem = item;
            Omnibox.Text = address == "about:blank" ? string.Empty : address;
            Omnibox.Focus();
        }

        return tab;
    }

    private async Task NavigateFromOmniboxAsync()
    {
        var raw = Omnibox.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw) || CurrentTab is null) return;

        var parsed = OmniboxParser.Parse(raw);
        if (parsed.Kind == NavigationKind.Search)
        {
            var decision = await _services.MachineBridge.DecideAsync(
                MachineEventFactory.SearchBeforeSubmit(_services.Session, CurrentTab.TabId, parsed.SearchQuery!),
                CancellationToken.None);

            if (decision.Decision == MachineDecisionType.Block)
            {
                StatusText.Text = "Pesquisa bloqueada pela Machine";
                return;
            }
        }

        CurrentBrowser?.LoadUrl(parsed.TargetUrl);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentBrowser?.CanGoBack == true) CurrentBrowser.Back();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentBrowser?.CanGoForward == true) CurrentBrowser.Forward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => CurrentBrowser?.Reload();

    private async void Omnibox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await NavigateFromOmniboxAsync();
    }

    private void NewTab_Click(object sender, RoutedEventArgs e) => CreateTab("about:blank", select: true);

    private void CloseTab_Click(object sender, RoutedEventArgs e) => CloseCurrentTab();

    private void CloseCurrentTab()
    {
        if (CurrentTab is null) return;

        var tab = CurrentTab;
        _tabs.Remove(tab.Item);
        Tabs.Items.Remove(tab.Item);
        tab.Browser.Dispose();

        if (Tabs.Items.Count == 0)
        {
            CreateTab("about:blank", select: true);
        }
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrentBrowser is null) return;
        Omnibox.Text = CurrentBrowser.Address is "about:blank" or null ? string.Empty : CurrentBrowser.Address;
        BackButton.IsEnabled = CurrentBrowser.CanGoBack;
        ForwardButton.IsEnabled = CurrentBrowser.CanGoForward;
    }

    private void Audit_Click(object sender, RoutedEventArgs e)
    {
        var window = new AuditWindow(_services.NetworkAudit)
        {
            Owner = this
        };
        window.Show();
    }

    private async void Relapse_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentTab is null) return;

        var evt = MachineEventFactory.RelapseConfirmed(_services.Session, CurrentTab.TabId);
        await _services.MachineBridge.PublishAsync(evt, CancellationToken.None);
        var snapshot = await _services.FlightRecorder.PreserveRecentAsync("relapse", CancellationToken.None);
        StatusText.Text = $"RECAÍ registrado localmente | snapshot: {System.IO.Path.GetFileName(snapshot)}";
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.T && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            CreateTab("about:blank", select: true);
            e.Handled = true;
        }
        else if (e.Key == Key.W && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            CloseCurrentTab();
            e.Handled = true;
        }
        else if (e.Key == Key.L && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Omnibox.Focus();
            Omnibox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.F12)
        {
            CurrentBrowser?.ShowDevTools();
            e.Handled = true;
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        foreach (var tab in _tabs.Values.ToArray())
        {
            tab.Browser.Dispose();
        }
        _tabs.Clear();
    }
}
