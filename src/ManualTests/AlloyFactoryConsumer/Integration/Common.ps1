# Shared by the tracked runner and its verifier. Dot-sourcing performs no work.
function Get-ProbePath([string] $Path, [string] $Root) {
    $full = [IO.Path]::GetFullPath($Path, $Root)
    $prefix = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Root)) + [IO.Path]::DirectorySeparatorChar
    if (!$full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Path must be inside $Root : $full" }
    return $full
}

function New-ProbeContext([string] $Workspace, [string] $RunDirectory, [string] $Profile) {
    $allowed = Join-Path $Workspace 'TestResults/AlloyInfrastructure'
    $run = Get-ProbePath $RunDirectory $allowed
    if (Test-Path -LiteralPath $run) { throw "Use a fresh RunDirectory; existing evidence is never overwritten: $run" }
    [IO.Directory]::CreateDirectory((Join-Path $run 'logs')) | Out-Null
    return [pscustomobject]@{
        Workspace = $Workspace; Run = $run
        Manifest = [ordered]@{
            Schema = 1; Completed = $false; Profile = $Profile; Workspace = $Workspace; RunDirectory = $run
            StartedUtc = [DateTime]::UtcNow.ToString('O'); FinishedUtc = $null; Sdk = $null
            OS = [Environment]::OSVersion.VersionString; Architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
            Validation = $null; Failure = $null; Phases = @(); Commands = @(); Tests = @(); Cases = @()
            Sources = @(); InputProjects = @(); SourceDirectories = @(); Artifacts = @(); Packages = @(); PackagePayloads = @(); PackageVersion = $null; PackageIds = @(); Consumer = $null
        }
    }
}

function Save-ProbeManifest($Context) {
    $Context.Manifest | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath (Join-Path $Context.Run 'summary.json') -Encoding utf8
}

function Get-ProbeFingerprint([string] $Path) {
    return [pscustomobject]@{ Path = [IO.Path]::GetFullPath($Path); Sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
}

function Add-ProbeArtifact($Context, [string] $Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $existing = @($Context.Manifest.Artifacts | Where-Object Path -eq $full)
    if ($existing.Count) { Assert-ProbeFingerprints $existing; return }
    $Context.Manifest.Artifacts += Get-ProbeFingerprint $full
}

function Assert-ProbeFingerprints($Entries) {
    foreach ($entry in $Entries) {
        if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $entry.Sha256) { throw "Fingerprint changed: $($entry.Path)" }
    }
}

function Add-ProbeBinaries($Context, [string] $Directory) {
    foreach ($file in Get-ChildItem -LiteralPath $Directory -Recurse -File | Where-Object Extension -in @('.exe', '.dll', '.json')) {
        Add-ProbeArtifact $Context $file.FullName
    }
}

function Invoke-ProbeCommand($Context, [string] $Name, [string] $Executable, [string[]] $Arguments,
    [int] $TimeoutSeconds = 180) {
    if ($Name -notmatch '^[A-Za-z0-9_.-]+$' -or $TimeoutSeconds -lt 1) { throw 'Invalid command name or timeout.' }
    $stdoutPath = Join-Path $Context.Run "logs/$Name.stdout.log"
    $stderrPath = Join-Path $Context.Run "logs/$Name.stderr.log"
    if ((Test-Path -LiteralPath $stdoutPath) -or (Test-Path -LiteralPath $stderrPath)) { throw "Duplicate command: $Name" }
    $info = [Diagnostics.ProcessStartInfo]::new($Executable)
    $info.WorkingDirectory = $Context.Workspace
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $info.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    $entry = [ordered]@{ Name = $Name; Executable = $Executable; Arguments = $Arguments; Pid = $null
        StartedUtc = [DateTime]::UtcNow.ToString('O'); ExitCode = $null; TimedOut = $false; Failure = $null
        Stdout = $stdoutPath; Stderr = $stderrPath }
    $started = $false; $stdout = $null; $stderr = $null
    try {
        $started = $process.Start()
        if (!$started) { throw "Could not start $Name" }
        $entry.Pid = $process.Id
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            $entry.TimedOut = $true
            $process.Kill($true) # Only this process and its descendants belong to this invocation.
            if (!$process.WaitForExit(5000)) { throw "Could not reap owned process $Name" }
        }
        $entry.ExitCode = $process.ExitCode
        if (!$stdout.Wait(5000) -or !$stderr.Wait(5000)) { throw "Output did not close: $Name" }
        if ($entry.TimedOut) { throw "$Name timed out after ${TimeoutSeconds}s." }
        if ($entry.ExitCode -ne 0) { throw "$Name failed with exit code $($entry.ExitCode); see $stderrPath" }
        Write-Host "$Name passed"
    }
    catch { $entry.Failure = $_.Exception.Message; throw }
    finally {
        try {
            if ($started -and !$process.HasExited) {
                $process.Kill($true)
                if (!$process.WaitForExit(5000)) { throw "Could not reap owned process $Name" }
            }
            if ($stdout -and $stdout.Wait(5000)) { [IO.File]::WriteAllText($stdoutPath, $stdout.GetAwaiter().GetResult()) }
            if ($stderr -and $stderr.Wait(5000)) { [IO.File]::WriteAllText($stderrPath, $stderr.GetAwaiter().GetResult()) }
            foreach ($path in @($stdoutPath, $stderrPath)) { if (Test-Path -LiteralPath $path) { Add-ProbeArtifact $Context $path } }
        }
        finally {
            $process.Dispose(); $Context.Manifest.Commands += $entry; Save-ProbeManifest $Context
        }
    }
}

function Get-ProbeProjectGraph([string] $Workspace, [string[]] $Roots) {
    $visited = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $ordered = [Collections.Generic.List[string]]::new()
    function Visit([string] $Path) {
        $full = Get-ProbePath $Path $Workspace
        if (!$visited.Add($full)) { return }
        [xml] $project = Get-Content -LiteralPath $full -Raw
        foreach ($reference in $project.SelectNodes('//ProjectReference')) {
            if ($reference.Include.Contains('$(')) { throw "Unevaluated project reference in $full" }
            Visit ([IO.Path]::GetFullPath($reference.Include, (Split-Path $full)))
        }
        $ordered.Add($full)
    }
    foreach ($root in $Roots) { Visit ([IO.Path]::GetFullPath($root, $Workspace)) }
    return $ordered.ToArray()
}

function Get-ProbeSources([string] $Workspace, [string[]] $Projects, [string[]] $ExtraDirectories) {
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $directories = @($Projects | ForEach-Object { Split-Path $_ }) + $ExtraDirectories
    foreach ($directory in $directories | Sort-Object -Unique) {
        foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs', '.csproj', '.props', '.targets', '.json', '.ps1', '.template', '.png', '.ttf', '.spv', '.dll')
        }) { [void] $paths.Add($file.FullName) }
    }
    foreach ($name in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'global.json', 'NuGet.Config')) {
        $path = Join-Path $Workspace $name
        if (Test-Path -LiteralPath $path) { [void] $paths.Add($path) }
    }
    foreach ($path in $paths | Sort-Object) { Get-ProbeFingerprint $path }
}

function Read-ProbeTrx([string] $Path) {
    [xml] $trx = Get-Content -LiteralPath $Path -Raw
    $c = $trx.TestRun.ResultSummary.Counters
    if (!$c -or [int] $c.total -le 0 -or [int] $c.passed -ne [int] $c.total -or [int] $c.failed -ne 0 -or [int] $c.notExecuted -ne 0) {
        throw "Empty, failed or skipped discovery: $Path"
    }
    $results = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
    if ($results.Count -ne [int] $c.total -or @($results | Where-Object outcome -ne 'Passed').Count) { throw "Missing or inconsistent TRX results: $Path" }
    return [pscustomobject]@{ Total = [int] $c.total; Passed = [int] $c.passed; Failed = [int] $c.failed; Skipped = [int] $c.notExecuted }
}

function Assert-ProbeConsumer([string] $Path, [string] $Provider, [string] $Graphics, [bool] $Package) {
    $report = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if (!$report.Completed -or $report.Failure -or $report.Provider -ne $Provider -or $report.Graphics -ne $Graphics -or $report.Registry -ne 0 -or $report.ValidationMessages -ne 0) {
        throw "Consumer failed: $Path"
    }
    $expected = if ($Package) { @('factory') } else { @('disposed', 'factory', 'legacy', 'null', 'parented', 'throw') }
    if ((@($report.Cases.Scenario | Sort-Object) -join ',') -ne ($expected -join ',')) { throw "Consumer scenarios missing: $Path" }
    foreach ($case in $report.Cases) {
        if (!$case.Passed -or $case.Calls -ne 1 -or $case.Owner -le 0 -or $case.Registry -ne 0 -or $case.DependencyDisposals -ne 1 -or
            $case.NoViewRegistration -ne ($case.Scenario -ne 'legacy')) { throw "Consumer ownership/registration failed: $Path" }
        if ($case.Scenario -in @('factory', 'legacy', 'disposed') -and $case.Owner -ne $case.CleanupOwner) { throw "Consumer cleanup affinity failed: $Path" }
    }
    if (@($report.Assemblies).Count -ne 5) { throw "Missing assembly provenance: $Path" }
    return $report
}

function Assert-ProbeNative([string] $Path, [string] $Kind) {
    $report = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if (!$report.Completed -or $report.Cases.Count -ne 2 -or $report.ValidationMessages -ne 0) { throw "Native report failed: $Path" }
    if ($Kind -eq 'Close') {
        if ($report.Hooks -ne 0) { throw "Native hooks retained: $Path" }
        foreach ($case in $report.Cases) {
            if (!$case.NativeVeto -or !$case.SaveFailureRetry -or !$case.Concurrent -or !$case.WaitCancellation -or !$case.ResizeSuspendResume -or
                !$case.OwnerProgress -or !$case.ForcedDrain -or !$case.CleanupFailure -or $case.Registry -ne 0) { throw "Close behavior failed: $Path" }
        }
    } else {
        if ($report.DpiHooks -ne 0) { throw "Native hooks retained: $Path" }
        foreach ($case in $report.Cases) {
            if (!$case.Pixels -or !$case.NativeExpose -or !$case.MinimizeResume -or $case.Delivered -ne 200 -or $case.Registry -ne 0 -or
                $case.First.State -ne 4 -or $case.Second.State -ne 4) { throw "Scheduling behavior failed: $Path" }
        }
    }
}
