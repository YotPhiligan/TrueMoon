[CmdletBinding()]
param([string]$RunDirectory, [string]$EvidenceDirectory)

# Materialize an independent solution; no window, graphics device or native calls.
$ErrorActionPreference = 'Stop'
$packageWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$packageRun = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else {
    Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ARCH2e-' + [Guid]::NewGuid().ToString('N'))
}
if ($packageRun -eq $packageWorkspace -or $packageRun.StartsWith($packageWorkspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'RunDirectory must be outside the checkout.'
}
if (Test-Path -LiteralPath $packageRun) { throw 'Use a fresh RunDirectory; existing evidence is preserved.' }
if (![OperatingSystem]::IsWindows() -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') { throw 'Windows x64 is required.' }
$packageEvidence = if ($EvidenceDirectory) { [IO.Path]::GetFullPath($EvidenceDirectory) } else {
    Join-Path $packageWorkspace ('TestResults/ARCH2e-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
if (Test-Path -LiteralPath $packageEvidence) { throw 'Use a fresh EvidenceDirectory.' }
$packageFeed = Join-Path $packageRun 'feed'; $packageCache = Join-Path $packageRun 'cache'
$packageConsumer = Join-Path $packageRun 'consumer'; $packageCwd = Join-Path $packageRun 'unrelated working directory'
foreach ($directory in @($packageRun, $packageEvidence, $packageFeed, $packageCache, $packageConsumer, $packageCwd)) {
    [void][IO.Directory]::CreateDirectory($directory)
}
$packageDotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
$packageVersion = '0.2.2.7-arch2e.' + (Get-Date -Format 'yyyyMMddHHmmss')
$packageIds = @('TrueMoon.Contracts', 'TrueMoon.Core', 'TrueMoon.Cobalt', 'TrueMoon',
    'TrueMoon.Extensions.DependencyInjection', 'TrueMoon.Enerit.Generator', 'TrueMoon.Enerit', 'TrueMoon.Mithril',
    'TrueMoon.Argentis', 'TrueMoon.Alloy', 'TrueMoon.Alloy.Rendering.Skia', 'TrueMoon.Alloy.Platform.Silk',
    'TrueMoon.Alloy.Hosting', 'TrueMoon.Aether', 'TrueMoon.Aether.Editor')
$packageReport = [ordered]@{
    Completed = $false; Stage = 'ARCH2e.1/2'; RunDirectory = $packageRun; Consumer = $packageConsumer
    Version = $packageVersion; Feed = $packageFeed; Cache = $packageCache; GpuRun = $false; WindowRun = $false
    Sdk = $null; Commands = @(); Packages = @(); Payloads = @(); Sources = @(); Graphs = @(); Resources = @(); Cases = @(); Failure = $null
}

function Save-PackageReport {
    $json = $packageReport | ConvertTo-Json -Depth 35
    [IO.File]::WriteAllText((Join-Path $packageEvidence 'verification.json'), $json)
}

function Invoke-PackageCommand([string]$name, [string[]]$arguments, [string]$cwd = $packageCwd,
    [int]$expectedExit = 0, [int]$timeoutSeconds = 240, [switch]$isolated) {
    $info = [Diagnostics.ProcessStartInfo]::new($packageDotnet)
    $info.WorkingDirectory = $cwd; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    [void]$info.Environment.Remove('TM_ENGINE_BOOTSTRAP')
    $info.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    if ($isolated) {
        $info.Environment['NUGET_PACKAGES'] = $packageCache
        $info.Environment['NUGET_HTTP_CACHE_PATH'] = Join-Path $packageRun 'http-cache'
        $info.Environment['NUGET_PLUGINS_CACHE_PATH'] = Join-Path $packageRun 'plugins-cache'
    }
    foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
    $timedOut = $false
    try {
        if (!$process.WaitForExit($timeoutSeconds * 1000)) { $timedOut = $true; $process.Kill($true); $process.WaitForExit() }
        $outText = $stdout.GetAwaiter().GetResult(); $errText = $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $packageEvidence "$name.stdout.log"), $outText)
        [IO.File]::WriteAllText((Join-Path $packageEvidence "$name.stderr.log"), $errText)
        $packageReport.Commands += [ordered]@{ Name = $name; FileName = $packageDotnet; Arguments = $arguments
            WorkingDirectory = $cwd; IsolatedCache = [bool]$isolated; ExitCode = $process.ExitCode
            ExpectedExitCode = $expectedExit; TimedOut = $timedOut }
        Save-PackageReport
        if ($timedOut -or $process.ExitCode -ne $expectedExit) { throw "$name failed (exit $($process.ExitCode), timeout $timedOut); see logs." }
        if ($arguments[0] -in @('restore', 'build', 'pack', 'publish') -and
            !(Get-ChildItem -LiteralPath $packageEvidence -Filter "$name-*.binlog")) { throw "$name did not save a binlog." }
        Write-Host "$name passed"
        return $outText + $errText
    }
    finally { if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
}

function Get-PackageHash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function Get-PackageZipHash([string]$path, [string]$entry) {
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $item = $zip.GetEntry($entry)
        if (!$item -or $item.Length -eq 0) { throw "Missing $entry in $path" }
        $stream = $item.Open()
        try { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
    } finally { $zip.Dispose() }
}
function Assert-PackageResource([string]$profile, [string]$path, [string]$expected) {
    $hash = Get-PackageHash $path
    if ($hash -ne $expected) { throw "Incorrect resource delivery: $path" }
    $packageReport.Resources += [ordered]@{ Profile = $profile; Path = $path; Sha256 = $hash; Bytes = (Get-Item -LiteralPath $path).Length }
}

try {
    $packageReport.Sdk = (Invoke-PackageCommand 'sdk' @('--version')).Trim()
    # Hash framework, fixtures and host-owned resources; no bin/obj or generated sources.
    $directories = @($packageIds + @('TrueMoon.Cobalt.Generator', 'ManualTests/AetherPackageConsumer'))
    $sourcePaths = @('Directory.Build.props', 'Directory.Packages.props', 'NuGet.Config',
        'ManualTests/Game.Logic/ProbeGame.cs', 'Examples/AetherEditor/Assets/Fonts/NotoSans-Regular.ttf',
        'Examples/AetherEditor/Assets/Fonts/OFL.txt') | ForEach-Object { Join-Path $packageWorkspace $_ }
    foreach ($relative in $directories) {
        $sourcePaths += Get-ChildItem -LiteralPath (Join-Path $packageWorkspace $relative) -File -Recurse |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs', '.csproj', '.props', '.targets', '.dll', '.spv', '.ps1', '.md') } |
            ForEach-Object FullName
    }
    foreach ($path in @($sourcePaths | Sort-Object -Unique)) {
        $packageReport.Sources += [ordered]@{ Path = $path; Sha256 = Get-PackageHash $path }
    }
    foreach ($id in $packageIds) {
        $name = "pack-$id"
        $null = Invoke-PackageCommand $name @('pack', "$id/$id.csproj", '-c', 'Release', '-o', $packageFeed,
            "-p:Version=$packageVersion", '--verbosity', 'quiet', "-bl:$packageEvidence/$name-{}.binlog") $packageWorkspace
        $path = Join-Path $packageFeed "$id.$packageVersion.nupkg"
        $zip = [IO.Compression.ZipFile]::OpenRead($path)
        try {
            $reader = [IO.StreamReader]::new($zip.GetEntry("$id.nuspec").Open())
            try { [xml]$packageMetadataXml = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($packageMetadataXml.package.metadata.id -ne $id -or $packageMetadataXml.package.metadata.version -ne $packageVersion) { throw 'Invalid package metadata.' }
            foreach ($entry in @('LICENSE', 'icon.png', $packageMetadataXml.package.metadata.readme)) {
                if (!$zip.GetEntry($entry)) { throw "Missing metadata resource $entry in $id" }
            }
            if (@($packageMetadataXml.SelectNodes('//*[local-name()="dependency"]') | Where-Object { $_.id -match 'Tests|Host$|Probe$|^Examples' }).Count) { throw 'Executable/test package dependency.' }
        } finally { $zip.Dispose() }
        $packageReport.Packages += [ordered]@{ Id = $id; Path = $path; Sha256 = Get-PackageHash $path }
    }
    Copy-Item -Path (Join-Path $PSScriptRoot 'Consumer/*') -Destination $packageConsumer -Recurse
    Copy-Item -LiteralPath (Join-Path $packageWorkspace 'ManualTests/Game.Logic/ProbeGame.cs') -Destination "$packageConsumer/Game/ProbeGame.cs"
    [void][IO.Directory]::CreateDirectory("$packageConsumer/Editor/Assets/Fonts")
    foreach ($name in @('NotoSans-Regular.ttf', 'OFL.txt')) {
        Copy-Item -LiteralPath (Join-Path $packageWorkspace "Examples/AetherEditor/Assets/Fonts/$name") -Destination "$packageConsumer/Editor/Assets/Fonts/$name"
    }
    $props = [IO.File]::ReadAllText("$packageConsumer/Directory.Build.props").Replace('</Project>',
        "<PropertyGroup><TrueMoonPackageVersion>$packageVersion</TrueMoonPackageVersion></PropertyGroup></Project>")
    [IO.File]::WriteAllText("$packageConsumer/Directory.Build.props", $props)
    $escapedFeed = [Security.SecurityElement]::Escape($packageFeed)
    [IO.File]::WriteAllText("$packageConsumer/NuGet.Config", @"
<configuration><packageSources><clear /><add key="fresh" value="$escapedFeed" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
<packageSourceMapping><packageSource key="fresh"><package pattern="TrueMoon" /><package pattern="TrueMoon.*" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping></configuration>
"@)
    [IO.File]::WriteAllText("$packageConsumer/Consumer.slnx", '<Solution><Project Path="Editor/Editor.csproj" /><Project Path="Runtime/Runtime.csproj" /><Project Path="Game/Game.csproj" /></Solution>')
    [IO.File]::WriteAllText("$packageConsumer/global.json", '{"sdk":{"version":"' + $packageReport.Sdk + '","rollForward":"disable"}}')
    $null = Invoke-PackageCommand 'consumer-restore' @('restore', 'Consumer.slnx', '--configfile', "$packageConsumer/NuGet.Config", '--no-http-cache',
        "-bl:$packageEvidence/consumer-restore-{}.binlog") $packageConsumer -isolated
    foreach ($project in @('Editor', 'Runtime', 'Game')) {
        [xml]$projectXml = Get-Content -LiteralPath "$packageConsumer/$project/$project.csproj" -Raw
        if ($projectXml.SelectNodes('//ProjectReference|//Compile[@Include]').Count) { throw 'Project/source reference in independent consumer.' }
        $assets = Get-Content -LiteralPath "$packageConsumer/$project/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        if (@($assets.libraries.Values | Where-Object type -ne 'package').Count) { throw 'Non-package dependency in consumer.' }
        if ($assets.packageFolders.Keys.Count -ne 1 -or [IO.Path]::GetFullPath(@($assets.packageFolders.Keys)[0]).TrimEnd('\', '/') -ne $packageCache.TrimEnd('\', '/')) { throw 'Consumer did not use isolated cache.' }
        $local = @($assets.libraries.Keys | Where-Object { $_ -match '^TrueMoon(?:\.[^/]+)?/' })
        foreach ($key in $local) {
            if (!$key.EndsWith('/' + $packageVersion, [StringComparison]::Ordinal)) { throw "Stale framework package: $key" }
            $metadata = Get-Content -LiteralPath (Join-Path $packageCache ($assets.libraries[$key].path + '/.nupkg.metadata')) -Raw | ConvertFrom-Json
            if ($metadata.source -ne $packageFeed) { throw "Wrong framework feed: $key" }
        }
        if ($project -eq 'Runtime' -and @($local | Where-Object { $_ -match '^TrueMoon\.(Aether.Editor|Alloy|Argentis)' }).Count) { throw 'Runtime references Editor/UI.' }
        $packageReport.Graphs += [ordered]@{ Project = $project; Packages = $local; Assets = "$packageConsumer/$project/obj/project.assets.json" }
    }
    $null = Invoke-PackageCommand 'consumer-build-Debug' @('build', 'Consumer.slnx', '-c', 'Debug', '--no-restore', '--verbosity', 'quiet',
        "-bl:$packageEvidence/consumer-build-Debug-{}.binlog") $packageConsumer -isolated
    foreach ($project in @('Editor', 'Runtime')) {
        $name = "consumer-publish-$project-Release"
        $null = Invoke-PackageCommand $name @('publish', "$project/$project.csproj", '-c', 'Release', '-o', "$packageRun/published/$project", '--no-restore',
            '--verbosity', 'quiet', "-bl:$packageEvidence/$name-{}.binlog") $packageConsumer -isolated
    }
    foreach ($configuration in @('Debug', 'Release')) {
        $generated = Get-ChildItem -LiteralPath "$packageConsumer/Editor/obj/generated/$configuration" -Filter '*.cs' -Recurse
        if (!@($generated | Where-Object FullName -like '*TrueMoon.Cobalt.Generator*').Count -or
            !@($generated | Where-Object FullName -like '*TrueMoon.Enerit.Generator*').Count) { throw 'Consumer analyzers did not produce sources.' }
    }
    foreach ($required in @(
        @('TrueMoon.Cobalt', 'analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll'),
        @('TrueMoon.Cobalt', 'buildTransitive/TrueMoon.Cobalt.targets'),
        @('TrueMoon.Enerit.Generator', 'analyzers/dotnet/cs/TrueMoon.Enerit.Generator.dll'),
        @('TrueMoon.Alloy.Rendering.Skia', 'buildTransitive/TrueMoon.Alloy.Rendering.Skia.targets')
    )) {
        $owner = @($packageReport.Packages | Where-Object Id -eq $required[0])[0]
        $packageReport.Payloads += [ordered]@{ Id = $required[0]; Entry = $required[1]; Sha256 = Get-PackageZipHash $owner.Path $required[1] }
    }
    $renderer = @($packageReport.Packages | Where-Object Id -eq 'TrueMoon.Alloy.Rendering.Skia')[0]
    $nativeHash = Get-PackageZipHash $renderer.Path 'nativeAssets/win-x64/libSkiaSharp.dll'
    $editorAssets = Get-Content -LiteralPath "$packageConsumer/Editor/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
    foreach ($profile in @('build-Debug', 'published-Release')) {
        $editor = if ($profile -eq 'build-Debug') { "$packageConsumer/Editor/bin/Debug/net10.0" } else { "$packageRun/published/Editor" }
        $runtime = if ($profile -eq 'build-Debug') { "$packageConsumer/Runtime/bin/Debug/net10.0" } else { "$packageRun/published/Runtime" }
        foreach ($id in @('TrueMoon', 'TrueMoon.Aether', 'TrueMoon.Aether.Editor', 'TrueMoon.Argentis', 'TrueMoon.Alloy')) {
            $package = @($packageReport.Packages | Where-Object Id -eq $id)[0]
            Assert-PackageResource $profile "$editor/$id.dll" (Get-PackageZipHash $package.Path "lib/net10.0/$id.dll")
        }
        Assert-PackageResource $profile "$editor/truemoon-native/win-x64/libSkiaSharp.dll" $nativeHash
        $nativeCount = 0
        foreach ($target in $editorAssets.targets.Values) { foreach ($library in $target.GetEnumerator()) {
            if (!$library.Value.runtimeTargets) { continue }
            foreach ($asset in @($library.Value.runtimeTargets.GetEnumerator() | Where-Object { $_.Value.assetType -eq 'native' -and $_.Value.rid -eq 'win-x64' -and $_.Key.EndsWith('.dll', [StringComparison]::OrdinalIgnoreCase) })) {
                $installed = Join-Path $packageCache ($editorAssets.libraries[$library.Key].path + '/' + $asset.Key)
                Assert-PackageResource $profile "$editor/$($asset.Key)" (Get-PackageHash $installed)
                $nativeCount++
            }
        } }
        if ($nativeCount -lt 3) { throw 'Missing stock native dependencies.' }
        foreach ($name in @('NotoSans-Regular.ttf', 'OFL.txt')) {
            Assert-PackageResource $profile "$editor/Assets/Fonts/$name" (Get-PackageHash "$packageConsumer/Editor/Assets/Fonts/$name")
        }
        foreach ($extension in @('dll', 'exe')) {
            foreach ($provider in @('cobalt', 'di')) {
                $name = "$profile-$extension-$provider"; $report = "$packageRun/cases/$name"
                $null = Invoke-PackageCommand $name @("$editor/Editor.dll", "$runtime/Runtime.$extension", "$packageConsumer/Game/Game.csproj", $report, $provider) -isolated
                $result = Get-Content -LiteralPath "$report/report.json" -Raw | ConvertFrom-Json
                if (!$result.Passed -or $result.GameLoadedInParent -or $result.Runtime.Children.Count -ne 3) { throw 'Incomplete runtime proof.' }
                foreach ($shader in $result.Runtime.Shaders) {
                    $file = $shader.Name.Replace('TrueMoon.Aether.Gpu.Vulkan.Shaders.', '')
                    if ($shader.Sha256 -ne (Get-PackageHash (Join-Path $packageWorkspace "TrueMoon.Aether/Gpu/Vulkan/Shaders/$file"))) { throw 'Embedded shader differs from source.' }
                }
                foreach ($assembly in @($result.Assemblies | Where-Object Name -match '^TrueMoon(?:\.|$)')) {
                    $owner = @($packageReport.Packages | Where-Object Id -eq $assembly.Name)
                    if ($owner.Count -ne 1) { throw "Untracked loaded framework assembly: $($assembly.Name)" }
                    if (![IO.Path]::GetFullPath($assembly.Path).StartsWith([IO.Path]::GetFullPath($editor) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Loaded framework assembly outside consumer deployment.' }
                    if ((Get-PackageHash $assembly.Path) -ne (Get-PackageZipHash $owner[0].Path "lib/net10.0/$($assembly.Name).dll")) { throw 'Loaded assembly differs from fresh package.' }
                }
                $packageReport.Cases += [ordered]@{ Profile = $profile; Extension = $extension; Provider = $provider; Report = "$report/report.json" }
            }
        }
        $text = Invoke-PackageCommand "$profile-no-bootstrap" @("$runtime/Runtime.dll") -expectedExit 1 -isolated
        if (!$text.Contains('Missing or oversized runtime bootstrap')) { throw 'Unexpected bootstrap rejection.' }
    }
    foreach ($entry in @($packageReport.Sources + $packageReport.Packages)) { if ((Get-PackageHash $entry.Path) -ne $entry.Sha256) { throw "Input changed: $($entry.Path)" } }
    $packageReport.Completed = $true
}
catch { $packageReport.Failure = $_.ToString(); throw }
finally { Save-PackageReport; Write-Host "Evidence: $packageEvidence; independent solution: $packageConsumer" }
