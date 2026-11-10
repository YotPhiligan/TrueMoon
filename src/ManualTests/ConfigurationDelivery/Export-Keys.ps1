[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$VerificationManifest,
    [Parameter(Mandatory)][string]$OutputPath
)

# Reads public frozen schema metadata from verified fresh packages. No profile sources or App are loaded.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$manifest=Get-Content -LiteralPath $VerificationManifest -Raw | ConvertFrom-Json
if (!$manifest.Completed) { throw 'Use a completed delivery manifest.' }
$directory=Join-Path $manifest.RunDirectory 'modules-fresh-published'
$context=[Runtime.Loader.AssemblyLoadContext]::new('configuration-catalog-'+[Guid]::NewGuid().ToString('N'), $true)
$resolve=[Func[Runtime.Loader.AssemblyLoadContext,Reflection.AssemblyName,Reflection.Assembly]] {
    param($loading,$requested)
    $candidate=Join-Path $directory ($requested.Name+'.dll')
    if (Test-Path -LiteralPath $candidate) { return $loading.LoadFromAssemblyPath($candidate) }
    return $null
}.GetNewClosure()
$context.add_Resolving($resolve)
function Type-Name([Type]$type) {
    if ($type.IsArray) { return (Type-Name $type.GetElementType())+'[]' }
    if ($type.IsGenericType) { return $type.Name.Split('`')[0]+'<'+(($type.GetGenericArguments() | ForEach-Object { Type-Name $_ }) -join ', ' )+'>' }
    return $type.Name
}
try {
    $specifications=@(
        @('TrueMoon','TrueMoon.Configuration.CoreConfigurationSchemas',@('App','Paths')),
        @('TrueMoon','TrueMoon.Diagnostics.DiagnosticsConfigurationSchema',@('Default')),
        @('TrueMoon.Aether','TrueMoon.Aether.AetherConfigurationSchemas',@('Engine','World','Scheduler','SceneLoading','Primitives','RenderResources','Authoring')),
        @('TrueMoon.Aether.Editor','TrueMoon.Aether.Editor.EditorConfigurationSchemas',@('Options','Diagnostics','SourceEditor','Window')),
        @('TrueMoon.Enerit','TrueMoon.Enerit.IO.Pipes.PipesConfigurationSchema',@('Default')),
        @('TrueMoon.Nom','TrueMoon.Nom.NomConfigurationSchema',@('Default')),
        @('TrueMoon.Alloy.Hosting','TrueMoon.Alloy.Hosting.WindowConfigurationSchema',@('Default'))
    )
    $keys=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $rows=@(); $schemas=@(); $hashes=@()
    foreach ($specification in $specifications) {
        $path=Join-Path $directory ($specification[0]+'.dll')
        $assembly=$context.LoadFromAssemblyPath($path)
        $hashes+=@{Assembly=$specification[0];Sha256=(Get-FileHash -LiteralPath $path).Hash}
        $type=$assembly.GetType($specification[1], $true)
        foreach ($property in $specification[2]) {
            $schema=$type.GetProperty($property).GetValue($null)
            $schemas+=$specification[1]+'.'+$property
            $defaults=@{}
            foreach ($entry in $schema.GetType().GetMethod('GetDefaults').Invoke($schema,@())) { $defaults[$entry.Path]=$entry.Value.GetRawText() }
            foreach ($key in $schema.Keys) {
                if (!$keys.Add($key.Path)) { throw 'Duplicate canonical metadata key.' }
                $default=if ($key.Field.Sensitive) { '"<redacted>"' } elseif ($key.Path -eq 'App:Name') { '"<entry assembly name>"' } else { $defaults[$key.Path] }
                $rows+=@{Section=$schema.Section;Key=$key.Path;Type=(Type-Name $key.ValueType);Default=$default;
                    Change=$key.Field.Change.ToString();Required=$key.Field.Required;Sensitive=$key.Field.Sensitive;
                    Argument=$key.Field.Argument.ToString();Aliases=$(if($null -eq $key.Field.Aliases){@()}else{@($key.Field.Aliases)})}
            }
        }
    }
    if ($schemas.Count -ne 17) { throw 'Expected the 17 standard schemas.' }
    $lines=[Collections.Generic.List[string]]::new()
    $lines.Add('# Стандартные ключи конфигурации')
    $lines.Add('')
    $lines.Add('Каталог CORE7.6 получен из public schema metadata свежих пакетов. [Подключение и семантика](CONFIGURATION_GUIDE.md).')
    $lines.Add('Здесь defaults до semantic validation; Paths:Root=null выбирает OS/product default, а App:Name зависит от entry assembly.')
    $lines.Add('Пустой Editor:SourceEditor:ExecutablePath требует явного валидного пути, если соответствующая схема выбрана.')
    $lines.Add('Все стандартные поля StartupOnly. Для собственного динамического schema field режим задаётся явно.')
    $lines.Add('Canonical CLI: --<key>=<value>. Для prefix MYAPP_ env name: MYAPP_ + key с заменой : на __; legacy/bootstrap aliases перечислены в guide.')
    $lines.Add('Runtime identity, factories, documents и mutable UI state в каталог не входят. Значения sensitive defaults маскируются.')
    $lines.Add('')
    $lines.Add('Воспроизведение: ManualTests/ConfigurationDelivery/Export-Keys.ps1 -VerificationManifest <successful-manifest> -OutputPath <catalog.md>.')
    $lines.Add('')
    foreach ($group in ($rows | Sort-Object Key | Group-Object Section | Sort-Object Name)) {
        $lines.Add('## '+$group.Name); $lines.Add('')
        $lines.Add('| Key | Type | Default JSON | CLI aliases | Required |')
        $lines.Add('| --- | --- | --- | --- | --- |')
        foreach ($row in $group.Group) {
            $default=$row.Default.Replace('|','\|').Replace('`','&#96;')
            $lines.Add('| `'+$row.Key+'` | `'+$row.Type+'` | `'+$default+'` | '+($row.Aliases -join ', ')+' | '+$row.Required.ToString().ToLowerInvariant()+' |')
        }
        $lines.Add('')
    }
    $output=[IO.Path]::GetFullPath($OutputPath)
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output))
    [IO.File]::WriteAllText($output,($lines -join "`n")+"`n")
    @{Schemas=$schemas;KeyCount=$rows.Count;Rows=$rows;Assemblies=$hashes;PackageVersion=$manifest.Version;OutputSha256=(Get-FileHash -LiteralPath $output).Hash} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath ($output+'.json') -Encoding utf8
    Write-Host "Schemas=$($schemas.Count), keys=$($rows.Count); $output"
} finally { $context.remove_Resolving($resolve); $context.Unload() }
