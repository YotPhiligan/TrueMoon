function Assert-ProbeHostingReference([string] $Workspace) {
    $directory = Join-Path $Workspace 'TrueMoon.Alloy.Hosting.Tests'
    [xml] $project = Get-Content -LiteralPath (Join-Path $directory 'TrueMoon.Alloy.Hosting.Tests.csproj') -Raw
    $hosting = Join-Path $Workspace 'TrueMoon.Alloy.Hosting'
    $references = @($project.SelectNodes('//ProjectReference') | ForEach-Object { [IO.Path]::GetFullPath($_.Include, $directory) })
    if ((Join-Path $hosting 'TrueMoon.Alloy.Hosting.csproj') -notin $references) { throw 'Hosting tests must reference the actual production assembly.' }
    foreach ($item in $project.SelectNodes('//Compile[@Include]')) {
        $path = [IO.Path]::GetFullPath($item.Include, $directory)
        if ($path.StartsWith($hosting + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Hosting contract tests must not compile linked Hosting production copies.'
        }
    }
}

function Assert-ProbeCommandSet($Manifest, [string[]] $Phases) {
    $names = @('sdk-version')
    if ('Assembly' -in $Phases) {
        foreach ($configuration in @('Debug', 'Release')) { foreach ($suite in @('Hosting', 'UI')) { $names += "tests-$suite-$configuration" } }
    }
    if ('Source' -in $Phases) {
        $names += @('source-build-Debug', 'source-publish-Release', 'native-build-Debug', 'native-publish-Release')
        foreach ($configuration in @('Debug', 'Release')) {
            foreach ($provider in @('cobalt', 'di')) { foreach ($graphics in @('opengl', 'vulkan')) { $names += "source-$configuration-$provider-$graphics" } }
            foreach ($kind in @('Scheduling', 'Close', 'Dispatcher', 'HUD')) { $names += "native-$configuration-$kind" }
        }
    }
    if ('Package' -in $Phases) {
        $names += @('package-restore', 'package-build-Debug', 'package-publish-Release')
        foreach ($id in $Manifest.PackageIds) { $names += "pack-$id" }
        foreach ($configuration in @('Debug', 'Release')) {
            foreach ($provider in @('cobalt', 'di')) { foreach ($graphics in @('opengl', 'vulkan')) { $names += "package-$configuration-$provider-$graphics" } }
        }
    }
    if (@(Compare-Object @($names | Sort-Object) @($Manifest.Commands.Name | Sort-Object)).Count) { throw 'Incomplete or unexpected command matrix.' }
}

function Assert-ProbeRun($Manifest, [switch] $AllowPendingCompletion) {
    if ($Manifest.Schema -ne 1 -or $Manifest.Profile -notin @('All', 'Assembly', 'Source', 'Package')) { throw 'Unsupported integration manifest schema/profile.' }
    if ((!$AllowPendingCompletion -and !$Manifest.Completed) -or $Manifest.Failure) { throw 'Integration did not complete successfully.' }
    $expected = if ($Manifest.Profile -eq 'All') { @('Assembly', 'Package', 'Source') } else { @($Manifest.Profile) }
    if ((@($Manifest.Phases.Name | Sort-Object) -join ',') -ne ($expected -join ',') -or @($Manifest.Phases | Where-Object Status -ne 'Passed').Count) { throw 'Selected phases are incomplete.' }
    if (!$Manifest.Sources.Count -or !$Manifest.Artifacts.Count -or !$Manifest.Commands.Count -or $Manifest.Sdk -notmatch '^10\.') { throw 'Missing input/artifact/command/SDK provenance.' }
    if (@($Manifest.Commands | Where-Object { $_.ExitCode -ne 0 -or $_.TimedOut -or $_.Failure }).Count) { throw 'Command failure/timeout in manifest.' }
    if (@($Manifest.Commands.Name | Sort-Object -Unique).Count -ne $Manifest.Commands.Count) { throw 'Duplicate command identities.' }
    Assert-ProbeCommandSet $Manifest $expected
    Assert-ProbeFingerprints $Manifest.Sources
    $currentSources = @(Get-ProbeSources $Manifest.Workspace $Manifest.InputProjects $Manifest.SourceDirectories)
    if (@(Compare-Object @($Manifest.Sources.Path | Sort-Object) @($currentSources.Path | Sort-Object)).Count) { throw 'Source input set changed.' }
    Assert-ProbeFingerprints $Manifest.Artifacts
    if ($Manifest.Profile -ne 'Assembly') {
        if (!$Manifest.Validation.Enabled -or !$Manifest.Validation.Manifests.Count) { throw 'Native validation provenance missing.' }
        Assert-ProbeFingerprints $Manifest.Validation.Manifests
    }
    $artifactPaths = @($Manifest.Artifacts.Path)
    if ('Assembly' -in $expected) {
        Assert-ProbeHostingReference $Manifest.Workspace
        if ($Manifest.Tests.Count -ne 4) { throw 'Assembly profile requires both suites in Debug/Release.' }
        foreach ($suite in @('Hosting', 'UI')) { foreach ($configuration in @('Debug', 'Release')) {
            $entry = @($Manifest.Tests | Where-Object { $_.Suite -eq $suite -and $_.Configuration -eq $configuration })
            if ($entry.Count -ne 1 -or $entry[0].Path -notin $artifactPaths) { throw 'Missing test report.' }
            $counts = Read-ProbeTrx $entry[0].Path
            if ($counts.Total -ne $entry[0].Counters.Total -or $counts.Passed -ne $entry[0].Counters.Passed) { throw 'Test discovery drift.' }
        } }
    } elseif ($Manifest.Tests.Count) { throw 'Unexpected test scope.' }
    foreach ($phase in @('Source', 'Package')) {
        $cases = @($Manifest.Cases | Where-Object Phase -eq $phase)
        if ($phase -notin $expected) { if ($cases.Count) { throw 'Unexpected native scope.' }; continue }
        $count = if ($phase -eq 'Source') { 16 } else { 8 }
        if ($cases.Count -ne $count -or @($cases.Name | Sort-Object -Unique).Count -ne $count) { throw "Incomplete $phase matrix." }
        foreach ($configuration in @('Debug', 'Release')) {
            foreach ($provider in @('cobalt', 'di')) { foreach ($graphics in @('opengl', 'vulkan')) {
                $entry = @($cases | Where-Object { $_.Kind -eq 'Factory' -and $_.Configuration -eq $configuration -and $_.Provider -eq $provider -and $_.Graphics -eq $graphics })
                if ($entry.Count -ne 1 -or $entry[0].Report -notin $artifactPaths) { throw "Missing $phase factory report." }
                $entry = $entry[0]
                [void] (Assert-ProbeConsumer $entry.Report $provider $graphics ($phase -eq 'Package'))
                foreach ($assembly in (Get-Content -LiteralPath $entry.Report -Raw | ConvertFrom-Json).Assemblies) {
                    [void] (Get-ProbePath $assembly $entry.BinaryDirectory)
                    if ($assembly -notin $artifactPaths) { throw "Untracked loaded assembly: $assembly" }
                }
                if ($phase -eq 'Package') { Assert-ProbePackageDelivery $Manifest $entry.Report $entry.BinaryDirectory }
            } }
            if ($phase -eq 'Source') {
                foreach ($kind in @('Scheduling', 'Close', 'Dispatcher', 'HUD')) {
                    $entry = @($cases | Where-Object { $_.Kind -eq $kind -and $_.Configuration -eq $configuration })
                    if ($entry.Count -ne 1 -or @($Manifest.Commands | Where-Object Name -eq $entry[0].Name).Count -ne 1) { throw "Missing native $kind command." }
                    if ($entry[0].Report) {
                        if ($entry[0].Report -notin $artifactPaths) { throw 'Untracked native report.' }
                        Assert-ProbeNative $entry[0].Report $kind
                    }
                }
            }
        }
    }
    if ('Package' -in $expected) {
        if (!$Manifest.PackageIds.Count -or $Manifest.Packages.Count -ne $Manifest.PackageIds.Count) { throw 'Incomplete fresh package set.' }
        Assert-ProbePackageGraph $Manifest.Consumer.Directory $Manifest.Consumer.Cache $Manifest.Consumer.Feed $Manifest.PackageIds $Manifest.PackageVersion
        Assert-ProbeFingerprints $Manifest.Packages
        $payloads = @(Get-ProbeRequiredPayloads $Manifest.Packages)
        if ($Manifest.PackagePayloads.Count -ne $payloads.Count) { throw 'Missing analyzer/native payload provenance.' }
        foreach ($payload in $payloads) {
            $record = @($Manifest.PackagePayloads | Where-Object { $_.PackageId -eq $payload.PackageId -and $_.Entry -eq $payload.Entry })
            if ($record.Count -ne 1 -or $record[0].Sha256 -ne $payload.Sha256) { throw 'Analyzer/native payload drift.' }
        }
        foreach ($configuration in @('Debug', 'Release')) { Assert-ProbeNoAnalyzer $Manifest.Consumer.Directory $configuration }
        if ((Get-FileHash -LiteralPath (Join-Path $Manifest.Consumer.Directory 'Program.cs')).Hash -ne
            (Get-FileHash -LiteralPath (Join-Path $Manifest.Workspace 'ManualTests/AlloyFactoryConsumer/Program.cs')).Hash) { throw 'Independent consumer source drift.' }
    }
}
