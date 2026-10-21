[CmdletBinding()]
param([string] $RunDirectory, [string] $PackageVersion)

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $workspace 'TestResults/AlloyPackages')) + [IO.Path]::DirectorySeparatorChar
$run = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else {
    Join-Path $allowedRoot ('run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
}
if (!$run.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'RunDirectory must be inside src/TestResults/AlloyPackages.' }
if (Test-Path -LiteralPath $run) { throw 'Use a new RunDirectory; previous evidence is never overwritten.' }
if (!$PackageVersion) {
    [xml] $buildProps = Get-Content -LiteralPath (Join-Path $workspace 'Directory.Build.props') -Raw
    $PackageVersion = $buildProps.SelectSingleNode('//Version').InnerText + '.ui.' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')
}
if ($PackageVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?-[0-9A-Za-z.-]+$') { throw 'Use a prerelease NuGet version.' }
$feed = Join-Path $run 'feed'
$cache = Join-Path $run 'packages'
$logs = Join-Path $run 'logs'
foreach ($directory in @($run, $feed, $cache, $logs)) { [IO.Directory]::CreateDirectory($directory) | Out-Null }
$summary = [ordered]@{
    Completed = $false; Version = $PackageVersion; RunDirectory = $run
    TimestampUtc = [DateTime]::UtcNow.ToString('O'); Sdk = (& dotnet --version).Trim()
    OS = [Environment]::OSVersion.VersionString
    Architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    Failure = $null; Commands = @(); Cases = @(); DirectCases = @()
    Packages = @(); Libraries = @(); Generation = @(); SourceFiles = @(); Evidence = @()
    NegativeControl = $null; AnalyzerSha256 = $null
}
$versionProperty = "-p:TrueMoonPackageVersion=$PackageVersion"
$evidencePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$projects = [Collections.Generic.List[string]]::new()
$visited = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

function Fingerprint([string] $Path) {
    @{ Path = [IO.Path]::GetRelativePath($run, $Path); Sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
}
function Remember([string] $Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if ($evidencePaths.Add($full)) { $summary.Evidence += (Fingerprint $full) }
}
function Invoke-DotNet([string] $Name, [string[]] $Arguments, [int] $TimeoutSeconds = 180, [bool] $ExpectedFailure = $false) {
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory = $run
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $started = $false
    $timedOut = $false
    try {
        $started = $process.Start()
        if (!$started) { throw "Could not launch $Name." }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            $timedOut = $true
            $process.Kill($true)
            if (!$process.WaitForExit(5000)) { throw "Could not reap owned process $Name." }
        }
        if (!$stdout.Wait(5000) -or !$stderr.Wait(5000)) { throw "$Name output did not close." }
        $output = $stdout.GetAwaiter().GetResult()
        $errors = $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $logs "$Name.stdout.log"), $output)
        [IO.File]::WriteAllText((Join-Path $logs "$Name.stderr.log"), $errors)
        $summary.Commands += @{ Name = $Name; Arguments = $Arguments; ExitCode = $process.ExitCode; TimedOut = $timedOut; Pid = $process.Id }
        if ($timedOut) { throw "$Name timed out after $TimeoutSeconds seconds." }
        if ($ExpectedFailure) {
            if ($process.ExitCode -eq 0 -or ($output + $errors) -notmatch '(CS0234|CS0246).*RAlloyPackageConsumer') {
                throw 'Omitted Cobalt analyzer did not cause the expected generated-type compilation failure.'
            }
            $summary.NegativeControl = @{ Name = $Name; ExitCode = $process.ExitCode; Diagnostic = $Matches[1] }
        } elseif ($process.ExitCode -ne 0) { throw "$Name failed (exit $($process.ExitCode)); see $logs." }
        Write-Host "$Name passed"
        return $process.Id
    } finally {
        if ($started -and !$process.HasExited) {
            $process.Kill($true)
            if (!$process.WaitForExit(5000)) { throw "Owned process $Name could not be reaped." }
        }
        $process.Dispose()
    }
}
function Add-Project([string] $Path) {
    $path = [IO.Path]::GetFullPath($Path)
    if (!$path.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Package graph leaves workspace: $path" }
    if (!$visited.Add($path)) { return }
    [xml] $project = Get-Content -LiteralPath $path -Raw
    foreach ($reference in $project.SelectNodes('//ProjectReference')) { Add-Project (Join-Path (Split-Path $path) $reference.Include) }
    $projects.Add($path)
}
function Write-NuGetConfig([string] $Destination) {
    $fallback = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $feedXml = [Security.SecurityElement]::Escape($feed)
    $fallbackXml = [Security.SecurityElement]::Escape([IO.Path]::GetFullPath($fallback))
    @"
<configuration>
  <packageSources><clear /><add key="truemoon-local" value="$feedXml" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping>
    <packageSource key="truemoon-local"><package pattern="TrueMoon" /><package pattern="TrueMoon.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
  <fallbackPackageFolders><clear /><add key="existing-third-party-cache" value="$fallbackXml" /></fallbackPackageFolders>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $Destination 'NuGet.Config') -Encoding utf8
}
$fontNames = @('NotoSans-Regular.ttf', 'NotoSansSymbols-Regular.ttf', 'OFL.txt')
$fontSource = Join-Path $workspace 'ManualTests/AlloyTest/Assets/Fonts'
function Copy-Fonts([string] $Destination) {
    $fonts = Join-Path $Destination 'Assets/Fonts'
    [IO.Directory]::CreateDirectory($fonts) | Out-Null
    foreach ($name in $fontNames) { Copy-Item -LiteralPath (Join-Path $fontSource $name) -Destination $fonts }
    foreach ($name in $fontNames) { Remember (Join-Path $fonts $name) }
}
function Copy-Consumer([string] $Destination) {
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($name in @('AlloyPackageConsumer.csproj', 'Program.cs', 'ConsumerView.cs', 'ConsumerFonts.cs', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $Destination
        Remember (Join-Path $Destination $name)
    }
    Copy-Fonts $Destination
    Write-NuGetConfig $Destination
    Remember (Join-Path $Destination 'NuGet.Config')
}
function Verify-Assets([string] $Consumer, [bool] $Direct = $false) {
    $assetsPath = Join-Path $Consumer 'obj/project.assets.json'
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
    if (@($assets.libraries.Values | Where-Object type -ne 'package').Count) { throw 'Consumer has non-package dependencies.' }
    if (@($assets.libraries.Keys | Where-Object { $_ -match '^(Microsoft.CodeAnalysis|TrueMoon.Cobalt.Generator/)' }).Count) { throw 'Build-only Roslyn or standalone generator dependency.' }
    $local = @($assets.libraries.Keys | Where-Object { $_ -match '^TrueMoon(?:\.|/)' })
    $expected = if ($Direct) { 3 } else { 10 }
    if ($local.Count -ne $expected) { throw "Expected $expected local packages, got $($local.Count)." }
    foreach ($library in $local) {
        if (!$library.EndsWith('/' + $PackageVersion)) { throw "Unexpected TrueMoon version: $library" }
        $packageRoot = Join-Path $cache $assets.libraries[$library].path
        $metadata = Get-Content -LiteralPath (Join-Path $packageRoot '.nupkg.metadata') -Raw | ConvertFrom-Json
        if ($metadata.source.TrimEnd('\', '/') -ne $feed.TrimEnd('\', '/')) { throw "Wrong source: $library" }
        Remember (Join-Path $packageRoot '.nupkg.metadata')
    }
    if ($Direct -and @($local | Where-Object { $_ -match '^TrueMoon(?:\.Alloy\.Hosting|\.Alloy\.Platform|\.Core|\.Contracts|\.Cobalt|/)' }).Count) {
        throw 'Direct renderer depends on App/Hosting/platform.'
    }
    $summary.Libraries += @{ Consumer = [IO.Path]::GetRelativePath($run, $Consumer); Packages = $local }
    Remember $assetsPath
}
function Verify-Generation([string] $Consumer, [string] $Configuration, [bool] $Cobalt) {
    $list = Join-Path $Consumer "obj/$Configuration/net10.0/resolved-analyzers.txt"
    $analyzers = @(Get-Content -LiteralPath $list)
    $matching = @($analyzers | Where-Object { $_ -like '*TrueMoon.Cobalt.Generator.dll' })
    $files = @(Get-ChildItem -LiteralPath (Join-Path $Consumer "obj/generated/$Configuration") -Recurse -File -Filter '*.g.cs' -ErrorAction SilentlyContinue)
    if ($Cobalt) {
        $expected = Join-Path $cache ("truemoon.cobalt/$PackageVersion/analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll".ToLowerInvariant())
        if ($matching.Count -ne 1 -or !$matching[0].Equals($expected, [StringComparison]::OrdinalIgnoreCase)) { throw 'Compiler did not use the bundled package analyzer.' }
        if ((Get-FileHash -LiteralPath $expected).Hash -ne $summary.AnalyzerSha256) { throw 'Compiler analyzer hash differs from package.' }
        foreach ($name in @('CobaltResolversRegistration.g.cs', '*ConsumerModel*SingletonResolver.g.cs', '*ConsumerView*TransientResolver.g.cs')) {
            if (!@($files | Where-Object Name -like $name).Count) { throw "Missing consumer generated source $name." }
        }
        Remember $expected
    } elseif ($matching.Count -or @($files | Where-Object { $_.FullName -like '*TrueMoon.Cobalt*' }).Count) { throw 'DI-only consumer still contains generated Cobalt code/analyzer.' }
    Remember $list
    foreach ($file in $files) { Remember $file.FullName }
    $summary.Generation += @{ Consumer = [IO.Path]::GetRelativePath($run, $Consumer); Configuration = $Configuration; Analyzers = $analyzers; Sources = @($files | ForEach-Object { Fingerprint $_.FullName }) }
}
function Verify-Delivery([string] $Output) {
    foreach ($name in $fontNames) {
        $path = Join-Path $Output "Assets/Fonts/$name"
        if ((Get-FileHash -LiteralPath $path).Hash -ne $expectedFonts[$name]) { throw "Font/license delivery mismatch: $path" }
        Remember $path
    }
    $native = Join-Path $Output 'truemoon-native/win-x64/libSkiaSharp.dll'
    if ((Get-FileHash -LiteralPath $native).Hash -ne $expectedNative) { throw "Native delivery mismatch: $native" }
    Remember $native
    if (@(Get-ChildItem -LiteralPath $Output -Recurse -File | Where-Object { $_.Name -match '^(TrueMoon\..*\.Generator|Microsoft.CodeAnalysis).*\.dll$' }).Count) { throw 'Build-only analyzer in runtime output.' }
    foreach ($package in $summary.Packages) {
        $dll = Join-Path $Output "$($package.Id).dll"
        if (Test-Path -LiteralPath $dll) {
            if ((Get-FileHash -LiteralPath $dll).Hash -ne $package.RuntimeSha256) { throw "Runtime assembly differs from package: $dll" }
            Remember $dll
        }
    }
}
try {
    foreach ($name in @('TrueMoon.Alloy.Hosting', 'TrueMoon', 'TrueMoon.Extensions.DependencyInjection')) { Add-Project (Join-Path $workspace "$name/$name.csproj") }
    $inputs = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($project in $projects) {
        Get-ChildItem -LiteralPath (Split-Path $project) -Recurse -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
            ForEach-Object { [void] $inputs.Add($_.FullName) }
    }
    Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | ForEach-Object { [void] $inputs.Add($_.FullName) }
    foreach ($name in $fontNames) { [void] $inputs.Add((Join-Path $fontSource $name)) }
    foreach ($path in @('Directory.Build.props', 'Directory.Packages.props', 'NuGet.Config', 'global.json', '../README.md', '../LICENSE', '../materials/icon.png')) {
        [void] $inputs.Add([IO.Path]::GetFullPath((Join-Path $workspace $path)))
    }
    $summary.SourceFiles = @($inputs | Sort-Object | ForEach-Object { @{ Path = [IO.Path]::GetRelativePath($workspace, $_); Sha256 = (Get-FileHash -LiteralPath $_).Hash } })
    foreach ($project in $projects) {
        [xml] $packProject = Get-Content -LiteralPath $project -Raw
        if ($packProject.SelectSingleNode('//IsPackable')?.InnerText -eq 'false') { continue }
        Invoke-DotNet ('pack-' + [IO.Path]::GetFileNameWithoutExtension($project)) @('pack', $project, '--configuration', 'Release', "-p:Version=$PackageVersion", '--output', $feed, '--verbosity', 'quiet') | Out-Null
    }
    $packages = @(Get-ChildItem -LiteralPath $feed -Filter '*.nupkg')
    if ($packages.Count -ne 10) { throw "Expected 10 packages, got $($packages.Count)." }
    foreach ($package in $packages) {
        $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            $entry = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec') })[0]
            $reader = [IO.StreamReader]::new($entry.Open())
            try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $metadata = $nuspec.SelectSingleNode('//*[local-name()="metadata"]')
            foreach ($asset in @('LICENSE', 'icon.png', $metadata.readme)) {
                if (!$archive.GetEntry($asset)) { throw "Package $($metadata.id) missing $asset." }
            }
            foreach ($dependency in $nuspec.SelectNodes('//*[local-name()="dependency"]')) {
                if ($dependency.id -match '^TrueMoon(?:\.|$)' -and $dependency.version -notmatch [regex]::Escape($PackageVersion)) { throw 'Producer dependency version mismatch.' }
            }
            $runtime = $archive.GetEntry("lib/net10.0/$($metadata.id).dll")
            if (!$runtime) { throw "Missing runtime DLL in $($metadata.id)." }
            $stream = $runtime.Open()
            try { $runtimeHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
            if ($metadata.id -eq 'TrueMoon.Cobalt') {
                $analyzer = $archive.GetEntry('analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll')
                if (!$analyzer -or !$archive.GetEntry('buildTransitive/TrueMoon.Cobalt.targets')) { throw 'Unified Cobalt analyzer delivery missing.' }
                $stream = $analyzer.Open()
                try { $summary.AnalyzerSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
                $producer = Join-Path $workspace 'TrueMoon.Cobalt.Generator/bin/Release/net10.0/TrueMoon.Cobalt.Generator.dll'
                if ((Get-FileHash -LiteralPath $producer).Hash -ne $summary.AnalyzerSha256) { throw 'Producer/package analyzer mismatch.' }
            }
            if ($metadata.id -eq 'TrueMoon.Alloy.Rendering.Skia') {
                foreach ($name in @('build/TrueMoon.Alloy.Rendering.Skia.targets', 'buildTransitive/TrueMoon.Alloy.Rendering.Skia.targets', 'nativeAssets/win-x64/libSkiaSharp.dll')) {
                    if (!$archive.GetEntry($name)) { throw "Renderer package missing $name." }
                }
                $stream = $archive.GetEntry('nativeAssets/win-x64/libSkiaSharp.dll').Open()
                try { $packageNative = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
            }
            $producer = Join-Path $workspace "$($metadata.id)/bin/Release/net10.0/$($metadata.id).dll"
            if ((Get-FileHash -LiteralPath $producer).Hash -ne $runtimeHash) { throw 'Producer/package runtime mismatch.' }
            $summary.Packages += @{ Id = $metadata.id; Version = $metadata.version; Readme = $metadata.readme; Sha256 = (Get-FileHash -LiteralPath $package.FullName).Hash; RuntimeSha256 = $runtimeHash }
            Remember $package.FullName
        } finally { $archive.Dispose() }
    }
    $expectedNative = (Get-FileHash -LiteralPath (Join-Path $workspace 'TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.dll')).Hash
    if ($packageNative -ne $expectedNative) { throw 'Source/package native SHA mismatch.' }
    $expectedFonts = @{}
    foreach ($name in $fontNames) { $expectedFonts[$name] = (Get-FileHash -LiteralPath (Join-Path $fontSource $name)).Hash }
    foreach ($variant in @('generated', 'di-only')) {
        $cobalt = $variant -eq 'generated'
        $consumer = Join-Path $run $variant
        Copy-Consumer $consumer
        $project = Join-Path $consumer 'AlloyPackageConsumer.csproj'
        $properties = @($versionProperty)
        if (!$cobalt) { $properties += @('-p:EnableCobaltGenerator=false', '-p:RequireCobaltGeneration=false') }
        Invoke-DotNet "restore-$variant" (@('restore', $project, '--configfile', (Join-Path $consumer 'NuGet.Config'), '--packages', $cache, '--verbosity', 'quiet') + $properties) | Out-Null
        Verify-Assets $consumer
        foreach ($configuration in @('Debug', 'Release')) {
            Invoke-DotNet "build-$variant-$configuration" (@('build', $project, '--configuration', $configuration, '--no-restore', '--verbosity', 'quiet') + $properties) | Out-Null
            Verify-Generation $consumer $configuration $cobalt
            $publish = Join-Path $run "publish/$variant/$configuration"
            Invoke-DotNet "publish-$variant-$configuration" (@('publish', $project, '--configuration', $configuration, '--no-restore', '--self-contained', 'false', '--output', $publish, '--verbosity', 'quiet') + $properties) | Out-Null
            foreach ($delivery in @(@{ Name = 'build'; Path = Join-Path $consumer "bin/$configuration/net10.0" }, @{ Name = 'publish'; Path = $publish })) {
                Verify-Delivery $delivery.Path
                Remember (Join-Path $delivery.Path 'AlloyPackageConsumer.dll')
                $providers = if ($cobalt) { @('cobalt', 'di') } else { @('di') }
                foreach ($provider in $providers) {
                    foreach ($backend in @('raster', 'opengl', 'vulkan')) {
                        $name = "$variant-$configuration-$($delivery.Name)-$provider-$backend"
                        $report = Join-Path $run "$name.json"
                        $casePid = Invoke-DotNet $name @((Join-Path $delivery.Path 'AlloyPackageConsumer.dll'), $backend, $report, $provider) 45
                        $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
                        $expectedFrames = if ($backend -eq 'raster') { 6 } else { 20 }
                        if (!$result.Completed -or $result.Failure -or !$result.Cleanup -or $result.Backend -ne $backend -or $result.Provider -ne $provider -or $result.ProcessId -ne $casePid -or $result.ConsumerGeneration -ne $cobalt -or $result.Frames -lt $expectedFrames -or $result.ActiveSessions -ne 0 -or $result.Subscriptions -ne 0 -or $result.ModelDisposeCalls -ne 1) { throw "Invalid consumer result: $name" }
                        if (!$result.NativeSkia.Equals((Join-Path $delivery.Path 'truemoon-native/win-x64/libSkiaSharp.dll'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Wrong native LoadedPath.' }
                        foreach ($font in $fontNames) { if ($result.Fonts.$font -ne $expectedFonts[$font]) { throw 'Runtime font hash mismatch.' } }
                        $summary.Cases += @{ Name = $name; Report = $result }
                        Remember $report
                    }
                }
            }
        }
    }
    # Direct renderer: no App/Hosting, with the same explicit font snapshots.
    $direct = Join-Path $run 'direct-renderer'
    [IO.Directory]::CreateDirectory($direct) | Out-Null
    foreach ($name in @('Directory.Build.props', 'Directory.Packages.props', 'ConsumerFonts.cs')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $direct }
    '<Project />' | Set-Content -LiteralPath (Join-Path $direct 'Directory.Build.targets')
    Copy-Fonts $direct
    Write-NuGetConfig $direct
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup><PackageReference Include="TrueMoon.Alloy.Rendering.Skia" Version="$PackageVersion" /><Content Include="Assets/Fonts/*" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $direct 'DirectRenderer.csproj') -Encoding utf8
    @'
using AlloyPackageConsumer;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
var hashes = ConsumerFonts.Fingerprints();
var fonts = ConsumerFonts.Load();
var editor = new RichTextEditor { Text = "Direct renderer package" };
editor.Select(0, 6); editor.ApplyStyle(new RichTextStyle { Underline = true });
using var ui = UiSession.Create(editor, new SkiaRasterRenderBackend(fonts), new RasterUiTarget(), new UiViewport(320, 100, 1));
if (!ui.Update() || ui.Update()) throw new InvalidOperationException("Direct renderer update/static invalidation failed.");
using var image = ui.SnapshotRasterImage();
if (image.Width != 320 || image.Height != 100) throw new InvalidOperationException("Direct renderer output dimensions failed.");
using var bitmap = SkiaSharp.SKBitmap.FromImage(image);
if (bitmap.Pixels.Distinct().Count() < 2) throw new InvalidOperationException("Direct renderer did not draw text.");
var expected = Path.Combine(AppContext.BaseDirectory, "truemoon-native", "win-x64", "libSkiaSharp.dll");
if (!string.Equals(SkiaNativeLibrary.LoadedPath, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Direct renderer loaded a different native DLL.");
ui.Dispose();
if (!ui.Root.IsDisposed) throw new InvalidOperationException("Direct renderer retained its tree.");
Console.WriteLine("Direct renderer package: passed; fonts, raster, cleanup and native delivery verified without Hosting/App.");
'@ | Set-Content -LiteralPath (Join-Path $direct 'Program.cs') -Encoding utf8
    $directProject = Join-Path $direct 'DirectRenderer.csproj'
    Get-ChildItem -LiteralPath $direct -File | ForEach-Object { Remember $_.FullName }
    Invoke-DotNet 'restore-direct' @('restore', $directProject, '--configfile', (Join-Path $direct 'NuGet.Config'), '--packages', $cache, '--verbosity', 'quiet') | Out-Null
    Verify-Assets $direct $true
    foreach ($configuration in @('Debug', 'Release')) {
        Invoke-DotNet "build-direct-$configuration" @('build', $directProject, '--configuration', $configuration, '--no-restore', '--verbosity', 'quiet') | Out-Null
        $publish = Join-Path $run "publish-direct/$configuration"
        Invoke-DotNet "publish-direct-$configuration" @('publish', $directProject, '--configuration', $configuration, '--no-restore', '--self-contained', 'false', '--output', $publish, '--verbosity', 'quiet') | Out-Null
        foreach ($delivery in @(@{ Name = 'build'; Path = Join-Path $direct "bin/$configuration/net10.0" }, @{ Name = 'publish'; Path = $publish })) {
            Verify-Delivery $delivery.Path
            Remember (Join-Path $delivery.Path 'DirectRenderer.dll')
            $name = "direct-$configuration-$($delivery.Name)"
            Invoke-DotNet $name @((Join-Path $delivery.Path 'DirectRenderer.dll')) 45 | Out-Null
            $summary.DirectCases += @{ Name = $name; NativeSha256 = $expectedNative }
        }
    }
    $negative = Join-Path $run 'without-cobalt-analyzer'
    Copy-Consumer $negative
    $project = Join-Path $negative 'AlloyPackageConsumer.csproj'
    Invoke-DotNet 'restore-without-analyzer' @('restore', $project, '--configfile', (Join-Path $negative 'NuGet.Config'), '--packages', $cache, $versionProperty, '-p:EnableCobaltGenerator=false', '--verbosity', 'quiet') | Out-Null
    Invoke-DotNet 'build-without-analyzer' @('build', $project, '--no-restore', $versionProperty, '-p:EnableCobaltGenerator=false', '--verbosity', 'quiet') 180 $true | Out-Null
    if ($summary.Cases.Count -ne 36 -or $summary.DirectCases.Count -ne 4 -or !$summary.NegativeControl) { throw 'Incomplete package matrix.' }
    foreach ($source in $summary.SourceFiles) {
        if ((Get-FileHash -LiteralPath (Join-Path $workspace $source.Path)).Hash -ne $source.Sha256) { throw "Source changed: $($source.Path)" }
    }
    foreach ($evidence in $summary.Evidence) {
        if ((Get-FileHash -LiteralPath (Join-Path $run $evidence.Path)).Hash -ne $evidence.Sha256) { throw "Evidence changed: $($evidence.Path)" }
    }
    $summary.Completed = $true
    Write-Host "UI package smoke completed: 36 App + 4 direct cases and omitted-analyzer control. $run"
} catch { $summary.Failure = $_.ToString(); throw }
finally { $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding utf8 }
