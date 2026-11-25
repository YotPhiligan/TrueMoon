namespace TrueMoon.Argentis.Styling;

internal sealed class ViewStyleScope(ViewStyleRule[] rules)
{
    private Theme? _theme;
    private Style[] _resolved = [];

    internal void PrepareTheme(Theme theme)
    {
        if (ReferenceEquals(_theme, theme)) return;
        _resolved = rules.Select(rule => rule.Resolve(theme)).ToArray();
        _theme = theme;
    }

    internal Style? Resolve(Element element, Theme theme)
    {
        PrepareTheme(theme);

        Style? result = null;
        for (var i = 0; i < rules.Length; i++)
            if (rules[i].Matches(element))
                result = result == null ? _resolved[i] : result.Overlay(_resolved[i]);
        return result;
    }
}