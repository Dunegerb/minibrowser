using CefSharp.Wpf;
using System.Windows.Controls;

namespace MiniBrowser.Models;

public sealed record BrowserTab(int TabId, TabItem Item, ChromiumWebBrowser Browser);
