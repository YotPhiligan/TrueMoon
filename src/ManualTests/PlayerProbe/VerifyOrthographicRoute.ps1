[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PlayerVerification,
    [Parameter(Mandatory)][string]$GameSourceDirectory,
    [Parameter(Mandatory)][string]$ValidationDirectory,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$ProbeAssembly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$verification = Get-Content -Raw -LiteralPath $PlayerVerification | ConvertFrom-Json
$game = [IO.Path]::GetFullPath($GameSourceDirectory)
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $evidence) { throw 'Use a fresh evidence directory.' }
if (!$verification.Completed -or !($verification.Graphs | Where-Object Project -eq 'probe')) {
    throw 'A completed Player package verification is required.'
}

$version = ([xml](Get-Content -Raw -LiteralPath "$game/Directory.Packages.props")).Project.PropertyGroup.GameTrueMoonVersion.InnerText
if ($verification.Version -ne $version) { throw "Game and Player package versions differ: $version and $($verification.Version)." }
$feed = Join-Path $verification.RunDirectory 'feed'
foreach ($package in $verification.Packages) {
    $path = Join-Path $feed "$($package.Id).$version.nupkg"
    if ((Get-FileHash -LiteralPath $path).Hash -ne $package.Sha256) { throw "Package digest changed: $($package.Id)." }
}

$source = Join-Path $evidence 'source'
[void][IO.Directory]::CreateDirectory($source)
$paths = @('global.json', 'Directory.Build.props', 'Directory.Packages.props')
foreach ($projectDirectory in @('TrueMoon.Tales.Build','TrueMoon.Tales.Gameplay')) {
    $paths += @(Get-ChildItem -LiteralPath "$game/$projectDirectory" -File |
        Where-Object Extension -in @('.csproj','.cs','.aetherscene') |
        ForEach-Object { "$projectDirectory/$($_.Name)" })
}
$sources = foreach ($relative in $paths) {
    $sourceFile = Join-Path $game $relative
    $target = Join-Path $source $relative
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $target))
    $bytes = [IO.File]::ReadAllBytes($sourceFile)
    [IO.File]::WriteAllBytes($target, $bytes)
    @{ Path = $relative; Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)) }
}
[IO.File]::WriteAllText("$source/NuGet.Config",
    '<configuration><packageSources><clear/><add key="verified" value="' +
    [Security.SecurityElement]::Escape($feed) + '"/></packageSources></configuration>')

function Invoke-Recorded([string]$name, [string[]]$arguments, [string]$directory = $source) {
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory = $directory
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.Environment['NUGET_PACKAGES'] = Join-Path $evidence 'cache'
    $info.Environment['NUGET_HTTP_CACHE_PATH'] = Join-Path $evidence 'http-cache'
    $info.Environment['VK_LAYER_PATH'] = [IO.Path]::GetFullPath($ValidationDirectory)
    foreach ($argument in $arguments) { [void]$info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    try {
        if (!$process.WaitForExit(240000)) { throw "$name timed out." }
        $output = $stdout.GetAwaiter().GetResult()
        [IO.File]::WriteAllText("$evidence/$name.stdout.log", $output)
        [IO.File]::WriteAllText("$evidence/$name.stderr.log", $stderr.GetAwaiter().GetResult())
        if ($process.ExitCode -ne 0) { throw "$name exited $($process.ExitCode). See $evidence/$name.stderr.log." }
        return $output
    }
    finally {
        if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}

$tool = 'TrueMoon.Tales.Build/TrueMoon.Tales.Build.csproj'
$project = 'TrueMoon.Tales.Gameplay/TrueMoon.Tales.Gameplay.csproj'
Invoke-Recorded 'restore-tool' @('restore', $tool, '--configfile', "$source/NuGet.Config", '--no-http-cache', '-p:NuGetAudit=false', '-v', 'quiet') | Out-Null
Invoke-Recorded 'restore-game' @('restore', $project, '--configfile', "$source/NuGet.Config", '--no-http-cache', '-p:NuGetAudit=false', '-v', 'quiet') | Out-Null
Invoke-Recorded 'publish-tool' @('publish', $tool, '--no-restore', '-c', 'Release', '-o', "$evidence/tool", '-v', 'quiet') | Out-Null

$graphs = foreach ($name in @('TrueMoon.Tales.Build','TrueMoon.Tales.Gameplay')) {
    $path = Join-Path $source "$name/obj/project.assets.json"
    $assets = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json -AsHashtable
    $localProjects = 0
    foreach ($library in $assets.libraries.GetEnumerator()) {
        if ($library.Value.type -eq 'project' -and $name -eq 'TrueMoon.Tales.Build' -and
            $library.Key.StartsWith('TrueMoon.Tales.Gameplay/') -and
            [IO.Path]::GetFullPath($library.Value.path, "$source/$name") -eq
                [IO.Path]::GetFullPath("$source/TrueMoon.Tales.Gameplay/TrueMoon.Tales.Gameplay.csproj")) {
            $localProjects++
            continue
        }
        if ($library.Value.type -ne 'package' -or ($library.Key -match '^TrueMoon(?:\.|/)' -and !$library.Key.EndsWith("/$version"))) {
            throw "Unexpected library in $name graph: $($library.Key)."
        }
    }
    @{ Name = $name; ProjectReferences = $localProjects; Sha256 = (Get-FileHash -LiteralPath $path).Hash }
}

$verifiedProbe = Join-Path $verification.RunDirectory 'fresh-published/probe/PlayerProbe.dll'
$player = if ($ProbeAssembly) { (Resolve-Path -LiteralPath $ProbeAssembly).Path } else { $verifiedProbe }
foreach ($binary in @('TrueMoon.Aether.Abstractions.dll','TrueMoon.Aether.Runtime.dll','TrueMoon.Aether.Player.dll')) {
    $published = Join-Path (Split-Path -Parent $player) $binary
    $verified = Join-Path (Split-Path -Parent $verifiedProbe) $binary
    if ((Get-FileHash -LiteralPath $published).Hash -ne (Get-FileHash -LiteralPath $verified).Hash) {
        throw "Player uses a different package payload: $binary."
    }
}
$build = Invoke-Recorded 'build-route' @("$evidence/tool/TrueMoon.Tales.Build.dll", $player, (Join-Path $source $project), "$evidence/build") | ConvertFrom-Json
$playback = Join-Path $evidence 'playback'
[void][IO.Directory]::CreateDirectory($playback)
Copy-Item -LiteralPath $build.DirectoryPath -Destination "$playback/$($build.BuildId)" -Recurse
Copy-Item -LiteralPath $build.ScenePath -Destination "$playback/IsometricRoute.aetherscene"
Invoke-Recorded 'player-route' @($player, '--orthographic-route', "$playback/$($build.BuildId)", "$playback/IsometricRoute.aetherscene",
    "$evidence/frames") $playback | Out-Null

$frames = Get-Content -Raw -LiteralPath "$evidence/frames/verification.json" | ConvertFrom-Json
if (!$frames.Completed -or $frames.BuildId -ne $build.BuildId) { throw 'The Player did not present the expected route build.' }
[ordered]@{
    Completed = $true; FrameworkVersion = $version; Build = $build; Sources = @($sources); Graphs = @($graphs);
    GameVersion = ([xml](Get-Content -Raw -LiteralPath "$source/Directory.Build.props")).Project.PropertyGroup.Version.InnerText;
    ProbeSha256 = (Get-FileHash -LiteralPath $player).Hash;
    PackageVerificationSha256 = (Get-FileHash -LiteralPath $PlayerVerification).Hash;
    FrameVerificationSha256 = (Get-FileHash -LiteralPath "$evidence/frames/verification.json").Hash
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8
Write-Host "Isometric route Vulkan proof: $evidence"
