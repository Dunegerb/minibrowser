using CefSharp;
using CefSharp.Handler;

namespace MiniBrowser.Cef;

public sealed class MiniLifeSpanHandler : LifeSpanHandler
{
    private readonly Action<string> _openTab;
    private readonly Func<bool> _canOpenPopup;

    public MiniLifeSpanHandler(Action<string> openTab, Func<bool>? canOpenPopup = null)
    {
        _openTab = openTab;
        _canOpenPopup = canOpenPopup ?? (() => true);
    }

    protected override bool OnBeforePopup(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        string targetUrl,
        string targetFrameName,
        WindowOpenDisposition targetDisposition,
        bool userGesture,
        IPopupFeatures popupFeatures,
        IWindowInfo windowInfo,
        IBrowserSettings browserSettings,
        ref bool noJavascriptAccess,
        out IWebBrowser newBrowser)
    {
        newBrowser = null!;

        if (_canOpenPopup() && !string.IsNullOrWhiteSpace(targetUrl))
        {
            _openTab(targetUrl);
        }

        // Native popup is always cancelled. If capability is allowed, it is re-routed to a
        // trusted MiniBrowser tab in the same capsule; otherwise the popup simply dies here.
        return true;
    }
}
