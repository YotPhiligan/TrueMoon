function Get-TrueMoonBuildProfile {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Workspace, [string]$EvidenceDirectory)
    $ErrorActionPreference = 'Stop'
    $root = [IO.Path]::GetFullPath($Workspace)
    if (!$EvidenceDirectory) { $EvidenceDirectory = Join-Path $root 'TestResults/BuildProfiles' }
    [void][IO.Directory]::CreateDirectory($EvidenceDirectory)
    $stem = Join-Path $EvidenceDirectory ('profile-' + [Guid]::NewGuid().ToString('N'))
    $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory = $root; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in @('msbuild', 'TrueMoon.Contracts/TrueMoon.Contracts.csproj', '-nologo',
        '-getProperty:TF,TestsTF,Version,NETCoreSdkVersion,LangVersion,Features', "-bl:$stem-{}.binlog")) {
        [void]$info.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::Start($info)
    $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
    try {
        if (!$process.WaitForExit(90000)) { $process.Kill($true); $process.WaitForExit(); throw 'Build-profile evaluation timed out.' }
        $output = $stdout.GetAwaiter().GetResult(); $errorOutput = $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText("$stem.stdout.log", $output)
        [IO.File]::WriteAllText("$stem.stderr.log", $errorOutput)
        if ($process.ExitCode -ne 0) { throw "Build-profile evaluation failed; see $stem.stdout.log." }
        if (!(Get-ChildItem -LiteralPath $EvidenceDirectory -Filter (([IO.Path]::GetFileName($stem))+'-*.binlog'))) {
            throw 'Build-profile binlog is missing.'
        }
        $properties = ($output | ConvertFrom-Json).Properties
        foreach ($name in @('TF','TestsTF','Version','NETCoreSdkVersion','LangVersion')) {
            if ([string]::IsNullOrWhiteSpace($properties.$name) -or $properties.$name.Contains('$(')) {
                throw "Build-profile property $name is empty or unresolved."
            }
        }
        $helper = (Get-Command Get-TrueMoonBuildProfile).ScriptBlock.File
        $sources = foreach ($file in @('Directory.Build.props','Directory.Packages.props','global.json')) {
            $path = Join-Path $root $file
            [pscustomobject]@{ Path=$path; Sha256=(Get-FileHash -LiteralPath $path).Hash }
        }
        $sources += [pscustomobject]@{ Path=$helper; Sha256=(Get-FileHash -LiteralPath $helper).Hash }
        $profile = [pscustomobject]@{
            Workspace=$root; Framework=[string]$properties.TF; TestFramework=[string]$properties.TestsTF
            Sdk=[string]$properties.NETCoreSdkVersion; Version=[string]$properties.Version
            LanguageVersion=[string]$properties.LangVersion; Features=[string]$properties.Features
            Sources=@($sources); EvidencePath="$stem.json"
        }
        $profile | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $profile.EvidencePath -Encoding utf8
        return $profile
    } finally {
        if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}

function Write-TrueMoonConsumerBuildProfile {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Profile, [Parameter(Mandatory)][string]$Directory)
    $ErrorActionPreference = 'Stop'
    [void][IO.Directory]::CreateDirectory($Directory)
    $escape = { param([string]$value) [Security.SecurityElement]::Escape($value) }
    $framework = & $escape $Profile.Framework; $tests = & $escape $Profile.TestFramework
    $language = & $escape $Profile.LanguageVersion; $features = & $escape $Profile.Features
    $props = @"
<Project>
  <PropertyGroup>
    <TF>$framework</TF><TestsTF>$tests</TestsTF><TargetFramework>`$(TF)</TargetFramework>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><IsPackable>false</IsPackable>
    <LangVersion>$language</LangVersion><Features>$features</Features><NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
</Project>
"@
    [IO.File]::WriteAllText((Join-Path $Directory 'Directory.Build.props'), $props)
    @{sdk=@{version=$Profile.Sdk;allowPrerelease=$false;rollForward='disable'}} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Directory 'global.json') -Encoding utf8
    [IO.File]::WriteAllText((Join-Path $Directory 'Directory.Packages.props'),
        '<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>')
}

function Export-TrueMoonConsumerFrameworks {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Profile, [Parameter(Mandatory)][string]$Directory)
    $ErrorActionPreference = 'Stop'
    $path = Join-Path $Directory 'Directory.Build.props'
    $document = [xml]::new(); $document.PreserveWhitespace = $true
    $document.Load($path)
    $group = $document.CreateElement('PropertyGroup')
    foreach ($entry in @(@('TF',$Profile.Framework),@('TestsTF',$Profile.TestFramework))) {
        $node = $document.CreateElement($entry[0]); $node.InnerText = $entry[1]; [void]$group.AppendChild($node)
    }
    [void]$document.DocumentElement.PrependChild($group)
    [IO.File]::WriteAllText($path, $document.OuterXml)
    @{sdk=@{version=$Profile.Sdk;allowPrerelease=$false;rollForward='disable'}} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Directory 'global.json') -Encoding utf8
}
