using CefSharp;
using CefSharp.Wpf.HwndHost;
using MiniBrowser.Cef;
using MiniBrowser.Core;
using MiniBrowser.Core.Audit;
using MiniBrowser.Core.Capsules;
using MiniBrowser.Core.History;
using MiniBrowser.Core.Machine;
using MiniBrowser.Core.Navigation;
using MiniBrowser.Core.Security;
using MiniBrowser.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MiniBrowser;

public partial class MainWindow : Window
{
    private readonly RuntimeServices _services;
    private readonly Dictionary<TabItem, BrowserTab> _tabs = new();
    private readonly Dictionary<int, string> _titles = new();
    private int _nextTabId;
    private bool _syncingCapsulePicker;

    public MainWindow(RuntimeServices services)
    {
        _services = services;
        InitializeComponent();
        ConfigureCapsulePicker();
        CreateTab("about:blank", select: true, _services.Capsules.Anonymous);
    }

    private BrowserTab? CurrentTab =>
        Tabs.SelectedItem is TabItem item && _tabs.TryGetValue(item, out var tab) ? tab : null;

    private ChromiumWebBrowser? CurrentBrowser => CurrentTab?.Browser;

    private void ConfigureCapsulePicker()
    {
        _syncingCapsulePicker = true;
        CapsulePicker.ItemsSource = _services.Capsules.AvailableCapsules;
        CapsulePicker.SelectedItem = _services.Capsules.Anonymous;
        _syncingCapsulePicker = false;
    }

    private BrowserTab CreateTab(string address, bool select, CapsuleDescriptor? capsule = null)
    {
        capsule ??= CurrentTab?.Capsule ?? _services.Capsules.Anonymous;

        var tabId = Interlocked.Increment(ref _nextTabId);
        var requestContext = _services.Capsules.GetRequestContext(capsule);
        var browser = new ChromiumWebBrowser(address)
        {
            // Must be assigned before HwndHost creates the underlying CEF browser.
            RequestContext = requestContext,
            RequestHandler = new MiniRequestHandler(
                _services.Session,
                tabId,
                capsule,
                _services.NetworkAudit,
                _services.MachineBridge,
                _services.NetworkPolicy),
            LifeSpanHandler = new MiniLifeSpanHandler(
                url => Dispatcher.BeginInvoke(() => CreateTab(url, select: true, capsule)),
                () => _services.Capabilities.IsAllowed(capsule, BrowserCapability.Popups))
        };

        var item = new TabItem { Header = $"{CapsulePrefix(capsule)} Nova aba" };
        var tab = new BrowserTab(tabId, item, browser, capsule);
        _tabs[item] = tab;
        _titles[tabId] = "Nova aba";
        Tabs.Items.Add(item);

        browser.AddressChanged += (_, args) => Dispatcher.BeginInvoke(() =>
        {
            if (CurrentTab == tab && !Omnibox.IsKeyboardFocusWithin)
            {
                Omnibox.Text = args.Address ?? string.Empty;
            }
        });

        browser.TitleChanged += (_, args) => Dispatcher.BeginInvoke(() =>
        {
            var title = string.IsNullOrWhiteSpace(args.Title) ? "Nova aba" : args.Title;
            _titles[tabId] = title;
            var shortTitle = title.Length > 22 ? title[..22] + "…" : title;
            item.Header = $"{CapsulePrefix(capsule)} {shortTitle}";
        });

        browser.LoadingStateChanged += (_, args) => Dispatcher.BeginInvoke(() =>
        {
            if (!args.IsLoading)
            {
                _services.History.Record(
                    tabId,
                    capsule.Id,
                    capsule.Name,
                    _titles.GetValueOrDefault(tabId),
                    browser.Address);
            }

            if (CurrentTab != tab) return;
            BackButton.IsEnabled = args.CanGoBack;
            ForwardButton.IsEnabled = args.CanGoForward;
            StatusText.Text = args.IsLoading
                ? $"Carregando em {capsule.Name}…"
                : "Machine: OFFLINE/STUB  •  NetworkPolicy: ACTIVE";
        });

        if (select)
        {
            Tabs.SelectedItem = item;
            BrowserHost.Content = browser;
            Omnibox.Text = address == "about:blank" ? string.Empty : address;
            Omnibox.Focus();
            UpdateCapsuleUi(tab);
        }

        return tab;
    }

    private static string CapsulePrefix(CapsuleDescriptor capsule) =>
        capsule.Kind == CapsuleKind.Anonymous ? "⚡" : "🔐";

    private void UpdateCapsuleUi(BrowserTab tab)
    {
        _syncingCapsulePicker = true;
        CapsulePicker.SelectedItem = tab.Capsule;
        _syncingCapsulePicker = false;
        SessionText.Text = $"{tab.Capsule.Name}  •  {tab.Capsule.StorageLabel}  •  Protocol v{MachineProtocol.CurrentVersion}";
    }

    private async Task NavigateFromOmniboxAsync()
    {
        var raw = Omnibox.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw) || CurrentTab is null) return;

        var parsed = OmniboxParser.Parse(raw);
        if (parsed.Kind == NavigationKind.Search)
        {
            var decision = await _services.MachineBridge.DecideAsync(
                MachineEventFactory.SearchBeforeSubmit(
                    _services.Session,
                    CurrentTab.TabId,
                    CurrentTab.Capsule.Id,
                    parsed.SearchQuery!),
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

    private void NewTab_Click(object sender, RoutedEventArgs e) =>
        CreateTab("about:blank", select: true, CurrentTab?.Capsule ?? _services.Capsules.Anonymous);

    private void CapsulePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingCapsulePicker || CapsulePicker.SelectedItem is not CapsuleDescriptor capsule) return;
        if (CurrentTab?.Capsule.Id == capsule.Id) return;

        // RequestContext cannot be swapped after browser creation. Switching identity therefore
        // creates a new tab in the selected capsule instead of mutating an existing renderer.
        CreateTab("about:blank", select: true, capsule);
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e) => CloseCurrentTab();

    private void CloseCurrentTab()
    {
        if (CurrentTab is null) return;

        var tab = CurrentTab;
        if (ReferenceEquals(BrowserHost.Content, tab.Browser)) BrowserHost.Content = null;
        _tabs.Remove(tab.Item);
        _titles.Remove(tab.TabId);
        Tabs.Items.Remove(tab.Item);
        tab.Browser.Dispose();

        if (Tabs.Items.Count == 0)
        {
            CreateTab("about:blank", select: true, _services.Capsules.Anonymous);
        }
        else if (Tabs.SelectedItem is TabItem selected && _tabs.TryGetValue(selected, out var selectedTab))
        {
            BrowserHost.Content = selectedTab.Browser;
            UpdateCapsuleUi(selectedTab);
        }
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrentTab is not { } tab) return;
        BrowserHost.Content = tab.Browser;
        Omnibox.Text = tab.Browser.Address is "about:blank" or null ? string.Empty : tab.Browser.Address;
        BackButton.IsEnabled = tab.Browser.CanGoBack;
        ForwardButton.IsEnabled = tab.Browser.CanGoForward;
        UpdateCapsuleUi(tab);
    }

    private void History_Click(object sender, RoutedEventArgs e)
    {
        var window = new HistoryWindow(_services.History, url => CurrentBrowser?.LoadUrl(url))
        {
            Owner = this
        };
        window.Show();
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentTab is null) return;

        try
        {
            _services.Capsules.ClearHttpCache(CurrentTab.Capsule);
            StatusText.Text = $"Cache HTTP de {CurrentTab.Capsule.Name} limpo; cookies/storage não foram apagados.";
        }
        catch
        {
            StatusText.Text = "Não foi possível limpar o cache da cápsula agora.";
        }
    }

    private void Audit_Click(object sender, RoutedEventArgs e)
    {
        var window = new AuditWindow(_services.NetworkAudit) { Owner = this };
        window.Show();
    }

    private async void Relapse_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentTab is null) return;

        var evt = MachineEventFactory.RelapseConfirmed(
            _services.Session,
            CurrentTab.TabId,
            CurrentTab.Capsule.Id);
        await _services.MachineBridge.PublishAsync(evt, CancellationToken.None);
        var snapshot = await _services.FlightRecorder.PreserveRecentAsync("relapse", CancellationToken.None);
        StatusText.Text = $"RECAÍ registrado localmente | {System.IO.Path.GetFileName(snapshot)}";
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (e.Key == Key.T && ctrl && !shift)
        {
            CreateTab("about:blank", select: true, CurrentTab?.Capsule ?? _services.Capsules.Anonymous);
            e.Handled = true;
        }
        else if (e.Key == Key.N && ctrl && shift)
        {
            CreateTab("about:blank", select: true, _services.Capsules.Anonymous);
            e.Handled = true;
        }
        else if (e.Key == Key.P && ctrl && shift)
        {
            CreateTab("about:blank", select: true, _services.Capsules.Personal);
            e.Handled = true;
        }
        else if (e.Key == Key.W && ctrl)
        {
            CloseCurrentTab();
            e.Handled = true;
        }
        else if (e.Key == Key.H && ctrl)
        {
            History_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.L && ctrl)
        {
            Omnibox.Focus();
            Omnibox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.F12 && CurrentTab is { } tab)
        {
            if (_services.Capabilities.IsAllowed(tab.Capsule, BrowserCapability.DevTools))
            {
                CurrentBrowser?.ShowDevTools();
            }
            e.Handled = true;
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        BrowserHost.Content = null;
        foreach (var tab in _tabs.Values.ToArray()) tab.Browser.Dispose();
        _tabs.Clear();
    }
}
