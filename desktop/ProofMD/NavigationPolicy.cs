namespace ProofMD;

internal enum NavigationDecision
{
    Allow,
    OpenExternally,
    Block,
}

/// <summary>
/// The viewer page is the only thing ever shown in the WebView; every other navigation is
/// either handed to the system (web and mail links) or dropped.
/// </summary>
internal static class NavigationPolicy
{
    public static NavigationDecision Decide(string? uri, string viewerEntryUri, bool isReloadOrHistory)
    {
        if (uri is null) return NavigationDecision.Block;
        if (uri.Equals(viewerEntryUri, StringComparison.OrdinalIgnoreCase))
        {
            // A reload would discard the open document, so only the first load is allowed.
            return isReloadOrHistory ? NavigationDecision.Block : NavigationDecision.Allow;
        }

        return CanOpenExternally(uri, viewerEntryUri)
            ? NavigationDecision.OpenExternally
            : NavigationDecision.Block;
    }

    /// <summary>
    /// Web and mail addresses go to the system; other addresses on the viewer host are
    /// relative links the page did not handle and lead nowhere.
    /// </summary>
    public static bool CanOpenExternally(string uri, string viewerEntryUri)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed) &&
            Uri.TryCreate(viewerEntryUri, UriKind.Absolute, out Uri? viewer) &&
            !parsed.Host.Equals(viewer.Host, StringComparison.OrdinalIgnoreCase) &&
            (parsed.Scheme == Uri.UriSchemeHttp ||
                parsed.Scheme == Uri.UriSchemeHttps ||
                parsed.Scheme == Uri.UriSchemeMailto);
    }
}
