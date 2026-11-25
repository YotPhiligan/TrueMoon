using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis;

namespace AlloyTest;

// A small custom-painted widget reusing the normalized progress property and theme.
internal sealed class VolumeMeter : ProgressBar
{
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(200, 36);
}
