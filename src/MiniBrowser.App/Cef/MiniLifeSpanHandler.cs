using CefSharp;
using CefSharp.Handler;

namespace MiniBrowser.Cef;

public sealed class MiniLifeSpanHandler : LifeSpanHandler
{
    private readonly Action<string> _openTab;

    public MiniLifeSpanHandler(Action<string> openTab)
    {
        _openTab = openTab;
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

        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            _openTab(targetUrl);
        }

        // Cancel native popup: navigation is re-routed to a MiniBrowser tab.
        return true;
    }
}
