# Стандартные ключи конфигурации

Каталог CORE7.6 получен из public schema metadata свежих пакетов. [Подключение и семантика](CONFIGURATION_GUIDE.md).
Здесь defaults до semantic validation; Paths:Root=null выбирает OS/product default, а App:Name зависит от entry assembly.
Пустой Editor:SourceEditor:ExecutablePath требует явного валидного пути, если соответствующая схема выбрана.
Все стандартные поля StartupOnly. Для собственного динамического schema field режим задаётся явно.
Canonical CLI: --<key>=<value>. Для prefix MYAPP_ env name: MYAPP_ + key с заменой : на __; legacy/bootstrap aliases перечислены в guide.
Runtime identity, factories, documents и mutable UI state в каталог не входят. Значения sensitive defaults маскируются.

Воспроизведение: ManualTests/ConfigurationDelivery/Export-Keys.ps1 -VerificationManifest <successful-manifest> -OutputPath <catalog.md>.

## Aether

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Aether:BuildDirectory` | `String` | `null` |  | false |
| `Aether:BuildRetention:MaxAge` | `TimeSpan` | `"7.00:00:00"` |  | false |
| `Aether:BuildRetention:MaxJobs` | `Int32` | `16` |  | false |
| `Aether:BuildRetention:MaxTotalBytes` | `Int64` | `67108864` |  | false |
| `Aether:BuildTimeout` | `TimeSpan` | `"00:02:00"` |  | false |
| `Aether:ChildDiagnostics:BytesPerSecond` | `Int32` | `262144` |  | false |
| `Aether:ChildDiagnostics:Enabled` | `Boolean` | `true` |  | false |
| `Aether:ChildDiagnostics:MaxBatchBytes` | `Int32` | `32768` |  | false |
| `Aether:ChildDiagnostics:MaxBatchRecords` | `Int32` | `16` |  | false |
| `Aether:ChildDiagnostics:PollInterval` | `TimeSpan` | `"00:00:00.0500000"` |  | false |
| `Aether:ChildDiagnostics:RecordsPerSecond` | `Int32` | `128` |  | false |
| `Aether:ChildDiagnostics:RequestTimeout` | `TimeSpan` | `"00:00:00.2500000"` |  | false |
| `Aether:ChildDiagnostics:ShutdownTimeout` | `TimeSpan` | `"00:00:01"` |  | false |
| `Aether:Deadlines:Command` | `TimeSpan` | `"00:00:02"` |  | false |
| `Aether:Deadlines:HeartbeatInterval` | `TimeSpan` | `"00:00:01"` |  | false |
| `Aether:Deadlines:HeartbeatResponse` | `TimeSpan` | `"00:00:05"` |  | false |
| `Aether:Deadlines:Startup` | `TimeSpan` | `"00:00:15"` |  | false |
| `Aether:GpuValidation` | `Boolean` | `false` |  | false |
| `Aether:ReplacementDeadlines:Cleanup` | `TimeSpan` | `"00:00:10"` |  | false |
| `Aether:ReplacementDeadlines:Forward` | `TimeSpan` | `"00:00:18"` |  | false |
| `Aether:ReplacementDeadlines:Total` | `TimeSpan` | `"00:00:45"` |  | false |
| `Aether:ReportDirectory` | `String` | `"aether-reports"` |  | true |

## Aether:Authoring

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Aether:Authoring:MaxBatchEdits` | `Int32` | `64` |  | false |
| `Aether:Authoring:MaxHistoryEntries` | `Int32` | `100` |  | false |
| `Aether:Authoring:MaxOperations` | `Int32` | `4096` |  | false |

## Aether:Ecs:Primitives

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Aether:Ecs:Primitives:Far` | `Single` | `100` |  | false |
| `Aether:Ecs:Primitives:FieldOfView` | `Single` | `1.0471976` |  | false |
| `Aether:Ecs:Primitives:FixedStep` | `TimeSpan` | `"00:00:00.0166666"` |  | false |
| `Aether:Ecs:Primitives:MaximumPrimitives` | `Int32` | `64` |  | false |
| `Aether:Ecs:Primitives:MaximumTransformEntities` | `Int32` | `65536` |  | false |
| `Aether:Ecs:Primitives:Near` | `Single` | `0.1` |  | false |

## Aether:Ecs:SceneLoading

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Aether:Ecs:SceneLoading:MaximumComponentInstances` | `Int32` | `65536` |  | false |
| `Aether:Ecs:SceneLoading:MaximumEntities` | `Int32` | `4096` |  | false |
| `Aether:Ecs:SceneLoading:MaximumPreparedResources` | `Int32` | `4096` |  | false |
| `Aether:Ecs:SceneLoading:MaximumPropertyValues` | `Int32` | `262144` |  | false |

## Aether:Ecs:Scheduler

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Aether:Ecs:Scheduler:MaximumCommandsPerPhase` | `Int32` | `4096` |  | false |
| `Aether:Ecs:Scheduler:MaximumSystems` | `Int32` | `128` |  | false |

## Aether:Ecs:World

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Aether:Ecs:World:InitialEntityCapacity` | `Int32` | `64` |  | false |
| `Aether:Ecs:World:MaximumComponentTypes` | `Int32` | `128` |  | false |
| `Aether:Ecs:World:MaximumEntityCount` | `Int32` | `65536` |  | false |

## Aether:RenderResources

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Aether:RenderResources:MaximumActivePreparations` | `Int32` | `4` |  | false |
| `Aether:RenderResources:MaximumCpuBytes` | `Int64` | `67108864` |  | false |
| `Aether:RenderResources:MaximumCpuLeases` | `Int32` | `16384` |  | false |
| `Aether:RenderResources:MaximumCpuRevisions` | `Int32` | `2048` |  | false |
| `Aether:RenderResources:MaximumCpuScopes` | `Int32` | `64` |  | false |
| `Aether:RenderResources:MaximumCpuStagingBytes` | `Int64` | `16777216` |  | false |
| `Aether:RenderResources:MaximumGeometryBytes` | `Int64` | `67108864` |  | false |
| `Aether:RenderResources:MaximumGpuEntries` | `Int32` | `2048` |  | false |
| `Aether:RenderResources:MaximumQueuedPreparations` | `Int32` | `16` |  | false |
| `Aether:RenderResources:MaximumUploadStagingBytes` | `Int64` | `16777216` |  | false |

## Alloy:Window

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Alloy:Window:EnableDiagnostics` | `Boolean` | `false` |  | false |
| `Alloy:Window:FrameMode` | `UiFrameMode` | `0` |  | false |
| `Alloy:Window:FramesPerSecond` | `Int32` | `60` |  | false |
| `Alloy:Window:GraphicsApi` | `Nullable<UiWindowGraphicsApi>` | `null` |  | false |
| `Alloy:Window:Height` | `Int32` | `540` |  | false |
| `Alloy:Window:Title` | `String` | `"TrueMoon UI"` |  | false |
| `Alloy:Window:TitleBarOverContent` | `Boolean` | `false` |  | false |
| `Alloy:Window:UseDefaultWindowFrame` | `Boolean` | `true` |  | false |
| `Alloy:Window:UseSystemAccentBorder` | `Boolean` | `false` |  | false |
| `Alloy:Window:Width` | `Int32` | `960` |  | false |

## App

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `App:Name` | `String` | `"<entry assembly name>"` |  | true |

## Diagnostics

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Diagnostics:ConsoleOutput` | `Boolean` | `false` |  | false |
| `Diagnostics:DisabledEvents` | `String[]` | `[]` |  | false |
| `Diagnostics:Emergency:Enabled` | `Boolean` | `true` |  | false |
| `Diagnostics:Emergency:MaxMessageChars` | `Int32` | `1024` |  | false |
| `Diagnostics:Emergency:MaxReports` | `Int32` | `16` |  | false |
| `Diagnostics:Emergency:QueueCapacity` | `Int32` | `8` |  | false |
| `Diagnostics:JsonDirectory` | `String` | `null` |  | false |
| `Diagnostics:JsonFileSizeBytes` | `Int64` | `1048576` |  | false |
| `Diagnostics:JsonMaxFiles` | `Int32` | `4` |  | false |
| `Diagnostics:JsonRetention:MaxAge` | `TimeSpan` | `"7.00:00:00"` |  | false |
| `Diagnostics:JsonRetention:MaxRuns` | `Int32` | `8` |  | false |
| `Diagnostics:JsonRetention:MaxTotalBytes` | `Int64` | `33554432` |  | false |
| `Diagnostics:JsonUseRunDirectories` | `Boolean` | `false` |  | false |
| `Diagnostics:MaxExceptionChars` | `Int32` | `4096` |  | false |
| `Diagnostics:MaxMessageChars` | `Int32` | `2048` |  | false |
| `Diagnostics:MaxNameChars` | `Int32` | `128` |  | false |
| `Diagnostics:MaxProperties` | `Int32` | `16` |  | false |
| `Diagnostics:MaxPropertyChars` | `Int32` | `256` |  | false |
| `Diagnostics:MemoryByteCapacity` | `Int64` | `4194304` |  | false |
| `Diagnostics:MemoryCapacity` | `Int32` | `512` |  | false |
| `Diagnostics:MinimumLevel` | `DiagnosticLevel` | `2` |  | false |
| `Diagnostics:QueueByteCapacity` | `Int64` | `4194304` |  | false |
| `Diagnostics:QueueCapacity` | `Int32` | `256` |  | false |
| `Diagnostics:ReservedErrorCapacity` | `Int32` | `16` |  | false |
| `Diagnostics:ShutdownTimeout` | `TimeSpan` | `"00:00:05"` |  | false |
| `Diagnostics:SinkMinimumLevels` | `IReadOnlyDictionary<String, DiagnosticLevel>` | `{}` |  | false |
| `Diagnostics:SourceMinimumLevels` | `IReadOnlyDictionary<String, DiagnosticLevel>` | `{}` |  | false |
| `Diagnostics:Telemetry:MaxNameChars` | `Int32` | `128` |  | false |
| `Diagnostics:Telemetry:MaxOperationNames` | `Int32` | `64` |  | false |
| `Diagnostics:Telemetry:MaxProperties` | `Int32` | `16` |  | false |
| `Diagnostics:Telemetry:MaxPropertyChars` | `Int32` | `256` |  | false |
| `Diagnostics:Telemetry:MetricsEnabled` | `Boolean` | `true` |  | false |
| `Diagnostics:Telemetry:TracingEnabled` | `Boolean` | `true` |  | false |

## Editor

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Editor:ProjectPath` | `String` | `null` |  | false |
| `Editor:SourceTracking:Debounce` | `TimeSpan` | `"00:00:00.2500000"` |  | false |
| `Editor:SourceTracking:Enabled` | `Boolean` | `true` |  | false |
| `Editor:SourceTracking:MaximumDebounce` | `TimeSpan` | `"00:00:01"` |  | false |
| `Editor:SourceTracking:MaximumWatchers` | `Int32` | `32` |  | false |
| `Editor:SourceTracking:PollInterval` | `TimeSpan` | `"00:00:10"` |  | false |
| `Editor:SourceTracking:ScanBudget` | `TimeSpan` | `"00:00:05"` |  | false |
| `Editor:SourceTracking:UseFileSystemNotifications` | `Boolean` | `true` |  | false |

## Editor:Diagnostics

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Editor:Diagnostics:MaximumProcesses` | `Int32` | `16` |  | false |
| `Editor:Diagnostics:MaximumRawCharacters` | `Int32` | `4096` |  | false |
| `Editor:Diagnostics:MaximumRecordCharacters` | `Int32` | `8192` |  | false |
| `Editor:Diagnostics:MaximumRecords` | `Int32` | `512` |  | false |
| `Editor:Diagnostics:MaximumRetainedBytes` | `Int32` | `1048576` |  | false |
| `Editor:Diagnostics:RecordsPerRefresh` | `Int32` | `64` |  | false |
| `Editor:Diagnostics:RefreshInterval` | `TimeSpan` | `"00:00:00.1000000"` |  | false |

## Editor:SourceEditor

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Editor:SourceEditor:Arguments` | `IReadOnlyList<String>` | `[]` |  | false |
| `Editor:SourceEditor:ExecutablePath` | `String` | `""` |  | true |

## Editor:Window

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Editor:Window:EnableDiagnostics` | `Boolean` | `false` |  | false |
| `Editor:Window:FrameMode` | `UiFrameMode` | `2` |  | false |
| `Editor:Window:FramesPerSecond` | `Int32` | `60` |  | false |
| `Editor:Window:GraphicsApi` | `Nullable<UiWindowGraphicsApi>` | `null` |  | false |
| `Editor:Window:Height` | `Int32` | `800` |  | false |
| `Editor:Window:Title` | `String` | `"TrueMoon Aether Editor"` |  | false |
| `Editor:Window:TitleBarOverContent` | `Boolean` | `true` |  | false |
| `Editor:Window:UseDefaultWindowFrame` | `Boolean` | `true` |  | false |
| `Editor:Window:UseSystemAccentBorder` | `Boolean` | `true` |  | false |
| `Editor:Window:Width` | `Int32` | `1280` |  | false |

## Enerit:Pipes

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Enerit:Pipes:CaptureRequestEvents` | `Boolean` | `false` |  | false |
| `Enerit:Pipes:MaxBufferedRequestBytes` | `Int64` | `4194304` |  | false |
| `Enerit:Pipes:MaxConcurrentRequests` | `Int32` | `64` |  | false |
| `Enerit:Pipes:MaxConnections` | `Int32` | `4` |  | false |
| `Enerit:Pipes:MaxPendingRequests` | `Int32` | `64` |  | false |
| `Enerit:Pipes:MaxQueuedRequests` | `Int32` | `64` |  | false |
| `Enerit:Pipes:MetricsEnabled` | `Boolean` | `true` |  | false |
| `Enerit:Pipes:PropagateTraceContext` | `Boolean` | `false` |  | false |
| `Enerit:Pipes:RequestTimeout` | `TimeSpan` | `"00:00:50"` |  | false |

## Nom

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Nom:BatchBytes` | `Int32` | `4194304` |  | false |
| `Nom:BatchRows` | `Int32` | `256` |  | false |
| `Nom:DocumentBytes` | `Int32` | `131072` |  | false |
| `Nom:DomainIndexBytes` | `Int64` | `67108864` |  | false |
| `Nom:DrainTimeout` | `TimeSpan` | `"00:00:10"` |  | false |
| `Nom:MaximumOpenStores` | `Int32` | `4` |  | false |
| `Nom:QueueBytes` | `Int32` | `16777216` |  | false |
| `Nom:QueueCount` | `Int32` | `32` |  | false |

## Paths

| Key | Type | Default JSON | CLI aliases | Required |
| --- | --- | --- | --- | --- |
| `Paths:Root` | `String` | `null` | -root | false |

