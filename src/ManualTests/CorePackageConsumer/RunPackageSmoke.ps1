[CmdletBinding()]
param([string] $RunDirectory, [string] $PackageVersion)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $workspace 'TestResults/CorePackages')) + [IO.Path]::DirectorySeparatorChar
$run = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else {
    Join-Path $allowedRoot ('run-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
}
if (-not $run.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'RunDirectory must be inside this workspace TestResults/CorePackages.' }
if (Test-Path -LiteralPath $run) { throw 'Use a new RunDirectory; prior evidence is preserved.' }
if (-not $PackageVersion) {
    [xml] $props = Get-Content -LiteralPath (Join-Path $workspace 'Directory.Build.props') -Raw
    $PackageVersion = $props.SelectSingleNode('//Version').InnerText + '.headless.' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')
}
if ($PackageVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?-[0-9A-Za-z.-]+$') { throw 'Use a prerelease package version.' }
$feed = Join-Path $run 'feed'; $cache = Join-Path $run 'packages'; $logs = Join-Path $run 'logs'
New-Item -ItemType Directory -Path $run, $feed, $cache, $logs | Out-Null
$summary = [ordered]@{ Completed = $false; Version = $PackageVersion; RunDirectory = $run; Failure = $null; Cases = @(); NegativeControl = $null }
$versionProperty = "-p:TrueMoonPackageVersion=$PackageVersion"

function Invoke-DotNet([string] $Name, [string[]] $Arguments, [bool] $ExpectedFailure = $false, [int] $TimeoutSeconds = 300) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.WorkingDirectory = $run; $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start; $started = $false
    try {
        $started = $process.Start(); $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { $process.Kill($true); $process.WaitForExit(); throw "$Name timed out." }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        $output | Set-Content -LiteralPath (Join-Path $logs "$Name.log") -Encoding utf8
        if ($ExpectedFailure) {
            if ($process.ExitCode -eq 0 -or $output -notmatch '(CS0234|CS0246).*RHeadlessConsumer') { throw "$Name did not fail on missing consumer generation." }
            $summary.NegativeControl = @{ Name = $Name; ExitCode = $process.ExitCode; Diagnostic = $Matches[1]; MissingGeneratedNamespace = 'RHeadlessConsumer' }
        }
        elseif ($process.ExitCode -ne 0) { throw "$Name failed (exit $($process.ExitCode)); see $logs/$Name.log." }
        Write-Host "$Name passed"
    }
    finally { if ($started -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }; $process.Dispose() }
}

$projects = [Collections.Generic.List[string]]::new()
$visited = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
function Add-Project([string] $Path) {
    $path = [IO.Path]::GetFullPath($Path)
    if (-not $path.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Project leaves workspace: $path" }
    if (-not $visited.Add($path)) { return }
    [xml] $project = Get-Content -LiteralPath $path -Raw
    foreach ($reference in $project.SelectNodes('//ProjectReference')) { Add-Project (Join-Path (Split-Path $path) $reference.Include) }
    $projects.Add($path)
}

function Copy-Consumer([string] $Destination, [bool] $Direct = $false) {
    New-Item -ItemType Directory -Path $Destination | Out-Null
    foreach ($name in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $Destination
    }
    $source = if ($Direct) { Join-Path $PSScriptRoot 'DirectCobalt' } else { $PSScriptRoot }
    Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Extension -in @('.cs', '.csproj') } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination
    }
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

function Verify-Assets([string] $Consumer, [int] $ExpectedCount, [bool] $Direct = $false) {
    $assets = Get-Content -LiteralPath (Join-Path $Consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    if (@($assets.libraries.Keys | Where-Object { $_ -match '^(Microsoft.CodeAnalysis|TrueMoon.Cobalt.Generator/)' }).Count) { throw 'Consumer restored a build-only Roslyn or standalone Cobalt generator dependency.' }
    if (@($assets.libraries.Values | Where-Object { $_.type -ne 'package' }).Count) { throw 'Consumer has a non-package library.' }
    if (@($assets.libraries.Keys | Where-Object { $_ -match '(?i)^(Silk\.|Skia|Topten|TrueMoon\.(Alloy|Argentis|Enerit|Mithril))' }).Count) { throw 'Headless consumer unexpectedly has UI/IPC/units dependencies.' }
    $local = @($assets.libraries.Keys | Where-Object { $_ -match '^TrueMoon(?:\.|/)' })
    if ($local.Count -ne $ExpectedCount) { throw "Unexpected TrueMoon package count: $($local.Count)." }
    if ($Direct -and @($assets.libraries.Keys | Where-Object { $_ -match '^(TrueMoon/|TrueMoon.Extensions|Microsoft.Extensions)' }).Count) { throw 'Direct Cobalt consumer depends on App/adapter.' }
    foreach ($library in $local) {
        if (-not $library.EndsWith('/' + $PackageVersion)) { throw "Wrong package version: $library" }
        $metadataPath = Join-Path (Join-Path $cache $assets.libraries[$library].path) '.nupkg.metadata'
        $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
        if ($metadata.source.TrimEnd('\','/') -ne $feed.TrimEnd('\','/')) { throw "Wrong package source: $library" }
    }
    return $local
}

function Verify-Generation([string] $Consumer, [string] $Configuration, [bool] $Expected = $true) {
    $analyzers = @(Get-Content -LiteralPath (Join-Path $Consumer "obj/$Configuration/net10.0/resolved-analyzers.txt") | Where-Object { $_ -match 'TrueMoon.Cobalt.Generator.dll$' })
    if (-not $Expected) { if ($analyzers.Count) { throw 'Microsoft-only compilation still includes Cobalt generator.' }; return }
    $generator = Join-Path $cache ("truemoon.cobalt/$PackageVersion/analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll".ToLowerInvariant())
    if ($analyzers.Count -ne 1 -or -not $analyzers[0].Equals($generator, [StringComparison]::OrdinalIgnoreCase)) { throw 'Compiler did not consume the isolated generator package.' }
    if ((Get-FileHash -LiteralPath $generator).Hash -ne $summary.GeneratorSha256) { throw 'Analyzer package bytes differ.' }
    $generated = @(Get-ChildItem -LiteralPath (Join-Path $Consumer "obj/generated/$Configuration") -Recurse -File -Filter '*.g.cs')
    if (-not @($generated | Where-Object { $_.Name -eq 'CobaltResolversRegistration.g.cs' }).Count -or $generated.Count -lt 6) { throw 'Consumer generated sources incomplete.' }
    $summary.Generation += @{ Consumer = [IO.Path]::GetRelativePath($run, $Consumer); Configuration = $Configuration; Analyzer = $analyzers[0]; Sources = @($generated | ForEach-Object { @{ Path = [IO.Path]::GetRelativePath($run, $_.FullName); Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } }) }
}

try {
    Add-Project (Join-Path $workspace 'TrueMoon/TrueMoon.csproj')
    Add-Project (Join-Path $workspace 'TrueMoon.Extensions.DependencyInjection/TrueMoon.Extensions.DependencyInjection.csproj')
    $inputs = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($project in $projects) {
        Get-ChildItem -LiteralPath (Split-Path $project) -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.md','.props','.targets') } | ForEach-Object { [void]$inputs.Add($_.FullName) }
    }
    Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object { [void]$inputs.Add($_.FullName) }
    foreach ($name in @('Directory.Build.props','Directory.Packages.props','NuGet.Config','global.json')) { [void]$inputs.Add((Join-Path $workspace $name)) }
    $summary.SourceFiles = @($inputs | Sort-Object | ForEach-Object { @{ Path = [IO.Path]::GetRelativePath($workspace, $_); Sha256 = (Get-FileHash -LiteralPath $_).Hash } })
    foreach ($project in $projects) {
        [xml] $packProject = Get-Content -LiteralPath $project -Raw
        if ($packProject.SelectSingleNode('//IsPackable')?.InnerText -eq 'false') { continue }
        Invoke-DotNet ('pack-' + [IO.Path]::GetFileNameWithoutExtension($project)) @('pack', $project, '--configuration','Release', "-p:Version=$PackageVersion", '--output',$feed,'--verbosity','quiet')
    }
    foreach ($package in Get-ChildItem -LiteralPath $feed -Filter '*.nupkg') {
        $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            $entry = $archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec') }
            $reader = [IO.StreamReader]::new($entry.Open())
            try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $metadata = $nuspec.SelectSingleNode('//*[local-name()="metadata"]')
            foreach ($name in @('LICENSE','icon.png',$metadata.readme)) { if (-not $archive.GetEntry($name)) { throw "Missing metadata asset $name in $($package.Name)" } }
            if ($metadata.description -eq 'Package Description') { throw 'Placeholder package description.' }
            if ($metadata.id -eq 'TrueMoon.Cobalt') {
                $analyzer = $archive.GetEntry('analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll')
                if (-not $analyzer -or -not $archive.GetEntry('lib/net10.0/TrueMoon.Cobalt.dll') -or -not $archive.GetEntry('buildTransitive/TrueMoon.Cobalt.targets')) { throw 'Invalid unified Cobalt package layout.' }
                $stream = $analyzer.Open()
                try { $summary.GeneratorSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
            }
            if ($metadata.id -eq 'TrueMoon.Cobalt.Generator') { throw 'The generator must be bundled, not packed separately.' }
            if (@($nuspec.SelectNodes('//*[local-name()="dependency"]') | Where-Object { $_.id -match '^(TrueMoon.Cobalt.Generator|Microsoft.CodeAnalysis)' }).Count) { throw 'Runtime nuspec exposes a build-only dependency.' }
        } finally { $archive.Dispose() }
    }
    $summary.Generation = @(); $summary.Libraries = @()
    foreach ($variant in @('headless','direct-cobalt','microsoft-only')) {
        $consumer = Join-Path $run $variant; $direct = $variant -eq 'direct-cobalt'; $microsoftOnly = $variant -eq 'microsoft-only'
        Copy-Consumer $consumer $direct
        $project = Join-Path $consumer $(if ($direct) { 'DirectCobalt.csproj' } else { 'CorePackageConsumer.csproj' })
        $properties = @($versionProperty)
        if ($microsoftOnly) { $properties += @('-p:EnableCobaltGenerator=false','-p:RequireCobaltGeneration=false') }
        Invoke-DotNet "restore-$variant" (@('restore',$project,'--configfile',(Join-Path $consumer 'NuGet.Config'),'--packages',$cache,'--verbosity','quiet') + $properties)
        $expected = if ($direct) { 2 } else { 4 }
        $summary.Libraries += @{ Consumer = $variant; Packages = @(Verify-Assets $consumer $expected $direct) }
        foreach ($configuration in @('Debug','Release')) {
            Invoke-DotNet "build-$variant-$configuration" (@('build',$project,'--configuration',$configuration,'--no-restore','--verbosity','quiet') + $properties)
            Verify-Generation $consumer $configuration (-not $microsoftOnly)
            $publish = Join-Path $run "publish/$variant/$configuration"
            Invoke-DotNet "publish-$variant-$configuration" (@('publish',$project,'--configuration',$configuration,'--no-restore','--self-contained','false','--output',$publish,'--verbosity','quiet') + $properties)
            foreach ($delivery in @(@{ Name = 'build'; Path = (Join-Path $consumer "bin/$configuration/net10.0") }, @{ Name = 'publish'; Path = $publish })) {
                if (@(Get-ChildItem -LiteralPath $delivery.Path -Recurse -File | Where-Object { $_.Name -match '^(TrueMoon.Cobalt.Generator|Microsoft.CodeAnalysis|SkiaSharp|Silk\.).*\.dll$' }).Count) { throw 'Runtime output includes build-only analyzer or graphical dependencies.' }
                if ($direct) {
                    $name = "$variant-$configuration-$($delivery.Name)"; $reportPath = Join-Path $run "$name.json"
                    Invoke-DotNet $name @((Join-Path $delivery.Path 'DirectCobaltConsumer.dll'),$reportPath) $false 30
                    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
                    if (-not $report.Completed) { throw "$name report failed." }
                    $summary.Cases += @{ Name = $name; Report = [IO.Path]::GetRelativePath($run,$reportPath) }
                } else {
                    $providers = if ($microsoftOnly) { @('microsoft') } else { @('cobalt','microsoft') }
                    foreach ($provider in $providers) {
                        foreach ($scenario in @('normal','cancel','start-failure','stop-failure','build-failure','config-failure','cleanup-failure')) {
                            if ($scenario -eq 'cleanup-failure' -and $provider -ne 'cobalt') { continue }
                            $name = "$variant-$configuration-$($delivery.Name)-$provider-$scenario"; $reportPath = Join-Path $run "$name.json"
                            Invoke-DotNet $name @((Join-Path $delivery.Path 'HeadlessConsumer.dll'),$provider,$scenario,$reportPath) $false 30
                            $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
                            if (-not $report.Completed -or $report.Provider -ne $provider -or $report.Scenario -ne $scenario -or $report.Configuration -ne $configuration -or $report.ConsumerGeneration -ne (-not $microsoftOnly)) { throw "$name report incomplete." }
                            $summary.Cases += @{ Name = $name; Report = [IO.Path]::GetRelativePath($run,$reportPath) }
                        }
                    }
                }
            }
        }
    }
    $negative = Join-Path $run 'without-generator'; Copy-Consumer $negative
    $project = Join-Path $negative 'CorePackageConsumer.csproj'
    Invoke-DotNet 'restore-without-generator' @('restore',$project,'--configfile',(Join-Path $negative 'NuGet.Config'),'--packages',$cache,$versionProperty,'-p:EnableCobaltGenerator=false','--verbosity','quiet')
    Invoke-DotNet 'build-without-generator' @('build',$project,'--no-restore',$versionProperty,'-p:EnableCobaltGenerator=false','--verbosity','quiet') $true
    if ($summary.Cases.Count -ne 80) { throw 'Consumer matrix incomplete.' }
    # Compile and run the exact C# examples from the delivered package README files.
    $summary.ReadmeExamples = @()
    foreach ($id in @('TrueMoon','TrueMoon.Cobalt','TrueMoon.Extensions.DependencyInjection')) {
        $package = Get-ChildItem -LiteralPath $feed -Filter "$id.$PackageVersion.nupkg"
        $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            $reader = [IO.StreamReader]::new($archive.GetEntry('PACKAGE.md').Open())
            try { $readme = $reader.ReadToEnd() } finally { $reader.Dispose() }
        } finally { $archive.Dispose() }
        $snippets = [regex]::Matches($readme, '(?s)```csharp\r?\n(.*?)\r?\n```')
        if ($snippets.Count -ne 1) { throw "Expected one delivered C# example in $id." }
        $example = Join-Path $run "readme-$id"; Copy-Consumer $example
        # Replace only this owned scratch copy; the package examples are not hand-adapted.
        foreach ($file in Get-ChildItem -LiteralPath $example -File | Where-Object { $_.Extension -in @('.cs','.csproj') }) { Remove-Item -LiteralPath $file.FullName }
        $snippets[0].Groups[1].Value | Set-Content -LiteralPath (Join-Path $example 'Program.cs') -Encoding utf8
        $references = if ($id -eq 'TrueMoon.Cobalt') { '<PackageReference Include="TrueMoon.Cobalt" Version="$(TrueMoonPackageVersion)" />' } else { '<PackageReference Include="TrueMoon" Version="$(TrueMoonPackageVersion)" />' }
        if ($id -eq 'TrueMoon.Extensions.DependencyInjection') { $references += '<PackageReference Include="TrueMoon.Extensions.DependencyInjection" Version="$(TrueMoonPackageVersion)" />' }
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup>$references</ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $example 'ReadmeExample.csproj') -Encoding utf8
        $project = Join-Path $example 'ReadmeExample.csproj'
        Invoke-DotNet "restore-readme-$id" @('restore',$project,'--configfile',(Join-Path $example 'NuGet.Config'),'--packages',$cache,$versionProperty,'--verbosity','quiet')
        Invoke-DotNet "build-readme-$id" @('build',$project,'--configuration','Release','--no-restore',$versionProperty,'--verbosity','quiet')
        Invoke-DotNet "run-readme-$id" @((Join-Path $example 'bin/Release/net10.0/ReadmeExample.dll')) $false 30
        $summary.ReadmeExamples += @{ Package = $id; SourceSha256 = (Get-FileHash -LiteralPath (Join-Path $example 'Program.cs')).Hash; Log = "logs/run-readme-$id.log" }
    }
    $summary.Packages = @(Get-ChildItem -LiteralPath $feed -Filter '*.nupkg' | ForEach-Object { @{ Name = $_.Name; Sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
    $summary.SDK = (& dotnet --version).Trim()
    foreach ($source in $summary.SourceFiles) {
        if ((Get-FileHash -LiteralPath (Join-Path $workspace $source.Path)).Hash -ne $source.Sha256) {
            throw "Source changed during package proof: $($source.Path)"
        }
    }
    $summary.Completed = $true
    Write-Host "Core/Cobalt package smoke: 80 matrix + 3 delivered README processes passed; omitted-analyzer negative control passed. $run"
}
catch { $summary.Failure = $_.ToString(); throw }
finally { $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding utf8 }
