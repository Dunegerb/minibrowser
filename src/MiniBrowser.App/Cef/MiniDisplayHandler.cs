using CefSharp;
using CefSharp.Handler;

namespace MiniBrowser.Cef;

/// <summary>
/// Bridges CEF display-state callbacks into the trusted WPF shell.
/// HwndHost does not expose the same WPF AddressChanged/TitleChanged event surface as
/// CefSharp.Wpf, so address/title changes are observed through IDisplayHandler.
/// </summary>
public sealed class MiniDisplayHandler : DisplayHandler
{
    private readonly Action<string> _addressChanged;
    private readonly Action<string> _titleChanged;

    public MiniDisplayHandler(Action<string> addressChanged, Action<string> titleChanged)
    {
        _addressChanged = addressChanged ?? throw new ArgumentNullException(nameof(addressChanged));
        _titleChanged = titleChanged ?? throw new ArgumentNullException(nameof(titleChanged));
    }

    protected override void OnAddressChanged(IWebBrowser chromiumWebBrowser, AddressChangedEventArgs addressChangedArgs)
    {
        _addressChanged(addressChangedArgs.Address ?? string.Empty);
    }

    protected override void OnTitleChanged(IWebBrowser chromiumWebBrowser, TitleChangedEventArgs titleChangedArgs)
    {
        _titleChanged(titleChangedArgs.Title ?? string.Empty);
    }
}
