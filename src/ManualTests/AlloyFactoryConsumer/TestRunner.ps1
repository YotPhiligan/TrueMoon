#Requires -Version 7.2
[CmdletBinding()]
param([string] $RunDirectory)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Integration/Common.ps1')
. (Join-Path $PSScriptRoot 'Integration/Package.ps1')
. (Join-Path $PSScriptRoot 'Integration/Verify.ps1')
if (!$RunDirectory) { $RunDirectory = Join-Path $workspace ('TestResults/AlloyInfrastructure/runner-tests-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')) }
$context = New-ProbeContext $workspace ([IO.Path]::GetFullPath($RunDirectory, $workspace)) 'Assembly'
$checks = [Collections.Generic.List[string]]::new()
function Check([bool] $Condition, [string] $Name) {
    if (!$Condition) { throw "Runner regression failed: $Name" }
    $checks.Add($Name)
}
function Reject([string] $Name, [scriptblock] $Action, [string] $Pattern) {
    $cause = $null
    try { & $Action | Out-Null } catch { $cause = $_.Exception }
    Check ($cause -and $cause.Message -match $Pattern) $Name
}
$passed = $false
try {
    Reject 'Existing evidence is never overwritten' { New-ProbeContext $workspace $context.Run 'Assembly' } 'fresh RunDirectory'
    Reject 'Traversal outside output root rejected' { Get-ProbePath (Join-Path $workspace '../outside') (Join-Path $workspace 'TestResults/AlloyInfrastructure') } 'must be inside'
    Reject 'Incomplete manifest rejected' { Assert-ProbeRun ([pscustomobject]@{ Schema = 1; Profile = 'All'; Completed = $false }) } 'did not complete'
    Reject 'Partial command matrix rejected' { Assert-ProbeCommandSet ([pscustomobject]@{ Commands = @([pscustomobject]@{ Name = 'sdk-version' }) }) @('Assembly') } 'command matrix'
    $fixture = Join-Path $context.Run 'artifact.txt'; 'original' | Set-Content -LiteralPath $fixture
    $hash = Get-ProbeFingerprint $fixture; Assert-ProbeFingerprints @($hash)
    Check $true 'Unchanged fingerprint accepted'
    'changed' | Set-Content -LiteralPath $fixture
    Reject 'Artifact/source drift rejected' { Assert-ProbeFingerprints @($hash) } 'Fingerprint changed'
    $dummy = Join-Path $context.Run 'dummy-source'; [IO.Directory]::CreateDirectory($dummy) | Out-Null
    $dummyProject = Join-Path $dummy 'Dummy.csproj'; '<Project />' | Set-Content -LiteralPath $dummyProject
    $sourceManifest = [pscustomobject]@{
        Schema = 1; Profile = 'Assembly'; Completed = $true; Failure = $null; Sdk = '10.0.0'; Workspace = $workspace
        Phases = @([pscustomobject]@{ Name = 'Assembly'; Status = 'Passed' }); InputProjects = @($dummyProject); SourceDirectories = @()
        Sources = @(Get-ProbeSources $workspace @($dummyProject) @()); Artifacts = @((Get-ProbeFingerprint $fixture))
        Commands = @(@('sdk-version', 'tests-Hosting-Debug', 'tests-Hosting-Release', 'tests-UI-Debug', 'tests-UI-Release') |
            ForEach-Object { [pscustomobject]@{ Name = $_; ExitCode = 0; TimedOut = $false; Failure = $null } })
    }
    'class Added {}' | Set-Content -LiteralPath (Join-Path $dummy 'Added.cs')
    Reject 'New source input invalidates snapshot' { Assert-ProbeRun $sourceManifest } 'Source input set changed'
    foreach ($kind in @('empty', 'failed', 'skipped', 'missing-results', 'passed')) {
        $path = Join-Path $context.Run "$kind.trx"
        $total = if ($kind -eq 'empty') { 0 } else { 1 }
        $success = if ($kind -in @('passed', 'missing-results')) { 1 } else { 0 }
        $failed = if ($kind -eq 'failed') { 1 } else { 0 }; $skipped = if ($kind -eq 'skipped') { 1 } else { 0 }
        $results = if ($kind -eq 'passed') { '<Results><UnitTestResult outcome="Passed" /></Results>' } else { '<Results />' }
        "<TestRun>$results<ResultSummary><Counters total=`"$total`" passed=`"$success`" failed=`"$failed`" notExecuted=`"$skipped`" /></ResultSummary></TestRun>" | Set-Content -LiteralPath $path
        if ($kind -eq 'passed') { $counter = Read-ProbeTrx $path; Check ($counter.Passed -eq 1) 'Real nonempty passing TRX accepted' }
        else { Reject "TRX $kind rejected" { Read-ProbeTrx $path } 'discovery|inconsistent TRX' }
    }
    $fakeConsumer = Join-Path $context.Run 'fake-consumer'; [IO.Directory]::CreateDirectory((Join-Path $fakeConsumer 'obj')) | Out-Null
    '<Project><PropertyGroup><EnableCobaltGenerator>false</EnableCobaltGenerator></PropertyGroup></Project>' | Set-Content -LiteralPath (Join-Path $fakeConsumer 'Consumer.csproj')
    '{"libraries":{"TrueMoon/1.0.0-test":{"type":"project"}}}' | Set-Content -LiteralPath (Join-Path $fakeConsumer 'obj/project.assets.json')
    Reject 'Package assets cannot hide a source dependency' { Assert-ProbePackageGraph $fakeConsumer '' '' @('TrueMoon') '1.0.0-test' } 'source dependency'
    $emptyPackage = Join-Path $context.Run 'missing-payload.nupkg'
    $archive = [IO.Compression.ZipFile]::Open($emptyPackage, [IO.Compression.ZipArchiveMode]::Create); $archive.Dispose()
    Reject 'Missing analyzer/native archive entry rejected' { Get-ProbeZipHash $emptyPackage 'analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll' } 'Missing or empty'
    $pwsh = (Get-Process -Id $PID).Path
    $space = Join-Path $context.Run 'paths with spaces'; [IO.Directory]::CreateDirectory($space) | Out-Null
    $child = Join-Path $space 'child script.ps1'
    'param([string]$Text,[int]$Code); [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); [Console]::WriteLine($Text); [Console]::Error.WriteLine("stderr captured"); exit $Code' | Set-Content -LiteralPath $child
    $literal = 'literal spaces; $() Ω'
    Invoke-ProbeCommand $context 'argument-list' $pwsh @('-NoProfile', '-File', $child, '-Text', $literal, '-Code', '0') 10
    Check ([IO.File]::ReadAllText((Join-Path $context.Run 'logs/argument-list.stdout.log')).Trim() -eq $literal) 'Paths and arguments with spaces/metacharacters preserved'
    Reject 'Nonzero exit fails command' { Invoke-ProbeCommand $context 'nonzero' $pwsh @('-NoProfile', '-File', $child, '-Text', 'controlled', '-Code', '7') 10 } 'exit code 7'
    Check ($context.Manifest.Commands[-1].ExitCode -eq 7 -and [IO.File]::ReadAllText((Join-Path $context.Run 'logs/nonzero.stderr.log')).Contains('stderr captured')) 'Failed process retains exit code and logs'
    $sleeper = Join-Path $space 'sleeper.ps1'; 'Start-Sleep -Seconds 30' | Set-Content -LiteralPath $sleeper
    $parent = Join-Path $space 'parent.ps1'; $childPid = Join-Path $space 'child.pid'
    @'
param([string]$Child,[string]$Record)
$info=[Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
$info.UseShellExecute=$false; $info.CreateNoWindow=$true
foreach($argument in @('-NoProfile','-File',$Child)){$info.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::Start($info)
[IO.File]::WriteAllText($Record,$process.Id.ToString())
[Console]::WriteLine('owned descendant started')
$process.WaitForExit()
'@ | Set-Content -LiteralPath $parent
    Reject 'Timeout fails and reaps owned process tree' { Invoke-ProbeCommand $context 'timeout' $pwsh @('-NoProfile', '-File', $parent, '-Child', $sleeper, '-Record', $childPid) 3 } 'timed out'
    $descendantId = [int] (Get-Content -LiteralPath $childPid)
    Check ($context.Manifest.Commands[-1].TimedOut -and !(Get-Process -Id $descendantId -ErrorAction SilentlyContinue) -and
        !(Get-Process -Id $context.Manifest.Commands[-1].Pid -ErrorAction SilentlyContinue)) 'Both owned timeout processes exited'
    $badRun = Join-Path $context.Run 'missing-validation'
    Reject 'Native prerequisite fails before dotnet/build' {
        Invoke-ProbeCommand $context 'missing-layer' $pwsh @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'RunIntegration.ps1'), '-Profile', 'Source',
            '-RunDirectory', $badRun, '-ValidationLayerPath', (Join-Path $context.Run 'absent-layer')) 10
    } 'exit code'
    $bad = Get-Content -LiteralPath (Join-Path $badRun 'summary.json') -Raw | ConvertFrom-Json
    Check (!$bad.Completed -and $bad.Failure.Contains('ValidationLayerPath') -and $bad.Commands.Count -eq 0) 'Failed preflight persists incomplete manifest with zero commands'
    $passed = $true
}
finally {
    [pscustomobject]@{ Passed = $passed; Checks = $checks.ToArray(); CheckCount = $checks.Count; Scope = 'PowerShell runner regressions; no GPU or .NET test execution.' } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $context.Run 'runner-tests.json')
}
Write-Host "Runner checks passed: $($checks.Count), $($context.Run)"
