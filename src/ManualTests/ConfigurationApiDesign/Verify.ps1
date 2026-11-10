# Type-checks the proposed declarations and three consumers; never executes them.
[CmdletBinding()]
param([string]$OutputDirectory)

$ErrorActionPreference = 'Stop'
$repoPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputPath = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else {
    Join-Path $repoPath ('TestResults/ConfigurationApi-' + [Guid]::NewGuid().ToString('N'))
}
if (Test-Path -LiteralPath $outputPath) { throw 'Use a fresh API verification directory.' }
[void][IO.Directory]::CreateDirectory($outputPath)
$utf8 = [Text.UTF8Encoding]::new($false)
$documentPath = Join-Path $repoPath 'docs/core/CONFIGURATION_API.md'
$body = [IO.File]::ReadAllText($documentPath, $utf8)
$blocks = @([regex]::Matches($body, '(?s)```csharp\r?\n// api: ([^\r\n]+)\r?\n(.*?)```'))
$expected = @('declarations', 'headless', 'mithril', 'editor')
if ($blocks.Count -ne $expected.Count) { throw 'Expected declarations and three consumer blocks.' }
for ($index = 0; $index -lt $blocks.Count; $index++) {
    if ($blocks[$index].Groups[1].Value.Trim() -ne $expected[$index]) { throw 'Unexpected API block name/order.' }
    $code = $blocks[$index].Groups[2].Value
    if ($expected[$index] -eq 'declarations') {
        # CORE7.1 types must come from the actual assemblies, not shadow declarations.
        $implemented = @('ConfigurationSourcePriority','ConfigurationChangeMode','ConfigurationReadState','ConfigurationArgumentKind',
            'ConfigurationField','ConfigurationKey','ConfigurationOrigin','ConfigurationRead','ConfigurationEntry',
            'ConfigurationIssue','ConfigurationCommandLineResult','ConfigurationException','IConfigurationValueCodec',
            'IConfigurationSchemaBuilder','IConfigurationSchema','IConfigurationSnapshot','IConfigurationSource',
            'IConfigurationProfileBuilder','IConfigurationProfile','ConfigurationSchemas','ConfigurationProfiles',
            'ConfigurationSources','ConfigurationCommandLine','ConfigurationEnvironment',
            'IConfigurationReader','IAppConfigurationStage','IConfigurationProfileSchemas','IConfigurationSchemaVisitor',
            'ConfigurationAppExtensions','ConfigurationEdit','ConfigurationUpdate',
            'IConfigurationReloader','IConfigurationWriter','IConfigurationChanges',
            'IConfigurationMutableSource','IConfigurationProfileWriter','IConfigurationBootstrapInputs')
        $declarations = [regex]::Matches($code, '(?m)^    public (?:sealed (?:record|class)|static class|enum|interface)\s+([A-Za-z]\w*)')
        for ($declaration = $declarations.Count - 1; $declaration -ge 0; $declaration--) {
            if ($declarations[$declaration].Groups[1].Value -notin $implemented) { continue }
            $start = $declarations[$declaration].Index
            $end = if ($declaration + 1 -lt $declarations.Count) { $declarations[$declaration + 1].Index } else { $code.LastIndexOf('}') }
            $code = $code.Remove($start, $end - $start)
        }
    }
    [IO.File]::WriteAllText((Join-Path $outputPath ($expected[$index] + '.cs')), $code, $utf8)
}
# Shadow repository imports, including the legacy dotnet-xunit CLI tool reference.
foreach ($name in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) {
    [IO.File]::WriteAllText((Join-Path $outputPath $name), '<Project />', $utf8)
}
$libraries = [ordered]@{}
foreach ($directory in @('TrueMoon.Aether.Editor', 'TrueMoon', 'TrueMoon.Cobalt', 'TrueMoon.Extensions.DependencyInjection')) {
    $binaryPath = Join-Path $repoPath "$directory/bin/Debug/net10.0"
    if (-not (Test-Path -LiteralPath $binaryPath -PathType Container)) { throw "Missing local assemblies: $binaryPath" }
    foreach ($file in Get-ChildItem -LiteralPath $binaryPath -Filter '*.dll') { $libraries[$file.BaseName] = $file.FullName }
}
$referenceXml = ($libraries.GetEnumerator() | ForEach-Object {
    '<Reference Include="' + [Security.SecurityElement]::Escape($_.Key) + '"><HintPath>' +
    [Security.SecurityElement]::Escape($_.Value) + '</HintPath><Private>false</Private></Reference>'
}) -join "`n"
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion><IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors><EnableNETAnalyzers>false</EnableNETAnalyzers>
    <GenerateDocumentationFile>false</GenerateDocumentationFile><NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="declarations.cs;headless.cs;mithril.cs;editor.cs" />
__REFERENCES__
  </ItemGroup>
</Project>
'@
$projectPath = Join-Path $outputPath 'ConfigurationApi.csproj'
[IO.File]::WriteAllText($projectPath, $project.Replace('__REFERENCES__', $referenceXml), $utf8)
$configPath = Join-Path $outputPath 'NuGet.Config'
[IO.File]::WriteAllText($configPath, '<configuration><packageSources><clear /></packageSources></configuration>', $utf8)
$manifest = [ordered]@{
    Kind = 'DeclarationAndExamplesTypeCheck'; RuntimeExecuted = $false; Blocks = $blocks.Count
    DocumentSha256 = (Get-FileHash -LiteralPath $documentPath -Algorithm SHA256).Hash
    References = @($libraries.Values | ForEach-Object { @{ Path=$_; Sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } })
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputPath 'inputs.json') -Encoding utf8NoBOM
$environment = @{
    APPDATA = (Join-Path $outputPath 'appdata'); DOTNET_CLI_HOME = (Join-Path $outputPath 'cli')
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'; DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'; DOTNET_CLI_TELEMETRY_OPTOUT = '1'
}
$previous = @{}
foreach ($name in $environment.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process')
}
try {
    [void][IO.Directory]::CreateDirectory($environment.APPDATA)
    Push-Location $repoPath
    try {
        $sdk = & dotnet --version
        if ($LASTEXITCODE -ne 0) { throw 'SDK query failed.' }
        & dotnet build $projectPath --configuration Release --configfile $configPath -m "-bl:$outputPath/build-{}.binlog" -v:minimal
        if ($LASTEXITCODE -ne 0) { throw "API type-check build failed: $LASTEXITCODE" }
        $result = [ordered]@{ Completed=$true; Sdk=$sdk; Blocks=$blocks.Count; RuntimeExecuted=$false; DocumentSha256=$manifest.DocumentSha256 }
        $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputPath 'verification.json') -Encoding utf8NoBOM
        $result | ConvertTo-Json -Compress
    } finally { Pop-Location }
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
