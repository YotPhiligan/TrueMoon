param(
    [Parameter(Mandatory)][string]$SkiaSharpSourcePath,
    [string]$VisualStudioPath = $env:VS_INSTALL,
    [string]$PythonPath,
    [string]$NinjaPath,
    [switch]$InstallToRenderer,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $SkiaSharpSourcePath).Path
$revision = & git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not read the SkiaSharp source revision.' }
$pins = Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot 'SourcePins.psd1')
$pin = $pins.Values | Where-Object { $_.SkiaSharpRevision -eq $revision } | Select-Object -First 1
if (!$pin) { throw "Unsupported SkiaSharp revision $revision. See SourcePins.psd1 for verified source pins." }
$skiaRevision = & git -C $root rev-parse HEAD:externals/skia
if ($LASTEXITCODE -ne 0 -or $skiaRevision -ne $pin.SkiaRevision) { throw 'The parent/submodule source pins disagree.' }
Write-Output "Source pin: SkiaSharp $($pin.Version) ($revision), Skia $skiaRevision"
if (!$VisualStudioPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $VisualStudioPath = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    }
}
if (!$VisualStudioPath -or !(Test-Path -LiteralPath (Join-Path $VisualStudioPath 'VC/Auxiliary/Build/vcvarsall.bat'))) {
    throw 'Native build blocked: MSVC Build Tools not found. Install C++ Build Tools, Windows SDK and x64 Spectre libraries, or supply -VisualStudioPath.'
}
$spectre = @(Get-ChildItem -LiteralPath (Join-Path $VisualStudioPath 'VC/Tools/MSVC') -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'lib/spectre/x64') })
if (!$spectre.Count) { throw 'Native build blocked: the pinned Windows recipe requires x64 MSVC Spectre libraries.' }
$selectedToolset = $spectre | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
# This pinned Skia GN recipe appends one digit to the family prefix (14.4 -> 14.44).
$selectedToolsetVersion = [version]$selectedToolset.Name
$toolsetVersion = '{0}.{1}' -f $selectedToolsetVersion.Major, [int][Math]::Floor($selectedToolsetVersion.Minor / 10)
if (!$PythonPath) {
    $command = Get-Command python3, python -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { $PythonPath = $command.Source }
}
if (!$PythonPath) { throw 'Supply -PythonPath pointing to a real Python 3 executable.' }
& $PythonPath --version
if ($LASTEXITCODE -ne 0) { throw 'Python cannot run. Windows Store aliases are not a Python installation.' }
# Some bundled launchers report a directory as sys.executable and fail in git-sync-deps.
& $PythonPath -c 'import subprocess, sys; subprocess.check_call([sys.executable, "--version"])'
if ($LASTEXITCODE -ne 0) { throw 'Python cannot launch itself. Supply the real interpreter and its required process-local PATH/PYTHONHOME.' }
if (!$NinjaPath) {
    $ninjaCommand = Get-Command ninja.exe -ErrorAction SilentlyContinue
    if ($ninjaCommand) { $NinjaPath = $ninjaCommand.Source }
}
if (!$NinjaPath) { throw 'Supply -NinjaPath pointing to a Ninja executable, or add Ninja to the process PATH.' }
$NinjaPath = (Resolve-Path -LiteralPath $NinjaPath).Path
& $NinjaPath --version
if ($LASTEXITCODE -ne 0) { throw 'Ninja cannot run.' }
if ($CheckOnly) {
    Write-Output 'Source revision, MSVC/Spectre, Python child-process and Ninja prerequisites verified. No files changed.'
    exit 0
}
$previousGitConfigCount = $env:GIT_CONFIG_COUNT
$gitConfigIndex = if ($previousGitConfigCount) { [int]$previousGitConfigCount } else { 0 }
$gitConfigKeyName = "GIT_CONFIG_KEY_$gitConfigIndex"
$gitConfigValueName = "GIT_CONFIG_VALUE_$gitConfigIndex"
$previousGitConfigKey = [Environment]::GetEnvironmentVariable($gitConfigKeyName)
$previousGitConfigValue = [Environment]::GetEnvironmentVariable($gitConfigValueName)
$previousPath = $env:PATH
$previousVsLanguage = $env:VSLANG
Push-Location $root
try {
    $env:PATH = (Split-Path -Parent $NinjaPath) + [IO.Path]::PathSeparator + $previousPath
    # Ninja parses English /showIncludes lines; localized MSVC output defeats that parser.
    $env:VSLANG = '1033'
    # Git children inherit this setting, including dependency clones with long font-test filenames.
    # Do not alter global Git configuration or the system's long-path policy.
    $env:GIT_CONFIG_COUNT = ($gitConfigIndex + 1).ToString()
    [Environment]::SetEnvironmentVariable($gitConfigKeyName, 'core.longpaths')
    [Environment]::SetEnvironmentVariable($gitConfigValueName, 'true')
    # Fetch source dependencies, not prebuilt natives: those cannot contain this extension.
    & git submodule update --init --depth 1 externals/skia externals/depot_tools
    if ($LASTEXITCODE -ne 0) { throw 'Submodule initialization failed.' }
    & (Join-Path $PSScriptRoot 'ApplyNativeInterop.ps1') -SkiaSourcePath (Join-Path $root 'externals/skia')
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Cake tool restore failed.' }
    Push-Location (Join-Path $root 'native/windows')
    try {
        & dotnet cake --target=libSkiaSharp --arch=x64 --configuration=Release --llvm=msvc "--vcToolsetVersion=$toolsetVersion" "--vsinstall=$VisualStudioPath" "--python=$PythonPath" --supportVulkan=true --supportDirect3D=false
        if ($LASTEXITCODE -ne 0) { throw 'Native Skia build failed; do not substitute packaged native assets.' }
    } finally { Pop-Location }
    $dll = Join-Path $root 'output/native/windows/x64/libSkiaSharp.dll'
    if (!(Test-Path -LiteralPath $dll)) { throw "Build did not produce $dll" }
    Write-Output "Patched native library: $dll"
    if ($InstallToRenderer) {
        $workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
        [xml]$packages = Get-Content -Raw -LiteralPath (Join-Path $workspace 'Directory.Packages.props')
        $managedVersion = ($packages.Project.ItemGroup.PackageVersion | Where-Object Include -eq 'SkiaSharp').Version
        if ($managedVersion -ne $pin.Version) {
            throw "Refusing to install SkiaSharp $($pin.Version): the renderer uses $managedVersion."
        }
        $destination = Join-Path $workspace 'TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64'
        $null = New-Item -ItemType Directory -Force -Path $destination
        Copy-Item -LiteralPath $dll -Destination (Join-Path $destination 'libSkiaSharp.dll') -Force
        $metadata = [ordered]@{
            skiaSharpVersion = $pin.Version; skiaSharpRevision = $pin.SkiaSharpRevision
            skiaRevision = $pin.SkiaRevision; interopAbi = 1; runtimeIdentifier = 'win-x64'
            sha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
        }
        $metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'libSkiaSharp.build.json') -Encoding utf8
        Write-Output "Installed renderer native asset: $destination"
    }
} finally {
    Pop-Location
    $env:GIT_CONFIG_COUNT = $previousGitConfigCount
    [Environment]::SetEnvironmentVariable($gitConfigKeyName, $previousGitConfigKey)
    [Environment]::SetEnvironmentVariable($gitConfigValueName, $previousGitConfigValue)
    $env:PATH = $previousPath
    $env:VSLANG = $previousVsLanguage
}
