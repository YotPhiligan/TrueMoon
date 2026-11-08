# TrueMoon.Nom: package delivery и независимые consumers

<a id="nom7-delivery-2026-11-08"></a>
## NOM7 — проверенный профиль, 2026-11-08

Nom формирует managed `net10.0` NuGet package. Собственная библиотека зависит только
от TrueMoon.Contracts; standalone App consumer дополнительно подключает TrueMoon.
Общий dependency closure — четыре пакета одной версии: TrueMoon.Contracts,
TrueMoon.Cobalt, TrueMoon и TrueMoon.Nom. Cobalt включает generator и buildTransitive
target; отдельного runtime/package dependency на generator, Aether, UI или LiteDB нет.

Проверены локальные Windows x64/.NET SDK10.0.403/runtime10.0.14:

| Consumer | Source Debug | Fresh NuGet Debug | Portable published Release | win-x64 published Release |
| --- | --- | --- | --- | --- |
| NomPublicConsumer |34checks (VerifyApp) |34checks |34checks из source и34из NuGet |34checks из NuGet |
| NomAssetTool |9successful+2rejected CLI processes,211checks |9+2processes,211checks |9+2processes,211checks из NuGet |9+2processes,211checks из NuGet |
| NomBufferConsumer, NOM8a |28checks (VerifyApp -Consumer Buffer) |28checks |28checks из source и28из NuGet |28checks из NuGet |
| NomRawConsumer, NOM8b |31checks (VerifyApp -Consumer Raw) |31checks |31checks из source и31из NuGet |31checks из NuGet |

Оба publish режима framework-dependent: установленный .NET10 runtime, UseAppHost=false,
без доставки runtime packs. Generated consumer отключает EnableRuntimePackDownload и
transitive framework downloads, чтобы unused packs не подменяли проверку четырёхпакетного
графа. Managed AnyCPU не является доказательством других платформ: Linux/macOS,
win-arm64, self-contained, NativeAOT и trimming остаются непроверенными.

## Повторяемая локальная доставка

Из src, PowerShell7 и .NET10 SDK:

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/VerifyPackages.ps1 -EvidenceDirectory TestResults/MyFreshNomPackages
```

EvidenceDirectory должна отсутствовать. Runner создаёт уникальную local alpha версию
из общего0.2.5.1-alpha, UTC и random suffix; Directory.Build.props не меняется. Он:

1. Строит реальные проекты и пакует четыре библиотеки в новый OS TEMP feed с
   отдельными artifacts. Только source build использует существующий compiler cache
   для Roslyn; эту фазу нельзя называть zero-package build.
2. Проверяет nuspec ID/version/dependency, PACKAGE.md, LICENSE/icon byte hashes и
   DLL/XML payload. Nom содержит только managed lib/readme/license/icon/NuGet metadata,
   без runtime/analyzer/build/test/provider payload. PE references — BCL/Contracts,
   публичные типы — TrueMoon.Nom.*; public API storage не меняется ради упаковки.
3. Копирует публичные App/buffer/raw templates и четыре C# файла NomAssetTool в отдельные проекты
   вне checkout. Каждый получает свои пустые cache, NuGet.Config с единственным новым
   feed и очищенными fallback folders; portable/RID restores независимы.
4. Проверяет project.assets.json: ровно четыре свежих package libraries, нет
   ProjectReference и fallback cache. Hash каждого cached nupkg равен freshly packed
   файлу; actual compiler использует единственный generator из текущего Cobalt.
5. Запускает consumers из unrelated working directory; проверяет output/deps и SHA
   фактической Nom DLL против упакованной. Assembly names отличаются от friend tests/
   probe. Проба internal NomKvStore должна упасть с CS0122; feed без Nom — с NU1101.
   Raw unmanaged constraint проверяется отдельным compiler-negative template:CS8377
   на каждой из трёх операций для string/managed-field struct, а не произвольная build failure.
6. Сверяет frozen input hashes, копирует четыре nupkg в evidence/packages и сохраняет
   version/provenance/SHA/commands/profiles в verification.json. Каждый SDK command
   имеет уникальный binlog и stdout/stderr; процессы bounded и reaped в finally.

Это local delivery proof; runner не делает nuget push и не меняет package version
репозитория. Артефакты не подписаны как внешний release. Источник — явно dirty working
tree на зафиксированном HEAD и наборе frozen SHA, а не якобы чистый commit.

## Самостоятельный NomAssetTool

[Исходники и CLI](../../ManualTests/NomAssetTool/README.md) включены в solution как
отдельное headless приложение. Workspace project использует ProjectReference;
package runner копирует те же исходники в PackageReference-only project, без исходного
storage project или friend access. Production Nom не получает зависимость на tool.

AssetTransferWorkflow создаётся App registration factory и получает две typed
IFactory через constructor: OpenAssetStoreArguments и RestoreAssetStoreArguments.
Resolve остаётся в composition root. Workflow закрывает streams/snapshots/stores до
App; проверки WorkflowProductsDisposedBeforeApp/AppStopConfirmsActualOwnerRelease
требуют registry0 и actual release. Borrowed factories/inputs не освобождаются tool.

Каждый запуск — отдельный процесс, state передаётся named schema1 receipt с persistent
IDs, exact revisions/tickets, manifest/pin и generation. Receipt не содержит source root.
Последовательность: create → verify → export → restore → collect → verify-current →
повтор collect → повтор verify-current → verify исходного root. Проверяются:

- CreateFrozenExactDependencyManifest, FrozenOldGeometryBytesAfterReimportAndReopen:
  geometry v1/v2 и opaque shader, exact dependencies, независимое сравнение каждого
  byte/EOF и64KiB read buffer. Старые tickets истекли до GC, old bytes держит manifest.
- DurablePinRejectsRemovalBeforeAnyRuntimeLease, IndexedSourceExclusiveContinuationAndGuidOrder,
  ForwardAndReverseDependenciesUseExactVersions: persistent pin, фильтры/страницы и
  точные рёбра, в том числе старой revision.
- ExportFrozenStateToVerifiedImmutableBackup, RestoreFrozenStateThroughTypedFactory:
  backup/restore без доступа к source root, тот же receipt/IDs/generation.
- GcDryRunSelectsOnlyUnpinnedOldGeometry, CollectUnpinnedHistoryPreservesCurrentContentGeneration,
  CollectedOldExactRevisionCannotOpen, NoRemainingGarbageAfterReopen: old history
  удаляется явно после unpin/remove/prune; current/shader сохраняются после reopen,
  повтор GC безопасен, исходный каталог остаётся frozen.

Повтор create в существующий root и неизвестная команда дают exit2 до App creation,
не создают report и не меняют ни один source file (SHA сверяются). Runtime failure
после admission даёт report Completed=false/exit1. Tool не является importer/codec,
общим package manager или production editor bridge.

## Артефакты, границы и следующий шаг

Фактическая final версия: `0.2.5.1-alpha.nom7.20261108112314.r00129b91`.
Пути/полные четыре SHA и результаты — [HISTORY](HISTORY.md#nom7-2026-11-08),
текущая сводка — [STATUS](STATUS.md). Raw TestResults и TEMP сохраняются локально
без автоматического удаления; они ignored, не служат единственной записью acceptance.
Перед внешним release архивировать evidence/packages, JSON, logs и binlogs вместе
с exact source snapshot. Автоматическая retention policy и внешняя публикация не заданы.

NOM3/4 physical isolated-volume disk-full/power-loss gates открыты; package-only proof
их не повторяет. Отдельный [NOM7 physical follow-up](DURABILITY.md) после UAC прошёл
real disk-full5cases/166assertions каждый Debug/published Release и cleanup0;
по новому решению пользователя hardware power-off исключён из текущего acceptance.
[VM crash harness](VM_CRASH.md) реализован, actual guest trials ожидают image/credential.
Heavy capacity/performance NOM6 не повторялись, их числа остаются датированным NOM6
результатом. Unit/source/published functional regression повторена отдельно в NOM7.
Следующий consumer этап — engine ARCH1g.2 bridge после согласования владельцем,
без переноса importer/render/audio логики в Nom. Другие RID/release acceptance отдельно.
