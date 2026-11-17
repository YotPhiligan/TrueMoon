[CmdletBinding()]
param([string]$ResultsDirectory)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
if (!$ResultsDirectory) { $ResultsDirectory = 'TestResults/EneritXml-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff') }
$run = [IO.Path]::GetFullPath((Join-Path $workspace $ResultsDirectory))
if (Test-Path -LiteralPath $run) { throw 'Use a new evidence directory.' }
if (!$run.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence must be inside src.' }
$consumer = Join-Path $run 'consumer'; $feed = Join-Path $run 'feed'; $cache = Join-Path $run 'packages'
foreach ($directory in @($consumer,$feed,$cache)) { [void][IO.Directory]::CreateDirectory($directory) }
$profile = Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $run
$version = $profile.Version
$projects = @('TrueMoon.Contracts','TrueMoon.Core','TrueMoon.Cobalt','TrueMoon','TrueMoon.Enerit.Generator','TrueMoon.Enerit')
$summary = [ordered]@{ Completed=$false; Version=$version; BuildProfile=$profile; Commands=@(); Packages=@(); Configurations=@(); Sources=[ordered]@{}; Failure=$null }
$inputs = @(foreach ($directory in @($projects + @('TrueMoon.Cobalt.Generator','TrueMoon.Enerit.Generator.Tests','ManualTests/EneritDocumentationConsumer'))) {
    Get-ChildItem -LiteralPath (Join-Path $workspace $directory) -Recurse -File | Where-Object { $_.Extension -in @('.cs','.csproj','.ps1','.props','.targets','.md') -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
}) + @(Get-Item (Join-Path $workspace 'Directory.Build.props'),(Join-Path $workspace 'Directory.Packages.props'),(Join-Path $workspace 'global.json'),(Join-Path $workspace 'ManualTests/BuildProfile.ps1'))
foreach ($file in $inputs) { $summary.Sources[[IO.Path]::GetRelativePath($workspace,$file.FullName)] = (Get-FileHash -LiteralPath $file.FullName).Hash }
function DotNet([string]$Name,[string[]]$Arguments,[bool]$ExpectedFailure=$false) {
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet'); $info.WorkingDirectory=$consumer
    $info.UseShellExecute=$false; $info.CreateNoWindow=$true; $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    if ($Arguments[0] -in @('pack','restore','build','publish')) { $info.ArgumentList.Add('-bl:' + (Join-Path $run "$Name-{}.binlog")) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo=$info; $started=$false
    try {
        $started=$process.Start(); $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(180000)) { throw "$Name timed out" }
        $output=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $run "$Name.log"),$output)
        $summary.Commands += @{Name=$Name; Arguments=@($info.ArgumentList); ExitCode=$process.ExitCode}
        if ($ExpectedFailure) {
            if ($process.ExitCode -eq 0 -or $output -notmatch 'Undocumented.cs\(\d+,\d+\): error CS1591') { throw "$Name missed consumer CS1591" }
            $errors=@([regex]::Matches($output,'error (CS\d+|TME\d+|MSB\d+)') | ForEach-Object {$_.Groups[1].Value} | Sort-Object -Unique)
            if ($errors.Count -ne 1 -or $errors[0] -ne 'CS1591') { throw "$Name had unrelated errors" }
        } elseif ($process.ExitCode -ne 0) { throw "$Name failed; see $run/$Name.log" }
        if ($Arguments[0] -in @('pack','restore','build','publish') -and !(Get-ChildItem $run -Filter "$Name-*.binlog")) { throw "$Name missing binlog" }
        if ($Name -like 'run-*') {
            $record=@($output -split '\r?\n' | Where-Object { $_.StartsWith('RESULT ') })
            if ($record.Count -ne 1) { throw 'Missing runtime report' }
            $result=$record[0].Substring(7) | ConvertFrom-Json
            if (!$result.Completed -or $result.Calls -ne 4 -or $result.Pending -ne 0 -or !$result.WorkersCompleted) { throw 'Runtime verification failed' }
        }
        Write-Host "$Name passed"
    } finally {
        if ($started -and !$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}
try {
    foreach ($project in $projects) { DotNet "pack-$project" @('pack',(Join-Path $workspace "$project/$project.csproj"),'-c','Release','--no-restore','-o',$feed,'-v','quiet') }
    foreach ($archive in Get-ChildItem $feed -Filter '*.nupkg') {
        if (!$archive.Name.EndsWith(".$version.nupkg",[StringComparison]::Ordinal)) { throw 'Package version mismatch' }
        $summary.Packages += @{Path=[IO.Path]::GetRelativePath($workspace,$archive.FullName); Sha256=(Get-FileHash $archive.FullName).Hash}
    }
    if ($summary.Packages.Count -ne $projects.Count) { throw 'Package cohort incomplete' }
    Get-ChildItem $PSScriptRoot -File | Where-Object Extension -in @('.cs','.csproj','.props') | Copy-Item -Destination $consumer
    Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $consumer
    $feedXml=[Security.SecurityElement]::Escape($feed)
    "<configuration><packageSources><clear /><add key=`"fresh`" value=`"$feedXml`" /></packageSources></configuration>" | Set-Content (Join-Path $consumer 'NuGet.Config') -Encoding utf8
    $properties=@("-p:TrueMoonPackageVersion=$version","-p:RestorePackagesPath=$cache")
    $project='EneritDocumentationConsumer.csproj'
    DotNet 'restore' (@('restore',$project,'--configfile',(Join-Path $consumer 'NuGet.Config'),'-v','quiet')+$properties)
    $assets=Get-Content (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    if (Compare-Object @($projects | ForEach-Object {"$_/$version"} | Sort-Object) @($assets.libraries.Keys | Sort-Object)) { throw 'Unexpected package graph' }
    if (@($assets.libraries.Values | Where-Object type -ne 'package').Count) { throw 'Fresh consumer has project references' }
    $generator=Join-Path $cache "truemoon.enerit.generator/$version/analyzers/dotnet/cs/TrueMoon.Enerit.Generator.dll"
    $summary.GeneratorSha256=(Get-FileHash $generator).Hash
    if ($summary.GeneratorSha256 -ne (Get-FileHash (Join-Path $workspace "TrueMoon.Enerit.Generator/bin/Release/$($profile.Framework)/TrueMoon.Enerit.Generator.dll")).Hash) { throw 'Analyzer SHA differs from source' }
    foreach ($configuration in @('Debug','Release')) {
        DotNet "build-$configuration" (@('build',$project,'-c',$configuration,'--no-restore','--no-incremental','-v','quiet')+$properties)
        $analyzers=@(Get-Content (Join-Path $consumer "obj/$configuration/$($profile.Framework)/analyzers.txt") | Where-Object {$_ -match 'TrueMoon.Enerit.Generator.dll$'})
        if ($analyzers.Count -ne 1 -or [IO.Path]::GetFullPath($analyzers[0]) -ne $generator) { throw 'Isolated analyzer was not used' }
        $sources=@(Get-ChildItem (Join-Path $consumer "obj/generated/$configuration") -Recurse -Filter '*.g.cs')
        $eneritSources=@($sources | Where-Object FullName -match 'TrueMoon.Enerit.Generator')
        if ($eneritSources.Count -ne 5) { throw 'Expected five Enerit generated sources' }
        DotNet "run-$configuration" @((Join-Path $consumer "bin/$configuration/$($profile.Framework)/EneritDocumentationConsumer.dll"))
        $summary.Configurations += @{Configuration=$configuration; GeneratedSources=@($eneritSources.Name); Calls=4}
    }
    [IO.File]::WriteAllText((Join-Path $consumer 'Undocumented.cs'),"namespace NegativeControl; /// <summary>Consumer API.</summary>`npublic class ConsumerApi { public void Undocumented() {} }")
    foreach ($configuration in @('Debug','Release')) { DotNet "negative-$configuration" (@('build',$project,'-c',$configuration,'--no-restore','--no-incremental','-v','quiet')+$properties) $true }
    foreach ($entry in $summary.Sources.GetEnumerator()) { if ((Get-FileHash (Join-Path $workspace $entry.Key)).Hash -ne $entry.Value) { throw "Input drift: $($entry.Key)" } }
    $summary.Completed=$true
} catch { $summary.Failure=$_.Exception.Message; throw }
finally { $summary | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $run 'summary.json') -Encoding utf8; Write-Host "Evidence: $run" }
