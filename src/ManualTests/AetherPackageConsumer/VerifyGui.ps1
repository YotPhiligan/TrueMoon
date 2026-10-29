[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageVerification,
    [Parameter(Mandatory)][string]$ValidationDirectory,
    [string]$EvidenceDirectory,
    [Alias('Profile')][ValidateSet('All','SourceDebug','SourceRelease','PackageDebug','PackageRelease')][string]$RunProfile = 'All',
    [ValidateSet('All','Standard','Embedded')][string]$HostKind = 'All',
    [ValidateSet('All','Workflow','Failure','Cancel')][string]$Scenario = 'All'
)

$ErrorActionPreference = 'Stop'
$guiWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$guiBaseline = Get-Content -LiteralPath $PackageVerification -Raw | ConvertFrom-Json
if (!$guiBaseline.Completed -or $guiBaseline.Cases.Count -ne 8 -or $guiBaseline.Packages.Count -ne 15) { throw 'A completed ARCH2e.1/2 package proof is required.' }
$guiValidation = [IO.Path]::GetFullPath($ValidationDirectory)
if (!(Test-Path -LiteralPath "$guiValidation/VkLayer_khronos_validation.json") -or !(Test-Path -LiteralPath "$guiValidation/VkLayer_khronos_validation.dll")) { throw 'Provide the extracted validation layer directory.' }
$guiEvidence = if ($EvidenceDirectory) { [IO.Path]::GetFullPath($EvidenceDirectory) } else { Join-Path $guiWorkspace ('TestResults/ARCH2e3-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
if (Test-Path -LiteralPath $guiEvidence) { throw 'Use a fresh EvidenceDirectory.' }
$guiRun = Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ARCH2e3-' + [Guid]::NewGuid().ToString('N'))
foreach ($directory in @($guiEvidence, $guiRun, "$guiRun/unrelated working directory")) { [void][IO.Directory]::CreateDirectory($directory) }
$guiDotnet = (Get-Command dotnet.exe).Source
$guiReport = [ordered]@{
    Completed = $false; Stage = 'ARCH2e.3'; Profile = $RunProfile; HostKind = $HostKind; Scenario = $Scenario
    RunDirectory = $guiRun; EvidenceDirectory = $guiEvidence; Baseline = [IO.Path]::GetFullPath($PackageVerification)
    PackageVersion = $guiBaseline.Version; FrameworkChangesSincePackage = @(); Validation = @(); Sources = @(); Commands = @(); Cases = @(); Failure = $null
    Environment = [ordered]@{ OS = [Environment]::OSVersion.VersionString; Architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        Drivers = @(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion) }
}
function Save-GuiReport { [IO.File]::WriteAllText("$guiEvidence/verification.json", ($guiReport | ConvertTo-Json -Depth 35)) }
function Gui-Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function Invoke-Gui([string]$name, [string[]]$arguments, [string]$cwd = "$guiRun/unrelated working directory", [switch]$package) {
    $info = [Diagnostics.ProcessStartInfo]::new($guiDotnet)
    $info.WorkingDirectory = $cwd; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.Environment['VK_LAYER_PATH'] = $guiValidation
    $info.Environment['TRUEMOON_GPU_VALIDATION'] = '1'
    [void]$info.Environment.Remove('TM_ENGINE_BOOTSTRAP')
    if ($package) { $info.Environment['NUGET_PACKAGES'] = "$guiRun/cache"; $info.Environment['NUGET_HTTP_CACHE_PATH'] = "$guiRun/http-cache" }
    foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    $output = [IO.File]::Create("$guiEvidence/$name.stdout.log"); $errors = [IO.File]::Create("$guiEvidence/$name.stderr.log")
    $command = [ordered]@{ Name = $name; FileName = $guiDotnet; Arguments = $arguments; WorkingDirectory = $cwd
        Started = [DateTimeOffset]::UtcNow; ProcessId = $null; ExitCode = $null; TimedOut = $false; PackageCache = [bool]$package }
    $guiReport.Commands += $command
    try {
        if (!$process.Start()) { throw "Cannot start $name" }
        $command.ProcessId = $process.Id; Save-GuiReport
        $copyOutput = $process.StandardOutput.BaseStream.CopyToAsync($output)
        $copyErrors = $process.StandardError.BaseStream.CopyToAsync($errors)
        if (!$process.WaitForExit(300000)) { $command.TimedOut = $true; $process.Kill($true); if (!$process.WaitForExit(10000)) { throw 'Cannot reap owned process tree.' } }
        $null = $copyOutput.GetAwaiter().GetResult(); $null = $copyErrors.GetAwaiter().GetResult(); $output.Flush(); $errors.Flush()
        $command.ExitCode = $process.ExitCode
        if ($command.TimedOut -or $command.ExitCode -ne 0) { throw "$name failed (exit $($command.ExitCode)); see persisted logs." }
        if ($arguments[0] -in @('restore','build','publish') -and !(Get-ChildItem -LiteralPath $guiEvidence -Filter "$name-*.binlog")) { throw 'MSBuild binlog missing.' }
        Write-Host "$name passed"
    } finally {
        if ($command.ProcessId -and !$process.HasExited) { $process.Kill($true); $process.WaitForExit(10000) | Out-Null }
        $output.Dispose(); $errors.Dispose(); $process.Dispose(); Save-GuiReport
    }
}

try {
    foreach ($entry in $guiBaseline.Packages) { if ((Gui-Hash $entry.Path) -ne $entry.Sha256) { throw 'Package baseline changed.' } }
    foreach ($entry in $guiBaseline.Sources) {
        if ($entry.Path -notlike '*ManualTests*AetherPackageConsumer*') {
            $currentHash = Gui-Hash $entry.Path
            $guiReport.Sources += [ordered]@{ Path = $entry.Path; Sha256 = $currentHash }
            if ($currentHash -ne $entry.Sha256) {
                $guiReport.FrameworkChangesSincePackage += $entry.Path
                if ($RunProfile -in @('All','PackageDebug','PackageRelease')) { throw "Framework input changed; generate fresh packages first: $($entry.Path)" }
            }
        }
    }
    foreach ($path in @("$guiValidation/VkLayer_khronos_validation.json", "$guiValidation/VkLayer_khronos_validation.dll")) {
        $guiReport.Validation += [ordered]@{ Path = $path; Sha256 = Gui-Hash $path }
    }
    $paths = @(Get-ChildItem -LiteralPath "$PSScriptRoot/Gui" -File | ForEach-Object FullName)
    $paths += $PSCommandPath, "$guiWorkspace/Examples/AetherRuntimeHost/Program.cs", "$guiWorkspace/Examples/AetherRuntimeHost/AetherRuntimeHost.csproj"
    foreach ($path in $paths) { $guiReport.Sources += [ordered]@{ Path = $path; Sha256 = Gui-Hash $path } }
    $profiles = if ($RunProfile -eq 'All') { @('SourceDebug','SourceRelease','PackageDebug','PackageRelease') } else { @($RunProfile) }
    if (@($profiles | Where-Object { $_ -like 'Source*' }).Count) {
        if ('SourceDebug' -in $profiles) {
            Invoke-Gui 'source-build-Debug' @('build', 'ManualTests/AetherPackageConsumer/Gui/GuiConsumer.csproj', '-c','Debug','--verbosity','quiet',"-bl:$guiEvidence/source-build-Debug-{}.binlog") $guiWorkspace
            Invoke-Gui 'runtime-build-Debug' @('build', 'Examples/AetherRuntimeHost/AetherRuntimeHost.csproj', '-c','Debug','--verbosity','quiet',"-bl:$guiEvidence/runtime-build-Debug-{}.binlog") $guiWorkspace
        }
        if ('SourceRelease' -in $profiles) {
            Invoke-Gui 'source-publish-Release' @('publish', 'ManualTests/AetherPackageConsumer/Gui/GuiConsumer.csproj', '-c','Release','-o',"$guiRun/source-Release/Gui",'--verbosity','quiet',"-bl:$guiEvidence/source-publish-Release-{}.binlog") $guiWorkspace
            Invoke-Gui 'runtime-publish-Release' @('publish', 'Examples/AetherRuntimeHost/AetherRuntimeHost.csproj', '-c','Release','-o',"$guiRun/source-Release/Runtime",'--verbosity','quiet',"-bl:$guiEvidence/runtime-publish-Release-{}.binlog") $guiWorkspace
        }
    }
    if (@($profiles | Where-Object { $_ -like 'Package*' }).Count) {
        $consumer = "$guiRun/consumer"; [void][IO.Directory]::CreateDirectory("$consumer/Runtime")
        [void][IO.Directory]::CreateDirectory("$consumer/Gui/Assets/Fonts")
        Copy-Item -Path "$PSScriptRoot/Gui/*.cs" -Destination "$consumer/Gui"
        [IO.File]::WriteAllText("$consumer/Gui/GuiConsumer.csproj", ([IO.File]::ReadAllText("$PSScriptRoot/Gui/GuiConsumer.csproj.template").Replace('PACKAGE_VERSION', $guiBaseline.Version)))
        Copy-Item -LiteralPath "$guiWorkspace/Examples/AetherRuntimeHost/Program.cs" -Destination "$consumer/Runtime/Program.cs"
        [IO.File]::WriteAllText("$consumer/Runtime/Runtime.csproj", @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><RootNamespace>TrueMoon.Examples.AetherRuntimeHost</RootNamespace></PropertyGroup>
<ItemGroup><PackageReference Include="TrueMoon" Version="$($guiBaseline.Version)" /><PackageReference Include="TrueMoon.Aether" Version="$($guiBaseline.Version)" /></ItemGroup></Project>
"@)
        foreach ($name in @('NotoSans-Regular.ttf','OFL.txt')) { Copy-Item -LiteralPath "$guiWorkspace/Examples/AetherEditor/Assets/Fonts/$name" -Destination "$consumer/Gui/Assets/Fonts/$name" }
        $feed = [Security.SecurityElement]::Escape($guiBaseline.Feed)
        [IO.File]::WriteAllText("$consumer/NuGet.Config", @"
<configuration><packageSources><clear /><add key="fresh" value="$feed" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
<packageSourceMapping><packageSource key="fresh"><package pattern="TrueMoon" /><package pattern="TrueMoon.*" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping></configuration>
"@)
        [IO.File]::WriteAllText("$consumer/Consumer.slnx", '<Solution><Project Path="Gui/GuiConsumer.csproj" /><Project Path="Runtime/Runtime.csproj" /></Solution>')
        Invoke-Gui 'package-restore' @('restore','Consumer.slnx','--configfile',"$consumer/NuGet.Config","-bl:$guiEvidence/package-restore-{}.binlog") $consumer -package
        foreach ($name in @('Gui','Runtime')) {
            [xml]$consumerProjectXml = Get-Content -LiteralPath "$consumer/$name/$(if($name -eq 'Gui'){'GuiConsumer'}else{'Runtime'}).csproj" -Raw
            $assets = Get-Content -LiteralPath "$consumer/$name/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
            if ($consumerProjectXml.SelectNodes('//ProjectReference|//Compile[@Include]').Count -or @($assets.libraries.Values | Where-Object type -ne 'package').Count) { throw 'Consumer uses source/project dependencies.' }
            foreach ($key in @($assets.libraries.Keys | Where-Object { $_ -match '^TrueMoon(?:\.[^/]+)?/' })) {
                if (!$key.EndsWith('/' + $guiBaseline.Version, [StringComparison]::Ordinal)) { throw 'Stale package in GUI graph.' }
                $metadata = Get-Content -LiteralPath "$guiRun/cache/$($assets.libraries[$key].path)/.nupkg.metadata" -Raw | ConvertFrom-Json
                if ($metadata.source -ne $guiBaseline.Feed) { throw 'Wrong fresh framework feed.' }
            }
        }
        if ('PackageDebug' -in $profiles) { Invoke-Gui 'package-build-Debug' @('build','Consumer.slnx','-c','Debug','--no-restore','--verbosity','quiet',"-bl:$guiEvidence/package-build-Debug-{}.binlog") $consumer -package }
        if ('PackageRelease' -in $profiles) {
            foreach ($name in @('Gui','Runtime')) {
                $project = if ($name -eq 'Gui') { 'GuiConsumer' } else { 'Runtime' }; $command = "package-publish-$name-Release"
                Invoke-Gui $command @('publish',"$name/$project.csproj",'-c','Release','--no-restore','-o',"$guiRun/package-Release/$name",'--verbosity','quiet',"-bl:$guiEvidence/$command-{}.binlog") $consumer -package
            }
        }
    }
    foreach ($item in $profiles) {
        $gui = switch ($item) { 'SourceDebug' { "$PSScriptRoot/Gui/bin/Debug/net10.0" } 'SourceRelease' { "$guiRun/source-Release/Gui" } 'PackageDebug' { "$guiRun/consumer/Gui/bin/Debug/net10.0" } 'PackageRelease' { "$guiRun/package-Release/Gui" } }
        $runtime = switch ($item) { 'SourceDebug' { "$guiWorkspace/Examples/AetherRuntimeHost/bin/Debug/net10.0/AetherRuntimeHost.exe" } 'SourceRelease' { "$guiRun/source-Release/Runtime/AetherRuntimeHost.exe" } 'PackageDebug' { "$guiRun/consumer/Runtime/bin/Debug/net10.0/Runtime.exe" } 'PackageRelease' { "$guiRun/package-Release/Runtime/Runtime.exe" } }
        $hosts = if ($HostKind -eq 'All') { @('Standard','Embedded') } else { @($HostKind) }
        foreach ($hostMode in $hosts) {
            $scenarios = if ($Scenario -eq 'All') { @('Workflow','Failure','Cancel') } else { @($Scenario) }
            foreach ($caseMode in $scenarios) {
                $name = "$item-$hostMode-$caseMode"; $report = "$guiRun/cases/$name"
                $provider = if ($hostMode -eq 'Standard') { 'cobalt' } else { 'di' }
                Invoke-Gui $name @("$gui/GuiConsumer.dll", $runtime, $report, $hostMode.ToLowerInvariant(), $provider, $caseMode.ToLowerInvariant()) -package:($item -like 'Package*')
                $result = Get-Content -LiteralPath "$report/report.json" -Raw | ConvertFrom-Json
                if (!$result.Passed -or $result.ActiveUiSessions -ne 0 -or $result.GpuResources -ne 0 -or $result.GpuHandles -ne 0) { throw 'Case cleanup failed.' }
                $guiReport.Cases += [ordered]@{ Profile = $item; Host = $hostMode; Provider = $provider; Scenario = $caseMode; Report = "$report/report.json" }
                Save-GuiReport
            }
        }
    }
    foreach ($entry in @($guiReport.Sources + $guiReport.Validation + $guiBaseline.Packages)) { if ((Gui-Hash $entry.Path) -ne $entry.Sha256) { throw "Input drift: $($entry.Path)" } }
    $guiReport.Completed = $true
} catch { $guiReport.Failure = $_.ToString(); throw }
finally { Save-GuiReport; Write-Host "Evidence: $guiEvidence; external files: $guiRun" }
