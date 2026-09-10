namespace MiniBrowser.Core.Navigation;

public enum NavigationKind
{
    Url,
    Search
}

public sealed record NavigationTarget(NavigationKind Kind, string TargetUrl, string? SearchQuery = null);

public static class OmniboxParser
{
    private const string SearchEndpoint = "https://www.google.com/search?q=";

    public static NavigationTarget Parse(string raw)
    {
        var value = raw.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme is "http" or "https" or "about"))
        {
            return new NavigationTarget(NavigationKind.Url, absolute.ToString());
        }

        if (!value.Contains(' ') && value.Contains('.'))
        {
            var candidate = "https://" + value;
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var inferred))
            {
                return new NavigationTarget(NavigationKind.Url, inferred.ToString());
            }
        }

        return new NavigationTarget(
            NavigationKind.Search,
            SearchEndpoint + Uri.EscapeDataString(value),
            value);
    }
}
