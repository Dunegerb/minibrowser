using CefSharp.Wpf.HwndHost;
using MiniBrowser.Core.Capsules;
using System.Windows.Controls;

namespace MiniBrowser.Models;

public sealed record BrowserTab(
    int TabId,
    TabItem Item,
    ChromiumWebBrowser Browser,
    CapsuleDescriptor Capsule);
