#Requires -Version 7.2
[CmdletBinding()]
param(
    [ValidateSet('All', 'Assembly', 'Source', 'Package')] [string] $Profile = 'All',
    [string] $RunDirectory,
    [string] $ValidationLayerPath,
    [ValidateRange(1, 1800)] [int] $TimeoutSeconds = 180,
    [string] $PackageVersion
)

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Integration/Common.ps1')
. (Join-Path $PSScriptRoot 'Integration/Package.ps1')
. (Join-Path $PSScriptRoot 'Integration/Verify.ps1')
if (!$RunDirectory) { $RunDirectory = Join-Path $workspace ('TestResults/AlloyInfrastructure/inf7-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')) }
$context = New-ProbeContext $workspace ([IO.Path]::GetFullPath($RunDirectory, $workspace)) $Profile
$selected = if ($Profile -eq 'All') { @('Assembly', 'Source', 'Package') } else { @($Profile) }
foreach ($phase in $selected) { $context.Manifest.Phases += [pscustomobject]@{ Name = $phase; Status = 'Pending' } }
$previousLayerPath = $env:VK_LAYER_PATH

function Invoke-AssemblyPhase {
    $hostingProject = Join-Path $workspace 'TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj'
    Assert-ProbeHostingReference $workspace
    foreach ($configuration in @('Debug', 'Release')) {
        foreach ($suite in @('Hosting', 'UI')) {
            $project = if ($suite -eq 'Hosting') { $hostingProject } else { Join-Path $workspace 'TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj' }
            $name = "tests-$suite-$configuration"; $results = Join-Path $context.Run $name
            Invoke-ProbeCommand $context $name 'dotnet' @('test', $project, '--configuration', $configuration,
                '--logger', 'trx;LogFileName=results.trx', '--results-directory', $results, '--verbosity', 'quiet') $TimeoutSeconds
            $path = Join-Path $results 'results.trx'; $counters = Read-ProbeTrx $path
            $context.Manifest.Tests += [pscustomobject]@{ Suite = $suite; Configuration = $configuration; Path = $path; Counters = $counters }
            Add-ProbeArtifact $context $path
            $binary = Join-Path (Split-Path $project) "bin/$configuration/net10.0"
            Add-ProbeBinaries $context $binary
        }
    }
}

function Invoke-SourcePhase {
    $project = Join-Path $PSScriptRoot 'AlloyFactoryConsumer.csproj'
    $publish = Join-Path $context.Run 'source/publish'
    Invoke-ProbeCommand $context 'source-build-Debug' 'dotnet' @('build', $project, '--configuration', 'Debug', '--verbosity', 'quiet') $TimeoutSeconds
    Invoke-ProbeCommand $context 'source-publish-Release' 'dotnet' @('publish', $project, '--configuration', 'Release', '--output', $publish, '--verbosity', 'quiet') $TimeoutSeconds
    foreach ($configuration in @('Debug', 'Release')) {
        $directory = if ($configuration -eq 'Debug') { Join-Path $PSScriptRoot 'bin/Debug/net10.0' } else { $publish }
        Add-ProbeBinaries $context $directory
        foreach ($provider in @('cobalt', 'di')) { foreach ($graphics in @('opengl', 'vulkan')) {
            $name = "source-$configuration-$provider-$graphics"; $report = Join-Path $context.Run "source/$name.json"
            Invoke-ProbeCommand $context $name (Join-Path $directory 'AlloyFactoryConsumer.exe') @($provider, $graphics, 'all', $report, '--validation') $TimeoutSeconds
            [void] (Assert-ProbeConsumer $report $provider $graphics $false)
            Add-ProbeArtifact $context $report
            $context.Manifest.Cases += [pscustomobject]@{ Phase = 'Source'; Kind = 'Factory'; Name = $name; Report = $report
                Configuration = $configuration; Provider = $provider; Graphics = $graphics; BinaryDirectory = $directory }
        } }
    }
    $nativeProject = Join-Path $workspace 'ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj'
    $nativePublish = Join-Path $context.Run 'source/native-publish'
    Invoke-ProbeCommand $context 'native-build-Debug' 'dotnet' @('build', $nativeProject, '--configuration', 'Debug', '--verbosity', 'quiet') $TimeoutSeconds
    Invoke-ProbeCommand $context 'native-publish-Release' 'dotnet' @('publish', $nativeProject, '--configuration', 'Release', '--output', $nativePublish, '--verbosity', 'quiet') $TimeoutSeconds
    foreach ($configuration in @('Debug', 'Release')) {
        $directory = if ($configuration -eq 'Debug') { Join-Path (Split-Path $nativeProject) 'bin/Debug/net10.0' } else { $nativePublish }
        Add-ProbeBinaries $context $directory
        foreach ($kind in @('Scheduling', 'Close', 'Dispatcher', 'HUD')) {
            $name = "native-$configuration-$kind"; $report = $null
            $arguments = switch ($kind) {
                'Scheduling' { $report = Join-Path $context.Run "source/$name.json"; @('--frame-scheduling', '--validation', '--scheduling-output', $report) }
                'Close' { $report = Join-Path $context.Run "source/$name.json"; @('--async-close', '--validation', '--close-output', $report) }
                'Dispatcher' { @('--hosted-dispatcher', '--validation') }
                'HUD' { @('--settings-hud', '--validation') }
            }
            Invoke-ProbeCommand $context $name (Join-Path $directory 'AlloyVulkanTest.exe') $arguments $TimeoutSeconds
            if ($report) { Assert-ProbeNative $report $kind; Add-ProbeArtifact $context $report }
            $context.Manifest.Cases += [pscustomobject]@{ Phase = 'Source'; Kind = $kind; Name = $name; Report = $report
                Configuration = $configuration; BinaryDirectory = $directory }
        }
    }
}

try {
    Save-ProbeManifest $context
    if ($Profile -ne 'Assembly') {
        if (!$IsWindows -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') { throw 'Native profiles require Windows x64 with desktop OpenGL/Vulkan.' }
        $layerPath = if ($ValidationLayerPath) { [IO.Path]::GetFullPath($ValidationLayerPath, $workspace) } else { $env:VK_LAYER_PATH }
        $manifests = @()
        foreach ($directory in @($layerPath -split [IO.Path]::PathSeparator | Where-Object { $_ })) {
            $path = Join-Path $directory 'VkLayer_khronos_validation.json'
            if (Test-Path -LiteralPath $path) { $manifests += Get-ProbeFingerprint $path }
        }
        if (!$manifests.Count) { throw 'Provide -ValidationLayerPath (SDK Bin) or VK_LAYER_PATH containing VkLayer_khronos_validation.json. Native validation is required.' }
        $env:VK_LAYER_PATH = $layerPath
        $context.Manifest.Validation = [pscustomobject]@{ Enabled = $true; LayerPath = $layerPath; Manifests = $manifests }
    } else { $context.Manifest.Validation = [pscustomobject]@{ Enabled = $false } }
    $globalJson = Get-Content -LiteralPath (Join-Path $workspace 'global.json') -Raw | ConvertFrom-Json
    if ($globalJson.test.runner -and $globalJson.test.runner -ne 'VSTest') { throw 'This runner uses the repository VSTest command mode; update it before switching test platforms.' }
    Invoke-ProbeCommand $context 'sdk-version' 'dotnet' @('--version') $TimeoutSeconds
    $context.Manifest.Sdk = [IO.File]::ReadAllText((Join-Path $context.Run 'logs/sdk-version.stdout.log')).Trim()
    if ($context.Manifest.Sdk -notmatch '^10\.') { throw 'The runner requires the repository .NET 10 SDK.' }
    $packageProjects = @(Get-ProbeProjectGraph $workspace @('TrueMoon.Alloy.Hosting/TrueMoon.Alloy.Hosting.csproj', 'TrueMoon/TrueMoon.csproj', 'TrueMoon.Extensions.DependencyInjection/TrueMoon.Extensions.DependencyInjection.csproj'))
    $allProjects = @(Get-ProbeProjectGraph $workspace @('TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj', 'TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj',
        'ManualTests/AlloyFactoryConsumer/AlloyFactoryConsumer.csproj', 'ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj'))
    $context.Manifest.InputProjects = $allProjects
    $context.Manifest.SourceDirectories = @((Join-Path $workspace 'ManualTests/AlloyTest'))
    $context.Manifest.Sources = @(Get-ProbeSources $workspace $allProjects $context.Manifest.SourceDirectories)
    if (!$PackageVersion) {
        [xml] $props = Get-Content -LiteralPath (Join-Path $workspace 'Directory.Build.props') -Raw
        $PackageVersion = $props.SelectSingleNode('//Version').InnerText + '.inf7.' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')
    }
    if ($PackageVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?-[0-9A-Za-z.-]+$') { throw 'PackageVersion must be a prerelease NuGet version.' }
    foreach ($phase in $context.Manifest.Phases) {
        $phase.Status = 'Running'; Save-ProbeManifest $context
        switch ($phase.Name) {
            'Assembly' { Invoke-AssemblyPhase }
            'Source' { Invoke-SourcePhase }
            'Package' { Invoke-ProbePackages $context $packageProjects $PackageVersion $TimeoutSeconds }
        }
        $phase.Status = 'Passed'; Save-ProbeManifest $context
    }
    Assert-ProbeRun $context.Manifest -AllowPendingCompletion
    $context.Manifest.Completed = $true
    Write-Host "Integration passed ($Profile): $($context.Run)"
}
catch {
    $context.Manifest.Failure = $_.Exception.ToString()
    foreach ($phase in $context.Manifest.Phases | Where-Object Status -eq 'Running') { $phase.Status = 'Failed' }
    throw
}
finally {
    $env:VK_LAYER_PATH = $previousLayerPath
    $context.Manifest.FinishedUtc = [DateTime]::UtcNow.ToString('O')
    Save-ProbeManifest $context
}
