[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$run = Join-Path $workspace ('TestResults/CobaltGeneratorFixes/packages-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff'))
$feed = Join-Path $run 'feed'
$cache = Join-Path $run 'packages'
$consumer = Join-Path $run 'consumer'
[void][IO.Directory]::CreateDirectory($feed)
[void][IO.Directory]::CreateDirectory($consumer)
[xml]$props = Get-Content (Join-Path $workspace 'Directory.Build.props') -Raw
$version = $props.SelectSingleNode('//Version').InnerText
$summary = [ordered]@{ Completed = $false; Version = $version; RunDirectory = $run; Commands = @(); Configurations = @(); Failure = $null }

function DotNet([string]$Name, [string[]]$Arguments, [bool]$ExpectedFailure = $false) {
    # Use argument vectors so literal MSBuild properties and generated paths survive quoting.
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory = $consumer
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $binlog = Join-Path $run ($Name + '-{}.binlog')
    if ($Arguments[0] -in @('pack', 'restore', 'build')) { $info.ArgumentList.Add('-bl:' + $binlog) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        [void]$process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(180000)) { throw "$Name timed out" }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $run "$Name.log"), $output)
        $summary.Commands += @{ Name = $Name; Arguments = @($info.ArgumentList); ExitCode = $process.ExitCode }
        if ($ExpectedFailure) {
            if ($process.ExitCode -eq 0 -or $output -notmatch 'Undocumented.cs\(\d+,\d+\): error CS1591') { throw "$Name did not report consumer CS1591" }
            $errors = @([regex]::Matches($output, 'error (CS\d+|COB\d+|MSB\d+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
            if ($errors.Count -ne 1 -or $errors[0] -ne 'CS1591') { throw "$Name produced other errors" }
        } elseif ($process.ExitCode -ne 0) { throw "$Name failed; see $run/$Name.log" }
        if ($Arguments[0] -in @('pack', 'restore', 'build') -and !(Get-ChildItem $run -Filter "$Name-*.binlog")) { throw "$Name binlog missing" }
        Write-Host "$Name passed"
    } finally {
        if ($process.Id -and !$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}

try {
    foreach ($project in @('TrueMoon.Contracts', 'TrueMoon.Cobalt')) {
        DotNet "pack-$project" @('pack', (Join-Path $workspace "$project/$project.csproj"), '-c', 'Release', '--no-restore', '-o', $feed, '-v', 'quiet')
    }
    Get-ChildItem $PSScriptRoot -File | Where-Object { $_.Extension -in @('.cs', '.csproj', '.props') } | Copy-Item -Destination $consumer
    $feedXml = [Security.SecurityElement]::Escape($feed)
    @"
<configuration><packageSources><clear /><add key="fresh" value="$feedXml" /></packageSources></configuration>
"@ | Set-Content (Join-Path $consumer 'NuGet.Config') -Encoding utf8
    $properties = @("-p:TrueMoonPackageVersion=$version", "-p:RestorePackagesPath=$cache")
    DotNet 'restore' (@('restore', 'CobaltGeneratorConsumer.csproj', '--configfile', (Join-Path $consumer 'NuGet.Config'), '-v', 'quiet') + $properties)
    $assets = Get-Content (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    $libraries = @($assets.libraries.Keys | Sort-Object)
    if (Compare-Object @("TrueMoon.Cobalt/$version", "TrueMoon.Contracts/$version") $libraries) { throw 'Unexpected consumer dependencies' }
    $generator = Join-Path $cache "truemoon.cobalt/$version/analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll"
    $summary.GeneratorSha256 = (Get-FileHash $generator).Hash
    if ($summary.GeneratorSha256 -ne (Get-FileHash (Join-Path $workspace 'TrueMoon.Cobalt.Generator/bin/Release/net10.0/TrueMoon.Cobalt.Generator.dll')).Hash) { throw 'Bundled generator differs from source build' }
    $previousNames = $null
    foreach ($configuration in @('Debug', 'Release')) {
        DotNet "build-$configuration" (@('build', 'CobaltGeneratorConsumer.csproj', '-c', $configuration, '--no-restore', '--no-incremental', '-v', 'quiet') + $properties)
        $analyzers = @(Get-Content (Join-Path $consumer "obj/$configuration/net10.0/analyzers.txt") | Where-Object { $_ -match 'TrueMoon.Cobalt.Generator.dll$' })
        if ($analyzers.Count -ne 1 -or [IO.Path]::GetFullPath($analyzers[0]) -ne [IO.Path]::GetFullPath($generator)) { throw 'Expected one isolated bundled analyzer' }
        $sources = @(Get-ChildItem (Join-Path $consumer "obj/generated/$configuration") -Recurse -Filter '*.g.cs')
        if ($sources.Count -ne 9) { throw "Expected nine sources, got $($sources.Count)" }
        $names = @($sources.Name | Sort-Object)
        if (@($names | Where-Object { $_.Length -gt 128 -or $_ -cmatch '[^a-zA-Z0-9_.]' }).Count) { throw 'Invalid hint names' }
        if (@($names | Sort-Object -Unique).Count -ne $names.Count) { throw 'Hint name collision' }
        if ($previousNames -and (Compare-Object $previousNames $names)) { throw 'Hint names changed across configurations' }
        $previousNames = $names
        DotNet "run-$configuration" @((Join-Path $consumer "bin/$configuration/net10.0/CobaltGeneratorConsumer.dll"))
        $summary.Configurations += @{ Configuration = $configuration; Sources = $names; MaxComponentLength = ($names | ForEach-Object Length | Measure-Object -Maximum).Maximum; Checks = 11 }
    }
    [IO.File]::WriteAllText((Join-Path $consumer 'Undocumented.cs'), 'namespace NegativeControl; public class Undocumented;')
    foreach ($configuration in @('Debug', 'Release')) {
        DotNet "negative-$configuration" (@('build', 'CobaltGeneratorConsumer.csproj', '-c', $configuration, '--no-restore', '--no-incremental', '-v', 'quiet') + $properties) $true
    }
    $summary.Completed = $true
} catch {
    $summary.Failure = $_.Exception.Message
    throw
} finally {
    $summary | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $run 'summary.json') -Encoding utf8
    Write-Host "Evidence: $run"
}
