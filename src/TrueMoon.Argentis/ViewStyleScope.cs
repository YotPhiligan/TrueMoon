namespace TrueMoon.Argentis;

internal sealed class ViewStyleScope(ViewStyleRule[] rules)
{
    private Theme? _theme;
    private Style[] _resolved = [];

    internal Style? Resolve(Element element, Theme theme)
    {
        if (!ReferenceEquals(_theme, theme))
        {
            var resolved = rules.Select(rule => rule.Resolve(theme)).ToArray();
            _resolved = resolved;
            _theme = theme;
        }

        Style? result = null;
        for (var i = 0; i < rules.Length; i++)
            if (rules[i].Matches(element))
                result = result == null ? _resolved[i] : result.Overlay(_resolved[i]);
        return result;
    }
}