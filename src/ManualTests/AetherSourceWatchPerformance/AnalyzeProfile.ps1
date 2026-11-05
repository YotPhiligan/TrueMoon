param([Parameter(Mandatory)][string]$Speedscope,[Parameter(Mandatory)][string]$Output)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Choose a new output file; existing analysis is retained.'}
$source=(Resolve-Path -LiteralPath $Speedscope).Path
$trace=Get-Content -LiteralPath $source -Raw|ConvertFrom-Json
$frames=@($trace.shared.frames)
$inclusive=[double[]]::new($frames.Count);$exclusive=[double[]]::new($frames.Count)
$flags=foreach($frame in $frames){
    $name=$frame.name
    [pscustomobject]@{
        Root=$name -match '^TrueMoon\.Aether\.Editor!.*EditorSource(Census|DirectoryCache|DirectoryPin|File|OpenBatch)';Path=$name -match 'EditorSourceCensus\.ValidatePath|EditorSourceDirectoryCache\.(ValidatePath|ValidateAncestors|ValidateDirectory|Revalidate|Check|ReadAttributes)';
        Attributes=$name -match 'EditorSourceCensus\.(Attributes|Metadata)|EditorSourceFile\.ReadMetadata|EditorSourceDirectoryCache\.(Check|ReadAttributes)|FillAttributeInfo|GetFileAttributes|FileSystem\.GetAttributes|FileSystemInfo\.get_|FileInfo\.get_';
        Tree=$name -match 'EditorSourceCensus\.<ScanCoreAsync>g__Census|FileSystemEnumerator|FileSystemEnumerable';
        Drive=$name -match 'DriveInfo|GetDriveType';Hash=$name -match 'Cryptography|BCrypt|AppendData|HashProvider';
        Read=$name -match 'ReadAsync|ReadFile|RandomAccess|EditorSourceFile\.Read\(';
        OpenClose=$name -match 'SafeFileHandle\.Open|SafeFileHandle\.InitThreadPoolBinding|ThreadPoolBoundHandle\.BindHandle|FileStream\.\.ctor|OSFileStreamStrategy\.\.ctor|EditorSourceFile\.(Open|ReadOpenedMetadata)|EditorSourceOpenBatch\.Open\(|\.Dispose';
        Unknown=$name -match 'Missing Symbol|UNKNOWN|UNMANAGED_CODE_TIME'
        Pin=$name -match 'EditorSourceDirectoryPin'
    }
}
$state=@{Weight=[double]0;Unknown=[double]0;Segments=0;Categories=[Collections.Generic.Dictionary[string,double]]::new();Paths=[Collections.Generic.Dictionary[string,double]]::new()}
function AddInterval($stack,[double]$weight){
    if($weight -le 0 -or $stack.Count -eq 0){return}
    $root=$false;$path=$false;$attributes=$false;$tree=$false;$drive=$false;$hash=$false;$read=$false;$openClose=$false;$unknown=$false;$pin=$false
    foreach($index in $stack){$f=$flags[$index];$root=$root -or $f.Root;$path=$path -or $f.Path;$attributes=$attributes -or $f.Attributes;$tree=$tree -or $f.Tree;$drive=$drive -or $f.Drive;$hash=$hash -or $f.Hash;$read=$read -or $f.Read;$openClose=$openClose -or $f.OpenClose;$unknown=$unknown -or $f.Unknown;$pin=$pin -or $f.Pin}
    if(!$root){return}
    $state.Weight+=$weight;$state.Segments++
    if($unknown){$state.Unknown+=$weight}
    # Disjoint categories; inclusive method totals below can overlap and must not be summed.
    $category=if($path -and $attributes){'ValidatePath / ancestor attributes'}elseif($pin){'Directory namespace pins'}elseif($path -and $drive){'ValidatePath / drive type'}elseif($path){'ValidatePath / other'}elseif($tree){'Tree enumeration + entry attributes'}elseif($attributes){'File/probe metadata outside ValidatePath'}elseif($hash){'Hash creation/update/finalization'}elseif($openClose){'File open/handle metadata/binding/dispose'}elseif($read){'File read'}else{'Other census frames'}
    if(!$state.Categories.ContainsKey($category)){$state.Categories[$category]=0};$state.Categories[$category]+=$weight
    $seen=[Collections.Generic.HashSet[int]]::new()
    foreach($index in $stack){if($seen.Add($index)){$inclusive[$index]+=$weight}}
    $leaf=$stack[$stack.Count-1];$exclusive[$leaf]+=$weight
    $pathName=(@($stack|ForEach-Object{$frames[$_].name}) -join ' -> ')
    if(!$state.Paths.ContainsKey($pathName)){$state.Paths[$pathName]=0};$state.Paths[$pathName]+=$weight
}
$eventCount=0
foreach($profile in $trace.profiles){
    $scale=switch($profile.unit){'milliseconds'{1d}'seconds'{1000d}'microseconds'{0.001d}'nanoseconds'{0.000001d}default{throw "Unsupported unit: $($profile.unit)"}}
    if($profile.type -eq 'evented'){
        $stack=[Collections.Generic.List[int]]::new();$previous=[double]$profile.startValue
        foreach($event in $profile.events){
            $at=[double]$event.at
            if($at -lt $previous-0.000001){throw 'Event timestamps are not monotonic.'}
            AddInterval $stack (($at-$previous)*$scale)
            if($event.type -eq 'O'){$stack.Add([int]$event.frame)}elseif($event.type -eq 'C'){
                if($stack.Count -eq 0 -or $stack[$stack.Count-1] -ne [int]$event.frame){throw 'Unbalanced Speedscope stack.'}
                $stack.RemoveAt($stack.Count-1)
            }else{throw 'Unknown event type.'}
            $previous=$at;$eventCount++
        }
        AddInterval $stack (([double]$profile.endValue-$previous)*$scale)
        if($stack.Count -ne 0){throw 'Unclosed Speedscope stack.'}
    }elseif($profile.type -eq 'sampled'){
        for($sample=0;$sample -lt $profile.samples.Count;$sample++){AddInterval $profile.samples[$sample] ([double]$profile.weights[$sample]*$scale);$eventCount++}
    }else{throw "Unsupported profile type: $($profile.type)"}
}
if($state.Weight -le 0){throw 'No real EditorSourceCensus stack samples found.'}
$methodRows=for($index=0;$index -lt $frames.Count;$index++){
    if($inclusive[$index] -gt 0){[pscustomobject]@{Frame=$frames[$index].name;InclusiveMilliseconds=$inclusive[$index];InclusivePercentOfCensus=100*$inclusive[$index]/$state.Weight;ExclusiveMilliseconds=$exclusive[$index];ExclusivePercentOfCensus=100*$exclusive[$index]/$state.Weight}}
}
$categoryRows=@($state.Categories.GetEnumerator()|Sort-Object Value -Descending|ForEach-Object{[pscustomobject]@{Category=$_.Key;SampledThreadMilliseconds=$_.Value;PercentOfCensus=100*$_.Value/$state.Weight}})
$paths=@($state.Paths.GetEnumerator()|Sort-Object Value -Descending|Select-Object -First 20|ForEach-Object{@{Frames=$_.Key;SampledThreadMilliseconds=$_.Value;PercentOfCensus=100*$_.Value/$state.Weight}})
$validationModel=$null
$benchmark=Join-Path (Split-Path -Parent $source) benchmark/report.json
if(Test-Path -LiteralPath $benchmark){
    $descriptor=(Get-Content -LiteralPath $benchmark -Raw|ConvertFrom-Json).Descriptor
    $pathsToValidate=@($descriptor.SourceTrees)+@($descriptor.Files.Path)+@($descriptor.Probes.Path)
    $visits=[Collections.Generic.Dictionary[string,int]]::new([StringComparer]::OrdinalIgnoreCase);$calls=0
    foreach($pathToValidate in $pathsToValidate){
        for($ancestor=[IO.Path]::GetDirectoryName($pathToValidate);$null -ne $ancestor;$ancestor=[IO.Path]::GetDirectoryName($ancestor)){
            $calls++;if(!$visits.ContainsKey($ancestor)){$visits[$ancestor]=0};$visits[$ancestor]++
        }
    }
    $validationModel=@{ValidatedPaths=$pathsToValidate.Count;AncestorAttributeCallsWithoutRetries=$calls;DistinctAncestorDirectories=$visits.Count;RepeatedAncestorVisits=$calls-$visits.Count;
        TopAncestors=@($visits.GetEnumerator()|Sort-Object Value -Descending|Select-Object -First 12|ForEach-Object{@{Directory=$_.Key;Visits=$_.Value}});
        Interpretation='Legacy uncached ancestor-walk model derived from the descriptor for comparison. This is not the actual optimized pin/check count or an instrumented syscall count. Retries can add visits; tree/file/probe checks are additional.'}
}
$result=[ordered]@{Completed=$true;Speedscope=$source;Sha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash;Exporter=$trace.exporter;ThreadProfiles=$trace.profiles.Count;Frames=$frames.Count;Events=$eventCount;
    CensusSampledThreadMilliseconds=$state.Weight;CensusSegments=$state.Segments;CensusWithUnresolvedFramesPercent=100*$state.Unknown/$state.Weight;
    Categories=$categoryRows;Methods=@($methodRows|Sort-Object InclusiveMilliseconds -Descending);TopPaths=$paths;ValidationModel=$validationModel;DirectoryCacheFramesPresent=@($frames|Where-Object name -match 'EditorSourceDirectoryCache').Count -gt 0;
    FileReaderFramesPresent=@($frames|Where-Object name -match 'EditorSourceFile\.').Count -gt 0;
    OpenBatchFramesPresent=@($frames|Where-Object name -match 'EditorSourceOpenBatch').Count -gt 0;
    Analysis=@{Path=$PSCommandPath;Sha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash};
    Interpretation='Sampled managed thread-time weights conditional on actual Census or owned directory/file IO helper frames; not CPU cycles or a complete async wall-time breakdown. Inclusive method totals overlap. Categories are disjoint. Open category includes initial opened-handle metadata. Unresolved frames retain their resolved managed caller context.'}
$result|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $Output -Encoding utf8
[pscustomobject]$result|Select-Object Completed,CensusSampledThreadMilliseconds,CensusSegments,CensusWithUnresolvedFramesPercent,Categories,ValidationModel|ConvertTo-Json -Depth 5
