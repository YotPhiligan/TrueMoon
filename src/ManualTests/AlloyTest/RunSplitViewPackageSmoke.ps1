param(
    [Parameter(Mandatory)][string]$Feed,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Za-z.+-]+$')][string]$PackageVersion
)
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$probeRoot = Join-Path $sourceRoot ('TestResults\SplitViewPackageConsumer\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $probeRoot | Out-Null
$probeFeed = [Security.SecurityElement]::Escape((Resolve-Path -LiteralPath $Feed).Path)
$probeProps = '<Project />'
Set-Content -LiteralPath (Join-Path $probeRoot 'Directory.Build.props') -Value $probeProps -Encoding utf8
Set-Content -LiteralPath (Join-Path $probeRoot 'Directory.Build.targets') -Value $probeProps -Encoding utf8
Set-Content -LiteralPath (Join-Path $probeRoot 'Directory.Packages.props') -Value $probeProps -Encoding utf8
Set-Content -LiteralPath (Join-Path $probeRoot 'NuGet.Config') -Encoding utf8 -Value "<configuration><packageSources><clear/><add key='split' value='$probeFeed'/></packageSources><packageSourceMapping><clear/><packageSource key='split'><package pattern='*'/></packageSource></packageSourceMapping></configuration>"
Set-Content -LiteralPath (Join-Path $probeRoot 'Probe.csproj') -Encoding utf8 -Value "<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><RootNamespace>SplitViewPackageProbe</RootNamespace><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include='TrueMoon.Alloy' Version='$PackageVersion'/></ItemGroup></Project>"
$probeProgram = @'
using SplitViewPackageProbe;
using TrueMoon.Argentis;
using TrueMoon.Alloy.Runtime;

var checks = 0;
foreach (var orientation in Enum.GetValues<SplitOrientation>())
{
    var editor = new TextBox { Value = "packaged draft" };
    var split = new SplitView(editor, new Text("second")) { Orientation = orientation, FirstPaneSize = 120 };
    using (var ui = new UiSession(split, new Surface(), new(400, 400)))
    {
        ui.Update();
        Check(split.ActualFirstPaneSize == 120, "preferred size");
        var b = split.Divider.Bounds;
        Check(ui.HandleInput(new(InputKind.PointerDown, X: b.X + 2, Y: b.Y + 2)).PointerCaptured, "capture");
        ui.HandleInput(orientation == SplitOrientation.Horizontal ? new(InputKind.PointerMove, X: b.X + 42, Y: -10) : new(InputKind.PointerMove, X: -10, Y: b.Y + 42));
        ui.Update(); Check(split.ActualFirstPaneSize == 160, "logical delta");
        Check(!ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)).PointerCaptured, "cancel release");
        ui.Update(); Check(split.ActualFirstPaneSize == 120, "rollback");
        ui.Focus(split.Divider); ui.HandleInput(new(InputKind.KeyDown, Key: orientation == SplitOrientation.Horizontal ? UiKey.Right : UiKey.Down));
        ui.Update(); Check(split.ActualFirstPaneSize == 128, "keyboard");
        ui.Focus(editor); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.SetTheme(Theme.Light); ui.Update();
        Check(editor.IsFocused && editor.SelectionLength == editor.Value.Length && editor.Value == "packaged draft", "retained state");
        Check(ReferenceEquals(editor, split.FirstPane.Child), "identity");
        Check(!ui.Update(), "idle");
    }
    Check(split.IsDisposed && editor.IsDisposed && !split.IsAttached && !split.IsResizing, "disposal");
}
Check(Path.GetDirectoryName(typeof(SplitView).Assembly.Location) == AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), "package assembly copy");
Console.WriteLine($"Fresh package consumer passed: {checks} checks; horizontal/vertical layout, capture, cancel, keyboard, retained state, idle/disposal.");
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }

namespace SplitViewPackageProbe {
sealed class Surface : IUiRenderSurface
{
    public ITextLayoutService TextLayout { get; } = new TextLayout();
    public void VerifyAvailable() { }
    public void Resize(UiViewport viewport) { }
    public void Render(Element root, UiViewport viewport) { }
    public void Dispose() { }
}
sealed class TextLayout : ITextLayoutService
{
    public Size Measure(string text, float size, string family) => new(text.Length * size / 2, size);
}

}
'@
Set-Content -LiteralPath (Join-Path $probeRoot 'Program.cs') -Encoding utf8 -Value $probeProgram
$oldProbeAppData = $env:APPDATA
try {
    $env:APPDATA = Join-Path $probeRoot 'settings'
    New-Item -ItemType Directory -Path $env:APPDATA | Out-Null
    & dotnet restore (Join-Path $probeRoot 'Probe.csproj') --configfile (Join-Path $probeRoot 'NuGet.Config') --packages (Join-Path $probeRoot 'packages') -p:NuGetAudit=false "-bl:$probeRoot/restore-{}.binlog" | Tee-Object (Join-Path $probeRoot 'restore.log')
    if ($LASTEXITCODE -ne 0) { throw 'Package consumer restore failed.' }
    foreach ($probeConfiguration in @('Debug', 'Release')) {
        & dotnet build (Join-Path $probeRoot 'Probe.csproj') --no-restore -c $probeConfiguration "-bl:$probeRoot/build-$probeConfiguration-{}.binlog" | Tee-Object (Join-Path $probeRoot "build-$probeConfiguration.log")
        if ($LASTEXITCODE -ne 0) { throw "Package consumer $probeConfiguration build failed." }
        & dotnet (Join-Path $probeRoot "bin/$probeConfiguration/net10.0/Probe.dll") | Tee-Object (Join-Path $probeRoot "run-$probeConfiguration.log")
        if ($LASTEXITCODE -ne 0) { throw "Package consumer $probeConfiguration checks failed." }
    }
    Write-Output "Independent SplitView package proof: $probeRoot"
}
finally { $env:APPDATA = $oldProbeAppData }