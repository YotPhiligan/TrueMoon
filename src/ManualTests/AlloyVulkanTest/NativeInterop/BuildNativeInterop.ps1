param(
    [Parameter(Mandatory)][string]$SkiaSharpSourcePath,
    [string]$VisualStudioPath = $env:VS_INSTALL,
    [string]$PythonPath,
    [string]$NinjaPath,
    [string]$WindowsSdkVersion,
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
if ($pin['DefaultMsvcToolset']) {
    # M156 selects the VS default toolset itself instead of consuming vcToolsetVersion.
    $defaultToolset = [IO.File]::ReadAllText((Join-Path $VisualStudioPath 'VC/Auxiliary/Build/Microsoft.VCToolsVersion.default.txt')).Trim()
    $selectedToolset = $spectre | Where-Object Name -eq $defaultToolset | Select-Object -First 1
    if (!$selectedToolset -or [version]$defaultToolset -lt [version]'14.50') {
        throw 'The pinned M156 recipe requires the VS default MSVC v145+ toolset with x64 Spectre libraries.'
    }
}
# This pinned Skia GN recipe appends one digit to the family prefix (14.4 -> 14.44).
$selectedToolsetVersion = [version]$selectedToolset.Name
$toolsetVersion = '{0}.{1}' -f $selectedToolsetVersion.Major, [int][Math]::Floor($selectedToolsetVersion.Minor / 10)
$windowsSdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
$completeSdks = @(Get-ChildItem -LiteralPath (Join-Path $windowsSdkRoot 'Include') -Directory -ErrorAction SilentlyContinue | Where-Object {
    (Test-Path -LiteralPath (Join-Path $_.FullName 'um/Windows.h')) -and
    (Test-Path -LiteralPath (Join-Path $_.FullName 'ucrt/stdlib.h')) -and
    (Test-Path -LiteralPath (Join-Path $windowsSdkRoot "Lib/$($_.Name)/um/x64/kernel32.lib")) -and
    (Test-Path -LiteralPath (Join-Path $windowsSdkRoot "Lib/$($_.Name)/ucrt/x64/ucrt.lib")) -and
    (Test-Path -LiteralPath (Join-Path $windowsSdkRoot "bin/$($_.Name)/x64/rc.exe"))
})
if (!$WindowsSdkVersion) { $WindowsSdkVersion = ($completeSdks | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).Name }
if (!$WindowsSdkVersion -or !($completeSdks | Where-Object Name -eq $WindowsSdkVersion)) {
    throw 'Supply an installed complete Windows x64 SDK with -WindowsSdkVersion; no SDK is installed automatically.'
}
Write-Output "Native toolchain: MSVC $($selectedToolset.Name), Windows SDK $WindowsSdkVersion"
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
    # SkiaSharp 4.154 removed the depot_tools gitlink; retain it only for older pins.
    $submodulePaths = @(& git config --file .gitmodules --get-regexp '\.path$' | ForEach-Object { ($_ -split '\s+', 2)[1] })
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the pinned submodule paths.' }
    $requiredSubmodules = @('externals/skia')
    if ($submodulePaths -contains 'externals/depot_tools') { $requiredSubmodules += 'externals/depot_tools' }
    & git submodule update --init --depth 1 -- $requiredSubmodules
    if ($LASTEXITCODE -ne 0) { throw 'Submodule initialization failed.' }
    & (Join-Path $PSScriptRoot 'ApplyNativeInterop.ps1') -SkiaSourcePath (Join-Path $root 'externals/skia')
    # The isolated upstream feeds must not inherit TrueMoon's package source mapping.
    $toolNugetConfig = Join-Path $root 'nuget.config'
    if (!(Test-Path -LiteralPath $toolNugetConfig)) { throw 'The pinned source is missing its NuGet configuration.' }
    & dotnet tool restore --configfile $toolNugetConfig
    if ($LASTEXITCODE -ne 0) { throw 'Cake tool restore failed.' }
    Push-Location (Join-Path $root 'native/windows')
    try {
        $nativeArguments = @('--target=libSkiaSharp', '--arch=x64', '--configuration=Release', '--llvm=msvc',
            "--vsinstall=$VisualStudioPath", "--python=$PythonPath", "--windowsSdkVersion=$WindowsSdkVersion",
            '--supportVulkan=true', '--supportDirect3D=false')
        if (!$pin['DefaultMsvcToolset']) { $nativeArguments += "--vcToolsetVersion=$toolsetVersion" }
        & dotnet cake @nativeArguments
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
            msvcVersion = $selectedToolset.Name; windowsSdkVersion = $WindowsSdkVersion
        }
        if ($pin['VulkanGeneralHostStage']) {
            $metadata.sourcePatches = @([ordered]@{
                id = 'vulkan-general-host-stage'
                sha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'vulkan-general-host-stage.patch') -Algorithm SHA256).Hash
            })
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
