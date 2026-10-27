[CmdletBinding()]
param([string]$RunDirectory)

# Windows x64, PowerShell 7, framework-dependent net10.0. No window, device or GPU calls.
$ErrorActionPreference = 'Stop'
$deploymentWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$deploymentRun = if ($RunDirectory) { [IO.Path]::GetFullPath($RunDirectory) } else {
    Join-Path ([IO.Path]::GetTempPath()) ('TrueMoon-ExampleHosts-' + [Guid]::NewGuid().ToString('N'))
}
if ($deploymentRun.TrimEnd('\', '/') -eq $deploymentWorkspace.TrimEnd('\', '/') -or
    $deploymentRun.StartsWith($deploymentWorkspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use a fresh RunDirectory outside the source checkout.'
}
if (Test-Path -LiteralPath $deploymentRun) { throw 'Use a fresh RunDirectory.' }
if (-not [OperatingSystem]::IsWindows() -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') { throw 'Windows x64 is required.' }
$deploymentDotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
$deploymentCwd = Join-Path $deploymentRun 'unrelated working directory'
[IO.Directory]::CreateDirectory($deploymentCwd) | Out-Null
$deploymentSummary = [ordered]@{
    Completed = $false; RunDirectory = $deploymentRun; GpuRun = $false; WindowRun = $false
    IndependentNuGetConsumer = $false; Commands = @(); Resources = @(); Cases = @(); SourceHashes = @(); Failure = $null
}

function Invoke-DeploymentProcess([string]$name, [string]$file, [string[]]$arguments,
    [string]$workingDirectory = $deploymentCwd, [int]$expectedExit = 0, [int]$timeoutSeconds = 180) {
    $info = [Diagnostics.ProcessStartInfo]::new($file)
    $info.WorkingDirectory = $workingDirectory; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.Environment.Remove('TM_ENGINE_BOOTSTRAP') | Out-Null
    foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
    $timedOut = $false
    try {
        if (-not $process.WaitForExit($timeoutSeconds * 1000)) {
            $timedOut = $true; $process.Kill($true); $process.WaitForExit()
        }
        $outText = $stdout.GetAwaiter().GetResult(); $errorText = $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $deploymentRun "$name.stdout.log"), $outText)
        [IO.File]::WriteAllText((Join-Path $deploymentRun "$name.stderr.log"), $errorText)
        $deploymentSummary.Commands += [ordered]@{
            Name = $name; FileName = $file; Arguments = $arguments; WorkingDirectory = $workingDirectory
            ExitCode = $process.ExitCode; ExpectedExitCode = $expectedExit; TimedOut = $timedOut
        }
        if ($timedOut) { throw "$name timed out; owned process tree was terminated." }
        if ($process.ExitCode -ne $expectedExit) { throw "$name returned $($process.ExitCode), expected $expectedExit. See stderr/stdout logs." }
        if ($arguments[0] -in 'build', 'publish' -and -not (Get-ChildItem -LiteralPath $deploymentRun -Filter "$name-*.binlog")) {
            throw "$name produced no binlog."
        }
        Write-Host "$name passed"
        return $outText + $errorText
    }
    finally {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}

function Assert-DeploymentFile([string]$deploymentProfile, [string]$path, [string]$expectedSource) {
    $item = Get-Item -LiteralPath $path
    if ($item.Length -eq 0) { throw "Empty deployment resource: $path" }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($expectedSource -and $hash -ne (Get-FileHash -LiteralPath $expectedSource -Algorithm SHA256).Hash) {
        throw "Deployment resource differs from its owner: $path"
    }
    $deploymentSummary.Resources += [ordered]@{ Profile = $deploymentProfile; Path = $path; Bytes = $item.Length; Sha256 = $hash; Source = $expectedSource }
}

function Assert-ManagedResources([string]$deploymentProfile, [string]$directory) {
    foreach ($assembly in @(
        @{ File = 'TrueMoon.Aether.Editor.dll'; Type = 'EditorViewStyle' },
        @{ File = 'TrueMoon.Argentis.dll'; Type = 'Theme' },
        @{ File = 'TrueMoon.Aether.dll'; Type = $null }
    )) {
        $stream = [IO.File]::OpenRead((Join-Path $directory $assembly.File))
        try {
            $pe = [Reflection.PortableExecutable.PEReader]::new($stream)
            try {
                $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
                if ($assembly.Type) {
                    $names = @($metadata.TypeDefinitions | ForEach-Object { $metadata.GetString($metadata.GetTypeDefinition($_).Name) })
                    if ($assembly.Type -notin $names) { throw "Missing managed resource owner: $($assembly.Type)" }
                }
                else {
                    [byte[]]$blob = $pe.GetSectionData($pe.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress).GetContent()
                    foreach ($shader in @('cube.vert.spv', 'cube.frag.spv')) {
                        $resource = @($metadata.ManifestResources | ForEach-Object { $metadata.GetManifestResource($_) } |
                            Where-Object { $metadata.GetString($_.Name) -eq "TrueMoon.Aether.Gpu.Vulkan.Shaders.$shader" })
                        if ($resource.Count -ne 1 -or -not $resource[0].Implementation.IsNil) { throw "Missing embedded shader: $shader" }
                        $offset = [int]$resource[0].Offset; $length = [BitConverter]::ToInt32($blob, $offset)
                        [byte[]]$bytes = $blob[($offset + 4)..($offset + 3 + $length)]
                        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
                        $source = Join-Path $deploymentWorkspace "TrueMoon.Aether/Gpu/Vulkan/Shaders/$shader"
                        if ($length -le 4 -or [BitConverter]::ToUInt32($bytes, 0) -ne 0x07230203 -or
                            $hash -ne (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash) { throw "Invalid embedded shader: $shader" }
                        $deploymentSummary.Resources += [ordered]@{ Profile = $deploymentProfile; Path = "$directory/$($assembly.File):$shader"; Bytes = $length; Sha256 = $hash; Source = $source }
                    }
                }
            }
            finally { $pe.Dispose() }
        }
        finally { $stream.Dispose() }
    }
}

function Assert-DependencyClosure([string]$directory, [string]$entry, [switch]$runtime) {
    $deps = Get-Content -LiteralPath (Join-Path $directory "$entry.deps.json") -Raw | ConvertFrom-Json
    $names = @($deps.libraries.PSObject.Properties.Name | ForEach-Object { ($_ -split '/')[0] })
    $forbidden = @('EngineSessionProbe', 'Game.Logic', 'AetherEditorHost')
    if ($runtime) { $forbidden += @('AetherEditor', 'TrueMoon.Aether.Editor', 'TrueMoon.Argentis', 'SkiaSharp') }
    foreach ($name in $names) {
        if ($name -in $forbidden -or $name -match 'Tests$|ManualTests' -or $runtime -and $name -match '^TrueMoon\.Alloy|^Silk\.NET\.(OpenGL|Windowing|GLFW|Input)') {
            throw "Unexpected dependency in ${entry}: $name"
        }
    }
    foreach ($required in @('TrueMoon', 'TrueMoon.Aether')) {
        if ($required -notin $names) { throw "Missing dependency in ${entry}: $required" }
    }
    if (-not $runtime -and 'TrueMoon.Aether.Editor' -notin $names) { throw 'Editor module missing from editor closure.' }
    if ($runtime -and (Test-Path -LiteralPath (Join-Path $directory 'Assets/Fonts'))) { throw 'Runtime unexpectedly owns fonts.' }
}

try {
    $inputs = @('Examples/AetherEditor/Program.cs', 'Examples/AetherEditor/AetherEditor.csproj',
        'Examples/AetherEditor/Assets/Fonts/NotoSans-Regular.ttf', 'Examples/AetherEditor/Assets/Fonts/OFL.txt',
        'Examples/AetherRuntimeHost/Program.cs', 'Examples/AetherRuntimeHost/AetherRuntimeHost.csproj',
        'TrueMoon.Aether/TrueMoon.Aether.csproj', 'TrueMoon.Aether/Gpu/Vulkan/Shaders/cube.vert.spv',
        'TrueMoon.Aether/Gpu/Vulkan/Shaders/cube.frag.spv', 'TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj',
        'TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.dll')
    $before = @{}
    foreach ($inputPath in $inputs) { $before[$inputPath] = (Get-FileHash -LiteralPath (Join-Path $deploymentWorkspace $inputPath)).Hash }
    $projects = @(
        @{ Name = 'editor'; Project = 'Examples/AetherEditor/AetherEditor.csproj'; Entry = 'AetherEditor' },
        @{ Name = 'runtime'; Project = 'Examples/AetherRuntimeHost/AetherRuntimeHost.csproj'; Entry = 'AetherRuntimeHost' },
        @{ Name = 'probe'; Project = 'ManualTests/EngineSessionProbe/EngineSessionProbe.csproj'; Entry = 'EngineSessionProbe' }
    )
    foreach ($project in $projects) {
        $name = "build-$($project.Name)-Debug"
        $null = Invoke-DeploymentProcess $name $deploymentDotnet @('build', $project.Project, '-c', 'Debug', '--verbosity', 'quiet', "-bl:$deploymentRun/$name-{}.binlog") $deploymentWorkspace
        $name = "publish-$($project.Name)-Release"
        $null = Invoke-DeploymentProcess $name $deploymentDotnet @('publish', $project.Project, '-c', 'Release', '-o', "$deploymentRun/published/$($project.Name)", '--verbosity', 'quiet', "-bl:$deploymentRun/$name-{}.binlog") $deploymentWorkspace
    }
    $sourceEditor = Join-Path $deploymentWorkspace 'Examples/AetherEditor/bin/Debug/net10.0'
    foreach ($deploymentProfile in @('source-Debug', 'published-Release')) {
        $editor = if ($deploymentProfile -eq 'source-Debug') { $sourceEditor } else { "$deploymentRun/published/editor" }
        $runtime = if ($deploymentProfile -eq 'source-Debug') { Join-Path $deploymentWorkspace 'Examples/AetherRuntimeHost/bin/Debug/net10.0' } else { "$deploymentRun/published/runtime" }
        $probe = if ($deploymentProfile -eq 'source-Debug') { Join-Path $deploymentWorkspace 'ManualTests/EngineSessionProbe/bin/Debug/net10.0' } else { "$deploymentRun/published/probe" }
        Assert-DependencyClosure $editor 'AetherEditor'
        Assert-DependencyClosure $runtime 'AetherRuntimeHost' -runtime
        foreach ($name in @('NotoSans-Regular.ttf', 'OFL.txt')) {
            Assert-DeploymentFile $deploymentProfile "$editor/Assets/Fonts/$name" (Join-Path $deploymentWorkspace "Examples/AetherEditor/Assets/Fonts/$name")
        }
        if ((Get-Content -LiteralPath "$editor/Assets/Fonts/OFL.txt" -Raw) -notmatch 'SIL OPEN FONT LICENSE Version 1.1') { throw 'Missing font license.' }
        Assert-DeploymentFile $deploymentProfile "$editor/truemoon-native/win-x64/libSkiaSharp.dll" (Join-Path $deploymentWorkspace 'TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64/libSkiaSharp.dll')
        foreach ($name in @('glfw3.dll', 'libHarfBuzzSharp.dll', 'libSkiaSharp.dll')) {
            Assert-DeploymentFile $deploymentProfile "$editor/runtimes/win-x64/native/$name" "$sourceEditor/runtimes/win-x64/native/$name"
        }
        Assert-ManagedResources $deploymentProfile $editor
        # The runtime shares Aether's embedded shaders without loading the Editor assembly.
        Assert-DeploymentFile $deploymentProfile "$runtime/TrueMoon.Aether.dll" "$editor/TrueMoon.Aether.dll"
        $text = Invoke-DeploymentProcess "$deploymentProfile-help" "$editor/AetherEditor.exe" @('--help')
        if ($text -notmatch 'AetherEditor --runtime') { throw 'Missing editor CLI help.' }
        foreach ($mode in @('missing-runtime', 'unknown', 'missing-value', 'duplicate', 'invalid-project')) {
            $arguments = switch ($mode) {
                'missing-runtime' { @('--report', "$deploymentRun/must-not-exist") }
                'unknown' { @('--unknown') }
                'missing-value' { @('--runtime') }
                'duplicate' { @('--runtime', "$runtime/AetherRuntimeHost.exe", '--runtime', "$runtime/AetherRuntimeHost.exe") }
                'invalid-project' { @('--runtime', "$runtime/AetherRuntimeHost.exe", '--project', 'invalid.txt') }
            }
            $expectedMessage = switch ($mode) {
                'missing-runtime' { 'Select the runtime apphost explicitly.' }
                'unknown' { "Unknown argument '--unknown'." }
                'missing-value' { '--runtime requires a value.' }
                'duplicate' { "Duplicate argument '--runtime'." }
                'invalid-project' { '--project requires a .aetherproj manifest.' }
            }
            $text = Invoke-DeploymentProcess "$deploymentProfile-editor-$mode" "$editor/AetherEditor.exe" $arguments -expectedExit 1
            if (-not $text.Contains($expectedMessage)) { throw "Wrong CLI rejection for ${mode}: $text" }
        }
        if (Test-Path -LiteralPath "$deploymentRun/must-not-exist") { throw 'CLI rejection created App reports.' }
        $text = Invoke-DeploymentProcess "$deploymentProfile-runtime-invalid-cli" "$runtime/AetherRuntimeHost.exe" @('--unknown') -expectedExit 2
        if (-not $text.Contains('Usage: AetherRuntimeHost')) { throw 'Wrong runtime CLI rejection.' }
        $text = Invoke-DeploymentProcess "$deploymentProfile-runtime-no-bootstrap" "$runtime/AetherRuntimeHost.exe" @() -expectedExit 1
        if (-not $text.Contains('Missing or oversized runtime bootstrap')) { throw 'Wrong bootstrap rejection.' }
        foreach ($extension in @('dll', 'exe')) {
            $caseDirectory = "$deploymentRun/$deploymentProfile-$extension"
            $null = Invoke-DeploymentProcess "$deploymentProfile-$extension" $deploymentDotnet @("$probe/EngineSessionProbe.dll", '--runtime-launch-proof', "$runtime/AetherRuntimeHost.$extension", $caseDirectory)
            $report = Get-Content -LiteralPath "$caseDirectory/runtime-launch-report.json" -Raw | ConvertFrom-Json
            if (-not $report.Passed -or $report.Generations.Count -ne 3 -or $report.Cases.Count -ne 5 -or
                $report.RuntimeDirectoryHasGameFile -or $report.GameLoadedInParent) { throw 'Incomplete runtime proof.' }
            foreach ($fault in $report.Cases) {
                if (-not $fault.Passed -or -not $fault.Reaped -or -not $fault.ResourcesReleased) { throw 'Runtime fault cleanup failed.' }
            }
            foreach ($generation in $report.Generations) {
                if (-not $generation.Reaped -or -not $generation.ResourcesReleased -or $generation.Metadata.EditorLoaded -or
                    $generation.Metadata.UiLoaded -or $generation.Metadata.Resources -ne 0 -or $generation.Metadata.Handles -ne 0) { throw 'Runtime isolation/cleanup failed.' }
            }
            $deploymentSummary.Cases += [ordered]@{ Profile = $deploymentProfile; Extension = $extension; Passed = $report.Passed; Report = "$caseDirectory/runtime-launch-report.json" }
        }
    }
    $deploymentSummary.Completed = $true
}
catch { $deploymentSummary.Failure = $_.ToString(); throw }
finally {
    foreach ($inputPath in $before.Keys) {
        $after = (Get-FileHash -LiteralPath (Join-Path $deploymentWorkspace $inputPath)).Hash
        $deploymentSummary.SourceHashes += [ordered]@{ Path = $inputPath; Before = $before[$inputPath]; After = $after; Unchanged = $before[$inputPath] -eq $after }
        if ($before[$inputPath] -ne $after) { $deploymentSummary.Completed = $false; $deploymentSummary.Failure = "Source changed during verification: $inputPath" }
    }
    $deploymentSummary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $deploymentRun 'verification.json') -Encoding utf8
    Write-Host "Evidence: $deploymentRun/verification.json"
}
if (-not $deploymentSummary.Completed) { throw $deploymentSummary.Failure }
