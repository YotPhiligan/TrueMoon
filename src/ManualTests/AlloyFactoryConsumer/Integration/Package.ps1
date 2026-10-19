function Get-ProbePackageIds([string[]] $Projects) {
    foreach ($path in $Projects) {
        [xml] $project = Get-Content -LiteralPath $path -Raw
        if ($project.SelectSingleNode('//IsPackable')?.InnerText -eq 'false') { continue }
        $id = $project.SelectSingleNode('//PackageId')?.InnerText
        if (!$id) { $id = [IO.Path]::GetFileNameWithoutExtension($path) }
        if ($id -notmatch '^TrueMoon(?:\.[A-Za-z0-9.]+)?$') { throw "Unsupported package identity in $path : $id" }
        [pscustomobject]@{ Id = $id; Project = $path }
    }
}

function Assert-ProbePackageGraph([string] $Consumer, [string] $Cache, [string] $Feed, [string[]] $Ids, [string] $Version) {
    [xml] $project = Get-Content -LiteralPath (Join-Path $Consumer 'Consumer.csproj') -Raw
    if ($project.SelectNodes('//ProjectReference').Count -ne 0 -or $project.SelectSingleNode('//EnableCobaltGenerator').InnerText -ne 'false') {
        throw 'Consumer must use packages and disable the Cobalt analyzer.'
    }
    $assets = Get-Content -LiteralPath (Join-Path $Consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    if (@($assets.libraries.Values | Where-Object type -ne 'package').Count) { throw 'Consumer assets contain a project/source dependency.' }
    $local = @($assets.libraries.Keys | Where-Object { $_ -like 'TrueMoon/*' -or $_ -like 'TrueMoon.*/*' })
    $expected = @($Ids | ForEach-Object { "$_/$Version" })
    if (@(Compare-Object ($local | Sort-Object) ($expected | Sort-Object)).Count) { throw 'Consumer package graph/version differs from the fresh feed.' }
    foreach ($library in $local) {
        $metadata = Get-Content -LiteralPath (Join-Path $Cache ($assets.libraries[$library].path + '/.nupkg.metadata')) -Raw | ConvertFrom-Json
        if ($metadata.source -ne $Feed) { throw "Unexpected source for $library : $($metadata.source)" }
    }
}

function Get-ProbeZipHash([string] $Package, [string] $Entry) {
    $zip = [IO.Compression.ZipFile]::OpenRead($Package)
    try {
        $file = $zip.GetEntry($Entry)
        if (!$file -or $file.Length -eq 0) { throw "Missing or empty $Entry in $Package" }
        $stream = $file.Open()
        try { return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
        finally { $stream.Dispose() }
    } finally { $zip.Dispose() }
}

function Get-ProbeRequiredPayloads($Packages) {
    $required = @(
        @('TrueMoon.Cobalt', 'analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll'),
        @('TrueMoon.Cobalt', 'buildTransitive/TrueMoon.Cobalt.targets'),
        @('TrueMoon.Alloy.Rendering.Skia', 'buildTransitive/TrueMoon.Alloy.Rendering.Skia.targets'),
        @('TrueMoon.Alloy.Rendering.Skia', 'nativeAssets/win-x64/libSkiaSharp.dll')
    )
    foreach ($entry in $required) {
        $package = @($Packages | Where-Object Id -eq $entry[0])
        if ($package.Count -ne 1) { throw "Missing required package $($entry[0])" }
        [pscustomobject]@{ PackageId = $entry[0]; Entry = $entry[1]; Sha256 = (Get-ProbeZipHash $package[0].Path $entry[1]) }
    }
}

function Assert-ProbePackageDelivery($Manifest, [string] $Report, [string] $BinaryDirectory) {
    $result = Get-Content -LiteralPath $Report -Raw | ConvertFrom-Json
    foreach ($assembly in @($result.Assemblies | Where-Object { [IO.Path]::GetFileName($_) -like 'TrueMoon*.dll' })) {
        [void] (Get-ProbePath $assembly $BinaryDirectory)
        $id = [IO.Path]::GetFileNameWithoutExtension($assembly)
        $package = @($Manifest.Packages | Where-Object Id -eq $id)
        if ($package.Count -ne 1) { throw "Missing fresh package for loaded $id" }
        if ((Get-FileHash -LiteralPath $assembly).Hash -ne (Get-ProbeZipHash $package[0].Path "lib/net10.0/$id.dll")) {
            throw "Loaded assembly differs from its package: $id"
        }
    }
    $renderer = @($Manifest.Packages | Where-Object Id -eq 'TrueMoon.Alloy.Rendering.Skia')[0]
    $native = Join-Path $BinaryDirectory 'truemoon-native/win-x64/libSkiaSharp.dll'
    if ((Get-FileHash -LiteralPath $native).Hash -ne (Get-ProbeZipHash $renderer.Path 'nativeAssets/win-x64/libSkiaSharp.dll')) {
        throw 'Build/publish native Skia delivery differs from the fresh renderer package.'
    }
}

function Assert-ProbeNoAnalyzer([string] $Consumer, [string] $Configuration) {
    $path = Join-Path $Consumer "obj/$Configuration/net10.0/resolved-analyzers.txt"
    if (@(Get-Content -LiteralPath $path | Where-Object { $_ -like '*TrueMoon.Cobalt.Generator.dll' }).Count) {
        throw 'Factory consumer used a Cobalt analyzer.'
    }
}

function Invoke-ProbePackages($Context, [string[]] $Projects, [string] $Version, [int] $TimeoutSeconds) {
    $root = Join-Path $Context.Run 'package'; $feed = Join-Path $root 'feed'; $cache = Join-Path $root 'cache'
    $consumer = Join-Path $root 'consumer'; $publish = Join-Path $root 'publish'
    foreach ($directory in @($feed, $cache, $consumer)) { [IO.Directory]::CreateDirectory($directory) | Out-Null }
    $packages = @(Get-ProbePackageIds $Projects)
    $Context.Manifest.PackageVersion = $Version; $Context.Manifest.PackageIds = @($packages.Id)
    foreach ($package in $packages) {
        Invoke-ProbeCommand $Context ('pack-' + $package.Id) 'dotnet' @('pack', $package.Project, '--configuration', 'Release', "-p:Version=$Version", '--output', $feed, '--verbosity', 'quiet') $TimeoutSeconds
        $path = Join-Path $feed "$($package.Id).$Version.nupkg"
        $Context.Manifest.Packages += [pscustomobject]@{ Id = $package.Id; Path = $path; Sha256 = (Get-FileHash -LiteralPath $path).Hash }
        Add-ProbeArtifact $Context $path
    }
    if (@(Get-ChildItem -LiteralPath $feed -Filter '*.nupkg').Count -ne $packages.Count) { throw 'Unexpected package feed contents.' }
    $Context.Manifest.PackagePayloads = @(Get-ProbeRequiredPayloads $Context.Manifest.Packages)
    foreach ($name in @('Directory.Build.props', 'Directory.Build.targets')) { '<Project />' | Set-Content -LiteralPath (Join-Path $consumer $name) }
    '<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>' |
        Set-Content -LiteralPath (Join-Path $consumer 'Directory.Packages.props')
    $template = Join-Path $Context.Workspace 'ManualTests/AlloyFactoryConsumer/Integration/Consumer.csproj.template'
    [IO.File]::ReadAllText($template).Replace('__VERSION__', $Version) | Set-Content -LiteralPath (Join-Path $consumer 'Consumer.csproj')
    Copy-Item -LiteralPath (Join-Path $Context.Workspace 'ManualTests/AlloyFactoryConsumer/Program.cs') -Destination (Join-Path $consumer 'Program.cs')
    Copy-Item -LiteralPath (Join-Path $Context.Workspace 'global.json') -Destination (Join-Path $consumer 'global.json')
    $fallback = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $feedXml = [Security.SecurityElement]::Escape($feed)
    $fallbackXml = [Security.SecurityElement]::Escape([IO.Path]::GetFullPath($fallback))
    @"
<configuration>
  <packageSources><clear /><add key="fresh-local" value="$feedXml" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping>
    <packageSource key="fresh-local"><package pattern="TrueMoon" /><package pattern="TrueMoon.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
  <fallbackPackageFolders><clear /><add key="existing-third-party-cache" value="$fallbackXml" /></fallbackPackageFolders>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'NuGet.Config')
    $Context.Manifest.Consumer = [ordered]@{ Directory = $consumer; Cache = $cache; Feed = $feed; Publish = $publish }
    foreach ($file in Get-ChildItem -LiteralPath $consumer -File) { Add-ProbeArtifact $Context $file.FullName }
    $project = Join-Path $consumer 'Consumer.csproj'
    Invoke-ProbeCommand $Context 'package-restore' 'dotnet' @('restore', $project, '--packages', $cache, '--configfile', (Join-Path $consumer 'NuGet.Config'), '--verbosity', 'quiet') $TimeoutSeconds
    Assert-ProbePackageGraph $consumer $cache $feed $packages.Id $Version
    Add-ProbeArtifact $Context (Join-Path $consumer 'obj/project.assets.json')
    Invoke-ProbeCommand $Context 'package-build-Debug' 'dotnet' @('build', $project, '--configuration', 'Debug', '--no-restore', '--verbosity', 'quiet') $TimeoutSeconds
    Invoke-ProbeCommand $Context 'package-publish-Release' 'dotnet' @('publish', $project, '--configuration', 'Release', '--no-restore', '--output', $publish, '--verbosity', 'quiet') $TimeoutSeconds
    foreach ($configuration in @('Debug', 'Release')) {
        Assert-ProbeNoAnalyzer $consumer $configuration
        Add-ProbeArtifact $Context (Join-Path $consumer "obj/$configuration/net10.0/resolved-analyzers.txt")
        $directory = if ($configuration -eq 'Debug') { Join-Path $consumer 'bin/Debug/net10.0' } else { $publish }
        Add-ProbeBinaries $Context $directory
        foreach ($provider in @('cobalt', 'di')) { foreach ($graphics in @('opengl', 'vulkan')) {
            $name = "package-$configuration-$provider-$graphics"; $report = Join-Path $root "$name.json"
            Invoke-ProbeCommand $Context $name (Join-Path $directory 'Consumer.exe') @($provider, $graphics, 'factory', $report, '--validation') $TimeoutSeconds
            [void] (Assert-ProbeConsumer $report $provider $graphics $true)
            Assert-ProbePackageDelivery $Context.Manifest $report $directory
            Add-ProbeArtifact $Context $report
            $Context.Manifest.Cases += [pscustomobject]@{ Phase = 'Package'; Kind = 'Factory'; Name = $name; Report = $report
                Configuration = $configuration; Provider = $provider; Graphics = $graphics; BinaryDirectory = $directory }
        } }
    }
}
