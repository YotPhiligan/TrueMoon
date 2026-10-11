param(
    [string] $RunDirectory,
    [string] $PackageVersion
)

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$RunDirectory) {
    $RunDirectory = Join-Path $workspace ('TestResults/AlloyPackages/run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
}
$run = [IO.Path]::GetFullPath($RunDirectory)
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $workspace 'TestResults/AlloyPackages')) + [IO.Path]::DirectorySeparatorChar
if (!$run.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'RunDirectory must be inside src/TestResults/AlloyPackages.'
}
if (Test-Path -LiteralPath $run) { throw 'Use a new RunDirectory; previous evidence is never overwritten.' }
if (!$PackageVersion) {
    [xml] $buildProps = Get-Content -LiteralPath (Join-Path $workspace 'Directory.Build.props') -Raw
    $PackageVersion = $buildProps.SelectSingleNode('//Version').InnerText
    $PackageVersion += '.consumer.' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')
}
if ($PackageVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?-[0-9A-Za-z.-]+$') { throw 'Use a prerelease NuGet version.' }
$feed = Join-Path $run 'feed'
$consumer = Join-Path $run 'consumer'
$cache = Join-Path $run 'packages'
$logs = Join-Path $run 'logs'
New-Item -ItemType Directory -Path $feed, $consumer, $cache, $logs | Out-Null

function Invoke-DotNet([string] $Name, [string[]] $Arguments, [int] $TimeoutSeconds = 300) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.WorkingDirectory = $consumer
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            ($stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()) |
                Set-Content -LiteralPath (Join-Path $logs "$Name.log") -Encoding utf8
            throw "$Name timed out after $TimeoutSeconds seconds."
        }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        $output | Set-Content -LiteralPath (Join-Path $logs "$Name.log") -Encoding utf8
        if ($process.ExitCode -ne 0) { throw "$Name failed (exit $($process.ExitCode)); see $logs/$Name.log." }
        Write-Host "$Name passed"
    }
    finally { $process.Dispose() }
}

$projects = [Collections.Generic.List[string]]::new()
$visited = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
function Add-Project([string] $Path) {
    $path = [IO.Path]::GetFullPath($Path)
    if (!$visited.Add($path)) { return }
    if (!$path.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Package graph leaves workspace: $path"
    }
    [xml] $project = Get-Content -LiteralPath $path -Raw
    foreach ($reference in $project.SelectNodes('//ProjectReference')) {
        Add-Project (Join-Path (Split-Path $path) $reference.Include)
    }
    $projects.Add($path)
}

$summary = [ordered]@{ Completed = $false; Version = $PackageVersion; RunDirectory = $run; Failure = $null }
try {
    foreach ($name in @('TrueMoon.Alloy.Hosting', 'TrueMoon', 'TrueMoon.Extensions.DependencyInjection')) {
        Add-Project (Join-Path $workspace "$name/$name.csproj")
    }
    $inputs = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($project in $projects) {
        Get-ChildItem -LiteralPath (Split-Path $project) -Recurse -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs', '.csproj', '.props', '.targets', '.md') } |
            ForEach-Object { [void] $inputs.Add($_.FullName) }
    }
    Get-ChildItem -LiteralPath $PSScriptRoot -File | ForEach-Object { [void] $inputs.Add($_.FullName) }
    foreach ($path in @('Directory.Build.props', 'Directory.Packages.props', 'NuGet.Config', 'TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.build.json')) {
        [void] $inputs.Add((Join-Path $workspace $path))
    }
    $summary.SourceFiles = @($inputs | Sort-Object | ForEach-Object {
        @{ Path = [IO.Path]::GetRelativePath($workspace, $_); Sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
    })
    foreach ($project in $projects) {
        Invoke-DotNet ('pack-' + [IO.Path]::GetFileNameWithoutExtension($project)) @(
            'pack', $project, '--configuration', 'Release', "-p:Version=$PackageVersion", '--output', $feed, '--verbosity', 'quiet')
    }
    $rendererPackage = Get-ChildItem -LiteralPath $feed -Filter 'TrueMoon.Alloy.Rendering.Skia.*.nupkg'
    $archive = [IO.Compression.ZipFile]::OpenRead($rendererPackage.FullName)
    try {
        foreach ($entry in @('build/TrueMoon.Alloy.Rendering.Skia.targets', 'buildTransitive/TrueMoon.Alloy.Rendering.Skia.targets', 'nativeAssets/win-x64/libSkiaSharp.dll')) {
            if (!$archive.GetEntry($entry)) { throw "Renderer package is missing $entry" }
        }
    }
    finally { $archive.Dispose() }
    foreach ($name in @('AlloyPackageConsumer.csproj', 'Program.cs', 'ConsumerView.cs', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $consumer
    }
    # A unique version plus this isolated cache prevents reuse of previous TrueMoon artifacts.
    # Third-party dependencies may use the existing global cache as a read-only fallback.
    $fallback = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $feedXml = [Security.SecurityElement]::Escape($feed)
    $fallbackXml = [Security.SecurityElement]::Escape([IO.Path]::GetFullPath($fallback))
    @"
<configuration>
  <packageSources>
    <clear />
    <add key="truemoon-local" value="$feedXml" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="truemoon-local"><package pattern="TrueMoon" /><package pattern="TrueMoon.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
  <fallbackPackageFolders><clear /><add key="existing-third-party-cache" value="$fallbackXml" /></fallbackPackageFolders>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'NuGet.Config') -Encoding utf8
    $project = Join-Path $consumer 'AlloyPackageConsumer.csproj'
    $versionProperty = "-p:TrueMoonPackageVersion=$PackageVersion"
    Invoke-DotNet 'restore' @('restore', $project, '--configfile', (Join-Path $consumer 'NuGet.Config'), '--packages', $cache, $versionProperty, '--verbosity', 'quiet')
    $assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    $localLibraries = @($assets.libraries.Keys | Where-Object { $_ -match '^TrueMoon(?:\.|/)' })
    foreach ($library in $localLibraries) {
        $entry = $assets.libraries[$library]
        if ($entry.type -ne 'package' -or !$library.EndsWith('/' + $PackageVersion)) { throw "Unexpected TrueMoon library: $library" }
        $metadata = Join-Path (Join-Path $cache $entry.path) '.nupkg.metadata'
        if (!(Test-Path -LiteralPath $metadata)) { throw "TrueMoon package was not restored into isolated cache: $library" }
        $origin = Get-Content -LiteralPath $metadata -Raw | ConvertFrom-Json
        if ($origin.source.TrimEnd('\', '/') -ne $feed.TrimEnd('\', '/')) { throw "TrueMoon package came from another source: $library" }
    }
    if ($localLibraries.Count -lt 9) { throw 'Expected the complete transitive UI/App package graph.' }
    $summary.TrueMoonLibraries = $localLibraries
    $expectedNative = Get-FileHash -LiteralPath (Join-Path $workspace 'TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.dll') -Algorithm SHA256
    $cases = [Collections.Generic.List[object]]::new()
    foreach ($configuration in @('Debug', 'Release')) {
        Invoke-DotNet "build-$configuration" @('build', $project, '--configuration', $configuration, '--no-restore', $versionProperty, '--verbosity', 'quiet')
        $buildOutput = Join-Path $consumer "bin/$configuration/net10.0"
        $publishOutput = Join-Path $run "publish/$configuration"
        Invoke-DotNet "publish-$configuration" @('publish', $project, '--configuration', $configuration, '--no-restore', '--self-contained', 'false', '--output', $publishOutput, $versionProperty, '--verbosity', 'quiet')
        foreach ($delivery in @(@{ Name = 'build'; Path = $buildOutput }, @{ Name = 'publish'; Path = $publishOutput })) {
            $native = Join-Path $delivery.Path 'truemoon-native/win-x64/libSkiaSharp.dll'
            if (!(Test-Path -LiteralPath $native)) { throw "Missing transitive native asset: $native" }
            if ((Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash -ne $expectedNative.Hash) { throw "Native asset hash mismatch: $native" }
            foreach ($backend in @('raster', 'opengl', 'vulkan')) {
                $name = "$configuration-$($delivery.Name)-$backend"
                $report = Join-Path $run "$name.json"
                Invoke-DotNet $name @((Join-Path $delivery.Path 'AlloyPackageConsumer.dll'), $backend, $report) 45
                $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
                if (!$result.Completed) { throw "Consumer failed: $name" }
                $cases.Add(@{ Name = $name; Frames = $result.Frames; NativeSha256 = $expectedNative.Hash })
            }
        }
    }
    $summary.Cases = $cases.ToArray()
    # Verify the package's direct-reference build hook as well as the Hosting transitive route.
    $direct = Join-Path $run 'direct-renderer'
    New-Item -ItemType Directory -Path $direct | Out-Null
    foreach ($name in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'NuGet.Config')) {
        Copy-Item -LiteralPath (Join-Path $consumer $name) -Destination $direct
    }
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup><PackageReference Include="TrueMoon.Alloy.Rendering.Skia" Version="$PackageVersion" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $direct 'DirectRenderer.csproj') -Encoding utf8
    @'
using TrueMoon.Alloy;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using var ui = UiSession.Create(Text.Plain("Direct renderer package"), new SkiaRasterRenderBackend(), new RasterUiTarget(), new UiViewport(320, 100, 1));
ui.Update();
using var image = ui.SnapshotRasterImage();
if (image.Width != 320 || image.Height != 100) throw new InvalidOperationException("Direct renderer output dimensions failed.");
var expected = Path.Combine(AppContext.BaseDirectory, "truemoon-native", "win-x64", "libSkiaSharp.dll");
if (!string.Equals(SkiaNativeLibrary.LoadedPath, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Direct renderer loaded a different native DLL.");
Console.WriteLine("Direct renderer package: passed; raster and native delivery verified without Hosting/App.");
'@ | Set-Content -LiteralPath (Join-Path $direct 'Program.cs') -Encoding utf8
    $directProject = Join-Path $direct 'DirectRenderer.csproj'
    Invoke-DotNet 'restore-direct' @('restore', $directProject, '--configfile', (Join-Path $direct 'NuGet.Config'), '--packages', $cache, '--verbosity', 'quiet')
    $directAssets = Get-Content -LiteralPath (Join-Path $direct 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    if (@($directAssets.libraries.Keys | Where-Object { $_ -match '^TrueMoon(?:\.Alloy\.Hosting|\.Alloy\.Platform|\.Core|\.Contracts|\.Cobalt|/)' }).Count) {
        throw 'Direct renderer unexpectedly depends on App, Hosting or window platform.'
    }
    $directCases = [Collections.Generic.List[object]]::new()
    foreach ($configuration in @('Debug', 'Release')) {
        Invoke-DotNet "build-direct-$configuration" @('build', $directProject, '--configuration', $configuration, '--no-restore', '--verbosity', 'quiet')
        $buildOutput = Join-Path $direct "bin/$configuration/net10.0"
        $publishOutput = Join-Path $run "publish-direct/$configuration"
        Invoke-DotNet "publish-direct-$configuration" @('publish', $directProject, '--configuration', $configuration, '--no-restore', '--self-contained', 'false', '--output', $publishOutput, '--verbosity', 'quiet')
        foreach ($delivery in @(@{ Name = 'build'; Path = $buildOutput }, @{ Name = 'publish'; Path = $publishOutput })) {
            $native = Join-Path $delivery.Path 'truemoon-native/win-x64/libSkiaSharp.dll'
            if ((Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash -ne $expectedNative.Hash) { throw "Direct native asset hash mismatch: $native" }
            $name = "direct-$configuration-$($delivery.Name)"
            Invoke-DotNet $name @((Join-Path $delivery.Path 'DirectRenderer.dll')) 45
            $directCases.Add(@{ Name = $name; NativeSha256 = $expectedNative.Hash })
        }
    }
    $summary.DirectCases = $directCases.ToArray()
    $summary.Packages = @(Get-ChildItem -LiteralPath $feed -Filter '*.nupkg' | ForEach-Object {
        @{ Name = $_.Name; Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $summary.Sdk = (& dotnet --version).Trim()
    $summary.Completed = $true
    Write-Host "Package smoke completed: $run"
}
catch {
    $summary.Failure = $_.ToString()
    throw
}
finally {
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding utf8
}
