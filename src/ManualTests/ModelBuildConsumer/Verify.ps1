#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../BuildProfile.ps1')
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory, $workspace)
if (Test-Path -LiteralPath $evidence) { throw 'Use a fresh evidence directory.' }
[void][IO.Directory]::CreateDirectory($evidence)
$profile = Get-TrueMoonBuildProfile -Workspace $workspace -EvidenceDirectory $evidence
$dotnet = (Get-Command dotnet).Source
$version = $profile.Version
$roots = @('TrueMoon', 'TrueMoon.Extensions.DependencyInjection')
$packages = @('TrueMoon.Contracts', 'TrueMoon.Core', 'TrueMoon.Cobalt', 'TrueMoon',
    'TrueMoon.Extensions.DependencyInjection', 'TrueMoon.Aether.Abstractions',
    'TrueMoon.Enerit.Generator', 'TrueMoon.Enerit', 'TrueMoon.Mithril',
    'TrueMoon.Aether.Runtime', 'TrueMoon.Aether.Tooling', 'TrueMoon.Nom', 'TrueMoon.Aether.Nom')
$worker = "$evidence/worker"
$feed = "$evidence/feed"
[void][IO.Directory]::CreateDirectory($feed)
$report = [ordered]@{
    Completed = $false
    Stage = 'ARCH1g.2a'
    Profile = $profile
    Runs = @()
    Packages = @()
    ExternalPackages = @()
    Commands = @()
}

function Invoke-DotnetStep([string]$Name, [string[]]$Arguments) {
    if ($Arguments[0] -in @('build', 'publish', 'pack', 'restore')) { $Arguments += "-bl:$evidence/$Name-{}.binlog" }
    & $dotnet @Arguments *> "$evidence/$Name.log"
    $code = $LASTEXITCODE
    $report.Commands += @{ Name = $Name; Arguments = $Arguments; ExitCode = $code }
    if ($code -ne 0) {
        Get-Content -LiteralPath "$evidence/$Name.log" -Tail 20
        throw "$Name exited with code $code"
    }
    Write-Host "$Name passed"
}

function Test-Consumers([string]$ProfileName, [string]$Authoring, [string]$Runtime) {
    foreach ($provider in @('cobalt', 'microsoft')) {
        $name = "$ProfileName-$provider"
        $directory = "$evidence/$name"
        Invoke-DotnetStep $name @($Authoring, $provider, $directory, $worker, $Runtime, $dotnet, $profile.TestFramework, $profile.Sdk)
        $path = "$directory/report.json"
        $proof = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if (!$proof.Completed -or $proof.Old.Meshes[0].VertexX -ne 1 -or $proof.New.Meshes[0].VertexX -ne 2) { throw 'Unexpected Runtime report.' }
        $report.Runs += @{ Name = $name; Path = $path; Sha256 = (Get-FileHash -LiteralPath $path).Hash; OldBuild = $proof.OldBuild; NewBuild = $proof.NewBuild }
    }
}

function New-PackageConsumer([string]$Directory, [string]$AssemblyName, [string]$FeaturePackage) {
    [void][IO.Directory]::CreateDirectory($Directory)
    $xml = [xml]'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName></AssemblyName></PropertyGroup><ItemGroup/></Project>'
    $xml.Project.PropertyGroup.AssemblyName = $AssemblyName
    foreach ($id in @($roots + $FeaturePackage)) {
        $node = $xml.CreateElement('PackageReference')
        $node.SetAttribute('Include', $id)
        $node.SetAttribute('Version', "[$version]")
        [void]$xml.Project.LastChild.AppendChild($node)
    }
    $project = "$Directory/Consumer.csproj"
    $xml.Save($project)
    return $project
}

Push-Location $workspace
try {
    Invoke-DotnetStep 'worker' @('publish', 'TrueMoon.Aether.Importing.Assimp/TrueMoon.Aether.Importing.Assimp.csproj',
        '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-p:UseAppHost=false', '-o', $worker, '-v:q')
    $report.NativeSha256 = (Get-FileHash -LiteralPath "$worker/assimp.dll").Hash
    if ($report.NativeSha256 -ne '90984E886CF7394560E44FECD5A5666BECD2A93E98251974A562ECB79CFBA386') { throw 'Native worker drift.' }
    Invoke-DotnetStep 'source-authoring' @('build', "$PSScriptRoot/Authoring/Authoring.csproj", '-c', 'Debug', '-o', "$evidence/source-authoring", '-v:q')
    Invoke-DotnetStep 'source-runtime' @('build', "$PSScriptRoot/Runtime/Runtime.csproj", '-c', 'Debug', '-o', "$evidence/source-runtime", '-v:q')
    Test-Consumers 'source-debug' "$evidence/source-authoring/Authoring.dll" "$evidence/source-runtime/ModelBuildRuntimeProbe.dll"

    foreach ($id in $packages) {
        $project = "$workspace/$id/$id.csproj"
        Invoke-DotnetStep "pack-$id" @('pack', $project, '-c', 'Release', '-o', $feed, '-v:q')
        $archive = "$feed/$id.$version.nupkg"
        $report.Packages += @{ Id = $id; Version = $version; Sha256 = (Get-FileHash -LiteralPath $archive).Hash }
    }
    $external = @{}
    foreach ($project in @($packages + 'TrueMoon.Cobalt.Generator')) {
        $assets = Get-Content -LiteralPath "$workspace/$project/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
        foreach ($library in $assets.libraries.GetEnumerator()) {
            if ($library.Value.type -ne 'package' -or $external.ContainsKey($library.Key)) { continue }
            $parts = $library.Key.Split('/')
            $file = ($parts[0] + '.' + $parts[1] + '.nupkg').ToLowerInvariant()
            $archive = $null
            foreach ($folder in $assets.packageFolders.Keys) {
                $candidate = Join-Path (Join-Path $folder $library.Value.path) $file
                if (Test-Path -LiteralPath $candidate) { $archive = $candidate; break }
            }
            if (!$archive) { throw "Missing external archive $($library.Key)" }
            Copy-Item -LiteralPath $archive -Destination $feed
            $external[$library.Key] = $true
            $report.ExternalPackages += @{ Id = $library.Key; Sha256 = (Get-FileHash -LiteralPath $archive).Hash }
        }
    }
    $fresh = "$evidence/fresh"
    Write-TrueMoonConsumerBuildProfile -Profile $profile -Directory $fresh
    $escapedFeed = [Security.SecurityElement]::Escape($feed)
    $config = "$fresh/NuGet.Config"
    [IO.File]::WriteAllText($config, "<configuration><packageSources><clear/><add key='closed' value='$escapedFeed'/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders></configuration>")
    $authoring = New-PackageConsumer "$fresh/Authoring" 'Authoring' 'TrueMoon.Aether.Nom'
    $runtime = New-PackageConsumer "$fresh/Runtime" 'ModelBuildRuntimeProbe' 'TrueMoon.Aether.Runtime'
    Copy-Item -LiteralPath "$PSScriptRoot/Authoring/Program.cs", "$PSScriptRoot/../ModelImportResearch/Fixtures.cs" -Destination "$fresh/Authoring"
    Copy-Item -LiteralPath "$PSScriptRoot/Runtime/Program.cs" -Destination "$fresh/Runtime"
    Push-Location $fresh
    try {
        foreach ($project in @($authoring, $runtime)) {
            $directory = Split-Path -Parent $project
            $name = Split-Path -Leaf $directory
            Invoke-DotnetStep "fresh-$name-restore" @('restore', $project, '--configfile', $config, '--packages', "$fresh/cache", '-v:q')
            $assets = Get-Content -LiteralPath "$directory/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
            if ($assets.packageFolders.Count -ne 1 -or @($assets.libraries.Values | Where-Object type -ne 'package').Count -ne 0) { throw 'Fresh package graph is not isolated.' }
            foreach ($id in $assets.libraries.Keys) {
                if ($id -like 'TrueMoon*' -and !$id.EndsWith("/$version")) { throw "Wrong TrueMoon version $id" }
                if ($name -eq 'Runtime' -and $id -match '^(TrueMoon\.(Nom|Aether\.(Tooling|Nom))|AssimpNetter)/') { throw "Authoring dependency in Runtime: $id" }
            }
            Invoke-DotnetStep "fresh-$name-publish" @('publish', $project, '--no-restore', '-c', 'Release', '--self-contained', 'false', '-p:UseAppHost=false', '-o', "$fresh/$name-output", '-v:q')
        }
        Test-Consumers 'fresh-release' "$fresh/Authoring-output/Authoring.dll" "$fresh/Runtime-output/ModelBuildRuntimeProbe.dll"
    } finally { Pop-Location }
    $report.Completed = $true
} finally {
    Pop-Location
    $report | ConvertTo-Json -Depth 18 | Set-Content -LiteralPath "$evidence/verification.json" -Encoding utf8
}
