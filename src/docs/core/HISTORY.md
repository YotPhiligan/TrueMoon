# Core: история проверок и ревью

<a id="core85-delivery-2026-11-11"></a>
## CORE8.5: свежая согласованная поставка — 2026-11-11

CORE8.0–8.5 завершены в Windows x64 framework-dependent профиле. SDK 11.0.100,
net11.0, LangVersion=latest, runtime-async=on, base Version=0.3.0.1-alpha.
HEAD 2553f87d248cf748c858988e0ed38343290eda8c; накопленные пользовательские
изменения и proposed game заявка сохранены. В CORE8.5 C# runtime/tests, project
files, общие props, SDK pin, зависимости и public API не менялись.

### Поставка и actual consumers

Единая локальная версия: **0.3.0.1-alpha.core85.20261111221352**, 16 TrueMoon nupkg и 32 точных external
nupkg из restored assets. Remote publication не выполнялась. Nuspec identity,
согласованные TrueMoon dependency versions, net11 runtime payloads, README,
PACKAGE.md, license/icon, Cobalt/Enerit analyzer, build/buildTransitive targets
и bundled native Skia проверены по SHA. Aether и viewport используют те же
package bytes, без повторной упаковки или fallback к ProjectReference.

| Проверка | Результат и границы |
| --- | --- |
| Обычные source builds | Solution Debug и Release с restore: exit 0, errors 0/0, incremental warnings 0/0; clean-warning baseline не заявлена |
| Source/published example hosts | 4 DLL/apphost cases в source Debug / published Release; actual runtime children, 3 generations и 5 fault cases на case, parent/runtime dependency isolation, bootstrap rejection, resources/stop/reap |
| App/configuration delivery | 5 consumers × source Debug / published Release / fresh Release: 15 profiles, по 230 checks / 4 owned children на вид поставки, итого 690 checks / 12 children; Cobalt и Microsoft DI |
| Independent package game | Debug build / Release publish × DLL/apphost × Cobalt/Microsoft DI: 8 cases, по 3 actual runtime children; game compile/load/replacement/stop/reap, game не загружается в parent |
| Fresh package native viewport | 2 profiles: Debug build и Release publish; по 13 групп проверок / 9 owned children, actual GPU pixels, state и child/resource cleanup, validation errors/warnings 0/0 по reports |
| Финальная runtime regression | Все 11 test projects в Debug и Release: 22 новых полных запуска, по 8567 passed / 0 failed / 0 skipped на конфигурацию |

Независимые consumers находятся вне checkout, имеют собственные props/global.json,
CPM isolation и новые caches. SDK/TFM/LangVersion/Features передаются снимком.
TrueMoon packages поступают из локального feed; installed/deployed assemblies,
analyzer/native assets и source/package closure сверены по SHA. Общий feed содержит
и внешние зависимости; live NuGet в объединённом сценарии не используется.
Validation layer — прежний TestResults/EditorWindow-20261104/tools/validation;
driver/OS settings не менялись. Все stage manifests, source/package hashes и
command exits сохранены в общем verification.json.

MSBuild metadata сверена для всех 16/16 проектов с фактическими nuspec: identity, version, authors, description, readme/icon/license и target согласованы. Версионный [manifest поставки](CORE8_DELIVERY.json) сохраняет 16 package SHA, 32 external package identities/hashes, tool/native payload hashes и итоговые test/native counts без зависимости от TEMP retention.

### Исправления runners и документации

- Добавлен ManualTests/FrameworkDelivery/Verify.ps1: единый порядок сборки,
  поставки, actual consumers и финальных suites, с source-drift guards и binlogs.
- ConfigurationDelivery принимает согласованный уникальный PackageVersion.
- AetherPackageConsumer принимает completed PackageVerification, проверяет SHA
  и повторно использует exact packages. Его default version теперь следует
  текущему base version; independent game consumer получает language/features.
- Viewport использует собственный новый cache и снимок общего профиля;
  проверяет package-only graph, cache/feed provenance и deployment hashes.
  Props ограничены каталогом Host: соседние frozen game fixtures не наследуют
  host-only NuGetAudit/packaging settings; compiler allowlist не расширялся.
- Example-host runner безопасно проверяет пустой список CLI arguments.
- Исправлено подключение общего offline feed: один source key с mapping `*`.
  Два одинаковых пути под разными ключами дедуплицировались NuGet и приводили
  к NU1100 для external dependencies. Assertions и compiler policy не ослаблены.
- StrictMode оркестратора изолирован от старого standalone Aether runner,
  который читает optional NuGet JSON keys при обычных PowerShell semantics.
  Его собственный runtime/native verification сохранён.
- Root README, AGENTS, все 16 PACKAGE.md, актуальные Hosting/Aether требования,
  Alloy/Editor profile notes и consumer instructions согласованы с .NET 11.
  [Руководство по переходу](DOTNET11_MIGRATION.md) описывает SDK/TFM/Features,
  CPM, explicit rebuild прежних game artifacts и acceptance boundaries.

После успешного цикла исправлены ещё два source-only документа: SDK/target в
непакуемом TrueMoon.Cobalt.Generator/PACKAGE.md и текущие net11 команды в
TrueMoon.Alloy.Rendering.Skia/README.md. По содержимому MD entries подтверждено,
что они не входят в 16 nupkg. Их отдельные before/after SHA сохранены;
package bytes, C#/compiler/test inputs не менялись, тесты ради README не повторялись.

Первичные неуспешные trials сохранены отдельно: ошибка helper на empty arguments,
NU1100 из-за дублированного feed mapping, inherited StrictMode на optional
runtimeTargets и host props/NuGetAudit в game fixture. Это failures runners/composition, а не найденные runtime-async
регрессии. Passing получен новым полным циклом в новых каталогах/version/cache;
старые manifests не переписаны.

### Полные тесты окончательных исходников

| Проект | Debug passed | Release passed | Failed / skipped |
| --- | --- | --- | --- |
| TrueMoon.Aether.Editor.Tests | 1496 | 1496 | 0 / 0 |
| TrueMoon.Aether.Tests | 3243 | 3243 | 0 / 0 |
| TrueMoon.Alloy.Hosting.Tests | 171 | 171 | 0 / 0 |
| TrueMoon.Alloy.Tests | 1224 | 1224 | 0 / 0 |
| TrueMoon.Cobalt.Generator.Tests | 96 | 96 | 0 / 0 |
| TrueMoon.Cobalt.Tests | 179 | 179 | 0 / 0 |
| TrueMoon.Enerit.Generator.Tests | 114 | 114 | 0 / 0 |
| TrueMoon.Enerit.Tests | 354 | 354 | 0 / 0 |
| TrueMoon.Mithril.Tests | 155 | 155 | 0 / 0 |
| TrueMoon.Nom.Tests | 874 | 874 | 0 / 0 |
| TrueMoon.Tests | 661 | 661 | 0 / 0 |
| Итого на конфигурацию | 8567 | 8567 | 0 / 0 |

Обычные restore/build в каждой test command; нет test-only props overrides,
--no-build/--no-restore или смены xUnit 2/VSTest. Проекты запускаются
последовательно с MaxCpuCount=1 / ParallelizeTestCollections=false /
MaxParallelThreads=1. Это новые executions, отдельно от CORE8.4 discovery/TRX
сверки. Старые non-green результаты и причины diagnostics/drain timeout не
объявляются исправленными успешной регрессией.

### Команды и хранение

Из src, PowerShell 7.2+, Windows x64:

```powershell
./ManualTests/FrameworkDelivery/Verify.ps1 -ValidationDirectory TestResults/EditorWindow-20261104/tools/validation -EvidenceDirectory TestResults/CORE85-20261111-215503/delivery-complete -RunDirectory "$env:TEMP/TrueMoon-CORE85-20261111-215503-complete"
```

Все точные child arguments/exit codes — в stage manifests. Финальная test command
для каждого из 11 проектов и обеих конфигураций:

```powershell
dotnet test <project> -c <Debug|Release> --settings ManualTests/FrameworkDelivery/serial.runsettings --logger 'trx;LogFileName=tests.trx' --results-directory <evidence>/<project>-<configuration> --verbosity minimal '-bl:<evidence>/<project>-<configuration>-{}.binlog'
```

Основной evidence: TestResults/CORE85-20261111-215503/delivery-complete/.
TEMP consumers/feed/cache и source/published examples:
C:\Users\MEGA6\AppData\Local\Temp\TrueMoon-CORE85-20261111-215503-complete.
Ignored/TEMP retention не гарантирован; команды, inputs, outcomes, изменения
и границы сохраняются в этой versioned записи. Свежий повтор требует новых
каталогов и версии. Пакеты на удалённый NuGet service не отправлялись.

CORE8 завершил согласованный .NET 11 migration scope. Функциональная очередь
возвращается к ARCH1f.4 и отдельным задачам владельцев. Physical input/DPI,
K7/device loss/VRAM, Nom VM/power-loss, другие OS/RID/AOT, performance и
причины прежних reliability timeout сохраняют собственные критерии.

<a id="core-status-before-core85-2026-11-11"></a>
## STATUS до CORE8.5 — архив 2026-11-11

# Core: состояние и точка продолжения

**2026-11-11 — CORE8.4 завершён: полная runtime-async регрессия.**
[CORE8](PLAN.md#core8-dotnet11), [команды, матрица и ограничения](HISTORY.md#core84-runtime-2026-11-11).
Профиль: SDK 11.0.100 / net11.0 / runtime-async=on, обычные restore/build без
test-only overrides. Из 1769 code/build/script inputs сохранены 1765; исправлены
только четыре manual fixture writer: явная ссылка на Contracts. Production runtime,
API и зависимости в CORE8.4 не менялись.

- Все 11 test suites прошли в Debug и Release: **Passed 8567, Failed 0, Skipped 0 на конфигурацию**.
- Сохранены discovery и сопоставление методов с TRX: 22 полных запуска, 17134 test rows.
  Theory templates учитываются отдельно от развёрнутых параметризованных cases.
- После исправления fixtures повторены NativeRuntimeProcessTests: 4/4 в каждой конфигурации.
- Source Debug / published Release: прошли 24 native сценария CPU/GPU viewport/Editor
  и lifecycle с проверками пикселей, состояния, child stop/reap и освобождения ресурсов.
- Финальная solution сборка D/R: 0 ошибок; incremental warnings — 0/0.
  Это не новая clean warning baseline.

CORE8.0–3 сохраняют отдельные input/generator/frozen-game/package proofs.
[Предыдущий STATUS целиком](HISTORY.md#core-status-before-core84-2026-11-11).

## Следующий конкретный шаг

**CORE8.5:** свежая package/runtime/analyzer/native поставка одной версии,
source/published/fresh consumers и итоговая документация. Physical input/DPI,
device loss/K7/VRAM, другие OS/RID/AOT, performance и причины прежних
diagnostics/drain timeout остаются отдельными задачами.

<a id="core84-runtime-2026-11-11"></a>
## CORE8.4: полная runtime-async регрессия — 2026-11-11

Выполнена Windows x64 регрессия на SDK 11.0.100 / runtime 11.0.0,
TF/TestsTF=net11.0, LangVersion=latest, runtime-async=on,
Version=0.3.0.1-alpha. HEAD: 2553f87d248cf748c858988e0ed38343290eda8c.
Сохранены накопленные изменения CORE8.0–3 и proposed game заявка.
Из 1769 code/build/script inputs сохранены 1765; исправлены только четыре
manual fixture writer: явная non-copying ссылка на TrueMoon.Contracts через
прежний TrueMoonContractsPath. Production runtime C#, project files, общие props,
SDK pin, зависимости, публичные API и native/GPU implementation paths не менялись.
Assertions, guards и deadlines не ослаблялись.

### Полные suites и discovery

Все 11 tracked test projects обнаружены и выполнены в Debug и Release.
Исполнение включает обычные restore/build без --no-build/--no-restore,
test-only DirectoryPackagesPropsPath и props overrides. Сохранён xUnit 2 / VSTest.
Evaluation каждого проекта в обеих конфигурациях подтвердила net11.0,
SDK 11.0.100 и runtime-async=on. Проекты и process-heavy fixtures выполнялись
последовательно: ParallelizeTestCollections=false, MaxParallelThreads=1.

| Проект | Debug passed/total | Release passed/total | Failed / skipped |
| --- | --- | --- | --- |
| TrueMoon.Tests | 661/661 | 661/661 | 0/0 |
| TrueMoon.Cobalt.Tests | 179/179 | 179/179 | 0/0 |
| TrueMoon.Cobalt.Generator.Tests | 96/96 | 96/96 | 0/0 |
| TrueMoon.Enerit.Tests | 354/354 | 354/354 | 0/0 |
| TrueMoon.Enerit.Generator.Tests | 114/114 | 114/114 | 0/0 |
| TrueMoon.Mithril.Tests | 155/155 | 155/155 | 0/0 |
| TrueMoon.Nom.Tests | 874/874 | 874/874 | 0/0 |
| TrueMoon.Alloy.Tests | 1224/1224 | 1224/1224 | 0/0 |
| TrueMoon.Alloy.Hosting.Tests | 171/171 | 171/171 | 0/0 |
| TrueMoon.Aether.Tests | 3243/3243 | 3243/3243 | 0/0 |
| TrueMoon.Aether.Editor.Tests | 1496/1496 | 1496/1496 | 0/0 |
| Итого на конфигурацию | 8567/8567 | 8567/8567 | 0/0 |

Итого 17134 test rows в 22 полных запусках. Отдельный --list-tests --no-build
использовал существовавшие assemblies последней CORE8.3 solution сборки;
каждый последующий execution выполнял обычный build. Discovery logs/binlogs
сохранены для обеих конфигураций. MemberData может обнаруживаться одним theory
template: Cobalt — 110 templates / 179 executed rows, Alloy — 1215/1224.
Первый агрегатор ошибочно требовал равенства этих counts и вернул exit 1,
хотя все test/discovery команды вернули exit 0. Отдельная сверка сопоставила
каждый TRX row с обнаруженным методом и подтвердила исполнение всех методов.
Исходные counts и логи не переписаны. Агрегатор исправлен только в ignored
TestResults; повторное исполнение тестов из-за этой ошибки счётчика не требовалось.

| Поведение | Проверка в текущем полном запуске |
| --- | --- |
| Atomic configuration/reload/write, оба DI | ConfigurationReloadTests.ReloadPublishesOneAtomicGenerationAndRetainsOldOptionsServicesAndSnapshots; ConfigurationWriteTests.JsonPatchDistinguishesExplicitNullAndDeleteAndNeverReloadsImplicitly |
| Factory/aliases/dispose/errors | Все 179 Cobalt и 96 generator tests, включая AdapterRegressionTests/ResolverRegressionTests/ResolutionErrorTests |
| IPC cancellation/admission/drain | PipesTraceLifecycleTests.LocalCancellationAndTimeout_EndScopeButKeepCapacityAndOriginalLateResponseCorrelation; PipesMetricsTests.GenericServer_IncludesWaitingAndRetiringConnectionsUntilCancelledStopActuallyDrains |
| Owned processes/cancel/restart | Все 155 Mithril tests; ReadyFileProcessTests.ReadyRead_SharingRetriesHonorCallerCancellation; RestartProcessTests.Restart_OwnedSequence_EnforcesPolicyBudgetDelayAndStop |
| Nom IO/admission/reopen/lifetime | Все 874 Nom tests; NomRawBufferFaultTests.RawBlockedSource_CancelOrAppStopRetainsBorrowedOwnerAndRootUntilActualCompletion; NomAppStorageTests.RegisteredFactory_PublicStreamRoundtripReopenAndJournal_PreserveExactContent |
| UI callbacks / engine / frozen build | Все Alloy 1224 / Hosting 171 / Aether 3243 / Editor 1496 tests и source/published native пробы ниже |

Полные 22 suites выполнены до исправления четырёх manual templates. После
исправления повторены затронутые NativeRuntimeProcessTests: 4/4 в каждой
конфигурации, и все 24 source/published native сценария, включая каждый
затронутый scene/input/room/editor generator. Production runtime/API/dependency
inputs в CORE8.4 не менялись. Новых runtime/signature/DI semantics failures
на on не обнаружено; off-control для локализации ошибки не потребовался.
Результат не является измерением performance или проверкой всех .NET 11 semantics.

### Source/published UI, GPU и child lifetime

RunProbe выполнен на текущем общем MSBuild profile: Debug apphost и published
Release. По 12 сценариев на конфигурацию: room, scene, input, editor-gpu,
viewport, editor-proof и шесть lifecycle сценариев — startup-cancel,
command-cancel, stop-pending, forced-stop, parent-exit, app-stop.
Все 24 прошли; fresh NuGet consumer в этот запуск не входит.

CPU viewport, CPU Editor и GPU Editor выполнили свои 20-cycle/pixel/lifetime
профили. GPU использует прежний Vulkan/OpenGL interop transport без import
readbacks. Соответствующие reports проверяют child release, max-live=1 в Editor,
сохранение authoring state, отсутствие game load в parent, mappings=0,
UI sessions=0 и GPU resources/handles=0. Room/scene/input сохранили PNG evidence
и собственные проверки child/resource/input поведения.

Первый Debug GPU trial завершился до GPU startup на CS0234/CS0012:
generated game projects не ссылались на TrueMoon.Contracts для
IGameSessionEntry/IServiceResolver. Исправлены EditorViewportProbe,
SceneInputProbe, RoomViewportProbe и SceneViewportProbe через прежний
TrueMoonContractsPath. Исходная ошибка и логи сохранены; transport, assertions
и runtime-async defaults не менялись. Validation layer взят из существующего
TestResults/EditorWindow-20261104/tools/validation; SHA DLL/JSON сохранены.
Системный driver и настройки не менялись.

Это регрессия существующего ограниченного primitive/viewport/UI профиля.
Новый mesh surface renderer f.4, physical held-button/input/DPI/device loss,
K7/VRAM, другие OS/RID/AOT и performance baseline остаются отдельными задачами.
Прохождение регрессии не устанавливает причины прежних diagnostics delivery,
Stop/Dispose и post-reap drain timeout и не объявляет их исправленными.

### Команды, сборка и хранение

Из src, для каждого проекта в таблице и каждой конфигурации Debug/Release
(<run> — абсолютный путь TestResults/CORE84-20261111-185124):

```powershell
dotnet msbuild <project>/<project>.csproj -nologo -property:Configuration=<Debug|Release> -getProperty:TargetFramework,Features,LangVersion,NETCoreSdkVersion '-bl:<run>/<project>/evaluation-{}.binlog'
dotnet test <project>/<project>.csproj -c <Debug|Release> --no-build --list-tests --settings <run>/serial.runsettings --verbosity minimal '-bl:<run>/<project>/<configuration>/discovery-{}.binlog'
dotnet test <project>/<project>.csproj -c <Debug|Release> --settings <run>/serial.runsettings --logger 'trx;LogFileName=tests.trx' --results-directory <run>/<project>/<configuration> --verbosity minimal '-bl:<run>/<project>/<configuration>/msbuild-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c <Debug|Release> --settings <run>/serial.runsettings --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.NativeRuntimeProcessTests' --logger 'trx;LogFileName=tests.trx' --results-directory <run>/affected-<configuration> --verbosity minimal '-bl:<run>/affected-<configuration>/msbuild-{}.binlog'
& ./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/core84-20261111-185124-final -Configurations Debug,Release -IncludeViewport -IncludeEditorGpu -IncludeScene -IncludeInput -IncludeRoom -GpuValidationLayerDirectory TestResults/EditorWindow-20261104/tools/validation -Scenarios startup-cancel,command-cancel,stop-pending,forced-stop,parent-exit,app-stop
dotnet build TrueMoon.slnx -c <Debug|Release> --verbosity minimal '-bl:<run>/solution-<configuration>-{}.binlog'
```

Файл serial.runsettings задаёт RunConfiguration.MaxCpuCount=1,
xUnit.ParallelizeTestCollections=false и xUnit.MaxParallelThreads=1.
Точные массивы аргументов сохранены рядом с результатами.
Финальная обычная incremental solution сборка с restore: Debug — 0 errors /
0 warnings, Release — 0 errors / 0 warnings. Это не clean warning baseline;
suppression не добавлялся.

Локальные ignored artifacts: TestResults/CORE84-20261111-185124/ — source-before,
profile evaluations, 22 discovery logs/binlogs, полные TRX/logs/binlogs,
verified-tests, affected-results, native pointer, final builds, docs-before
и verification JSON. Native reports/PNG evidence:
TestResults/EngineSessions/core84-20261111-185124-final/.
Первый неуспешный native trial сохранён в соседнем каталоге без суффикса -final.
Ignored artifacts могут отсутствовать в другом checkout; команды, результаты,
изменения и границы остаются в этой versioned записи.

Следующий этап — CORE8.5: свежая согласованная NuGet/runtime/analyzer/native
поставка, source/published/fresh consumers и итоговая документация.
Прежние release/physical/reliability gates сохраняют свои критерии и владельцев.

<a id="core-status-before-core84-2026-11-11"></a>
## STATUS до CORE8.4 — архив 2026-11-11

# Core: состояние и точка продолжения

**2026-11-11 — CORE8.3 завершён: согласованный .NET11 frozen game build.**
[CORE8](PLAN.md#core8-dotnet11), [реализация, команды и границы](HISTORY.md#core83-frozen-2026-11-11).
Target/stable SDK major следуют TargetFrameworkAttribute Aether (MSBuild TF);
SDK11.0.100/net11.0/runtime-async=on, прежние dependency/package versions сохранены.

| Проверка | Текущий результат |
| --- | --- |
| Full Aether |3243/3243 каждый Debug/Release, failed/skipped0|
| Full Editor |1496/1496 каждый Debug/Release, failed/skipped0|
| SDK11-only root |Только SDK11.0.100/runtime11.0.0; actual frozen repository game compile/reuse/Open и independent Runtime Step/checkpoint/Stop/reap, source Debug/published Release;2actual children|
| Ordinary solution build |Debug0errors/175warnings, Release0errors/175warnings, incremental с restore|

Manifest schema1/lock schema1 сохранены, target=net11.0; старые artifacts требуют
явной пересборки. Features захватываются в evaluation/csc/fingerprint. SDK policy,
capture bounds, source identity, cache verification и ownership не ослаблены.
Исправлены helper shell и ошибочное предположение об уникальности OS PID между
последовательными child generations; проверяются valid SessionIdentity и cleanup.
[Предыдущий STATUS целиком](HISTORY.md#core-status-before-core83-2026-11-11).

CORE8.1 inputs/CPM/configuration delivery и CORE8.2 Cobalt/Enerit suites/packages
имеют собственные proofs; не считаются повторёнными текущими двумя suites.

## Следующий конкретный шаг

**CORE8.4:** сверка всех11test projects и оставшаяся runtime-async regression на
окончательных inputs; Aether/Editor full уже прошли в CORE8.3. Далее CORE8.5 fresh
packages/native/GPU/full delivery и документация. Warnings, прежние diagnostics/
drain reliability причины и physical/release gates остаются открытыми.

<a id="core83-frozen-2026-11-11"></a>
## CORE8.3: Aether frozen game build на .NET 11 — 2026-11-11

Завершён согласованный Windows x64/net11.0 профиль сборки и загрузки игры.
Исходный HEAD2553f87d248cf748c858988e0ed38343290eda8c и накопленные пользовательские
изменения CORE8.0–2 сохранены; package/dependency versions, SDK pin и общие props
не менялись. SDK11.0.100/runtime11.0.0, LangVersion latest, runtime-async=on.
Новая game заявка TALES-20261110-ae80510f не принималась и не редактировалась.

### Реализация и migration boundary

- Внутренний GameBuildProfile получает target/stable SDK major из стандартного
  TargetFrameworkAttribute самой Aether assembly, сформированного MSBuild из TF.
  Все compiler/preparation args, SDK selection, evaluated witness, package lock
  target и manifest используют один профиль. Entry host/working directory не
  становятся источником target. Editor preflight требует тот же stable SDK major.
- global.json сохраняет ограниченные disable/patch/latestPatch/latestMinor,
  exact installed SDK, runtime pin и read leases. Старые/будущие major, prerelease,
  allowPrerelease и неподдержанная rollForward policy отклоняются до SDK process.
  Попытки не ослабляют capture quotas, native/managed bounds или cleanup.
- Declarative Features разрешён в прежней property allowlist: общий runtime-async
  проходит через обычный captured MSBuild/csc, evaluation и compiler fingerprints.
  Functions/tasks/custom SDK и прочие прежние неподдержанные inputs не разрешались.
  SourceLink/Git metadata discovery отключены стандартными global MSBuild properties
  только в frozen job: Git checkout не становится новым неучтённым source input.
- Lock остаётся schema1 с одним net11.0 target без RID. Managed single-lib admission
  добавляет net11.0, сохраняя net10/net9/net8/netstandard2.1/2.0 и прежние проверки
  layout/unsigned/edges/hash/declared DLL. Нет сетевого package resolution в frozen
  preparation. Пакеты, lock и источники не переписываются сборкой.
- Manifest остаётся schema1: target уже является частью content-addressed bytes.
  Новый target=net11.0 и shared contract hash отделяют artifacts. Open старого
  net10 (либо неподдержанного target) сообщает expected net11/rebuild до game load.
  Нужна явная пересборка; auto-migration/relabel старых artifacts, новая схема и
  persisted cache не вводились. Process-local reuse сохраняет проверки inputs,
  compiler/payload и republish для нового SourceRevision.
- Fixtures и6manual game-project writers переведены на текущий target. Тестовые
  local hold helpers используют системный Windows PowerShell, $PID и process-only
  ExecutionPolicy Bypass для своих скриптов; global policy не меняется. Это устраняет
  зависимость helper от исчезнувшего прежнего PowerShell7 executable и Restricted
  Windows PowerShell policy. Отмена по-прежнему проверяет actual owned process tree.
- В процессе full acceptance Windows переиспользовала PID завершённого child.
  Проверка уникальных PID за все8последовательных leases была неверным предположением:
  все leases были reaped/released. Probe теперь сохраняет SessionIdentity, тест
  проверяет distinct valid identities, прежние3/8leases, PID>0/не parent и каждый
  cleanup; replacement max-live1/rollback/Stop fence assertions сохранены.

### Выполненные проверки

Исходный RepositoryGameProject test воспроизведён1/1failed на SelectSdk major10;
после первого исправления1/1passed. Первый full trial остановлен после подтверждённых
fixture failures (PID reuse и недоступный hold helper), не считается полным run.
После первых исправлений focused21/21, Failed:0, Skipped:0 и промежуточные full Aether3242/Editor1496 D/R прошли. Затем SDK11-only proof выявил ambient SourceLink: frozen policy закрепляет EnableSourceControlManagerQueries=false и EnableSourceLink=false, сохраняя compiler rejection для sourcelink switch. Добавлен actual repository-artifact-root случай; заключительные обычные full suites
с restore/build, без DirectoryPackagesPropsPath/test-only props overrides:

| Проект | Debug | Release | Failed / skipped |
| --- | --- | --- | --- |
| TrueMoon.Aether.Tests |3243/3243|3243/3243|0/0|
| TrueMoon.Aether.Editor.Tests |1496/1496|1496/1496|0/0|
| Итого на конфигурацию |4739/4739|4739/4739|0/0|

9new cases:5SDK policy negatives,2rehashed legacy/future manifest targets и
дополнительный compatible package lib target и artifact root внутри Git checkout. Это targeted regression, не новая
coverage/performance программа. Existing Builds/source observer/reuse/cancel/
App/DI/native-child/lifetime проверки выполнены в full Aether/Editor scope.
Process-heavy suites запускаются последовательно; VSTest settings:
ParallelizeTestCollections=false, MaxParallelThreads=1.
[Синтаксис xUnit RunSettings](https://xunit.net/docs/config-runsettings).

| Requirement | Evidence |
| --- | --- |
| SDK/target/Features/manifest | GameBuildPreparationTests.RepositoryGameProject_FreezesAncestorPropsAndContractReferences |
| Unsupported SDK policy до process admission | GameBuildPreparationTests.FrozenSdk_RejectsUnsupportedSelectionBeforeSdkAndDrains |
| Old/future artifact rejection после корректного rehash | GameBuildTests.RehashedManifest_StillValidatesSchemaContractAndDependencyPolicy |
| Locked packages, changed bytes и verified reuse | GameBuildPreparationTests.Packages_BuildLockedTransitiveDllsVerifyChangesAndReuseOnlyVerifiedPayload (net11/net10 lib) |
| Compiler cancellation/input changes/drain | GameBuildInputsTests.ChangeOrCancelDuringCompilation_DrainsOwnedSdkBeforeReturning |
| Actual selected build/replacement/checkpoint/cleanup | NativeRuntimeProcessTests.CompiledGameFixtures_PreserveSelectedBuildCheckpointRollbackAndCleanup |
| Full validation | dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj и TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj, -c Debug/Release --settings <run>/serial.runsettings; Passed3243/1496, Failed0, Skipped0 |

### Actual source/published game на SDK11-only root

В ignored run создана изолированная plain copy установленного toolchain
(5688files/749848460bytes, включая matching net11 targeting packs), без изменения системной установки. В её --list-sdks
только11.0.100, --list-runtimes только Microsoft.NETCore.App11.0.0; SDK10 отсутствует.
Оба acceptance consumers фактически работают на CoreLib из этого root, проверяют
его SDK census и используют его же absolute frozen SDK/MSBuild/csc/runtime paths.
Первая неполная copy без WindowsDesktop/AspNetCore net11 reference packs дала
NU1100 до compilation; matching packs скопированы из установленного SDK, без сети.
Этот fixture failure сохранён отдельно и не объявляется production defect.

Source Debug и published Release consumer/runtime прошли отдельно. Публичный App/
UseAether/DI путь, exact parent/runtime BuildIdentity, compile actual repository
Game.Logic.RoomGame → validate manifest → VerifyInputs → verified payload reuse/
new ticket → Open → actual independent AetherRuntimeHost child → Step1/checkpoint1
→ Stop/reap выполнены в обоих случаях. Parent не загружает game code; child report
подтверждает ровно выбранный artifact path/SHA, GameLoaded=true, Editor/UiLoaded=false,
свой CoreLib из изолированного root и Resources/Handles0. Compiler ProcessId=null,
каждый из2actual children Reaped/ResourcesReleased=true. Это headless/CPU/control
acceptance, не native surface/GPU proof и не fresh NuGet consumer.

### Команды, результаты и хранение

Из src, для каждого проекта/конфигурации:

```powershell
dotnet test <project>/<project>.csproj -c <Debug|Release> --settings <run>/serial.runsettings --logger 'trx;LogFileName=tests.trx' --results-directory <run>/<project>-<configuration> --verbosity minimal '-bl:<run>/<project>-<configuration>/msbuild-{}.binlog'
dotnet build <run>/consumer/Consumer.csproj -c Debug --verbosity minimal '-bl:<run>/consumer-build-Debug-{}.binlog'
dotnet publish Examples/AetherRuntimeHost/AetherRuntimeHost.csproj -c Release --self-contained false --output <run>/runtime-published --verbosity minimal '-bl:<run>/runtime-publish-Release-{}.binlog'
dotnet publish <run>/consumer/Consumer.csproj -c Release --self-contained false --output <run>/consumer-published --verbosity minimal '-bl:<run>/consumer-publish-Release-{}.binlog'
<run>/dotnet11-only/dotnet.exe exec --fx-version 11.0.0 <source-or-published-Consumer.dll> <matching-AetherRuntimeHost.dll> ManualTests/Game.Logic/Game.Logic.csproj <report> <run>/dotnet11-only <delivery>
dotnet build TrueMoon.slnx -c <Debug|Release> --verbosity minimal '-bl:<run>/solution-<configuration>-{}.binlog'
```

Для proof processes DOTNET_ROOT/DOTNET_ROOT_X64/DOTNET_HOST_PATH/PATH указывают
на isolated root, DOTNET_MULTILEVEL_LOOKUP=0; эти overrides не записаны в систему.
Compile/publish самого acceptance host выполнены штатным установленным SDK;
игру frozen compiler собирает уже в фактически выбранном SDK11-only runtime root.

Финальные ordinary incremental solution builds с restore: Debug0errors/175warnings,
Release0errors/175warnings. Это counts текущих recompiled projects,
не clean warning baseline и не обещание отсутствия warnings. Public API surface/
namespaces/ownership и package versions не перерабатывались; Profile — internal.
Руководства Aether/Editor/packaged README обновлены, исторические dated net10
GPU/performance proofs сохранены с исходными датами.

Raw artifacts локальны, ignored TestResults/CORE83-20261111-160620/: baseline TRX,
initial-aborted console/binlog, focused TRX, full4TRX/logs/binlogs, isolated-sdk.json/
toolchain copy, consumer sources/commands/results и2child summaries, final builds,
source-before/docs-before/guide-edits/verification JSON. Срок retention не гарантирован;
inputs/commands/outcomes/scope сохранены в versioned HISTORY независимо от этих файлов.

### Что дальше

CORE8.4: сверка всех11test projects и оставшаяся runtime-async regression на
окончательных inputs; CORE8.5: fresh package/native/GPU/full delivery и финальная
сверка документации. Прежние diagnostics delivery/Stop/Dispose/post-reap drain
timeout причины не закрываются успешным replay. Physical input/device loss/K7/
VRAM/other OS/RID/AOT/performance и renderer f.4 остаются своими задачами.

<a id="core-status-before-core83-2026-11-11"></a>
## Core STATUS до CORE8.3 — архив 2026-11-11

# Core: состояние и точка продолжения

**2026-11-11 — CORE8.2 завершён: генераторы, runtime и DI adapter на .NET11.**
[План CORE8](PLAN.md#core8-dotnet11), [команды, исправления и ограничения](HISTORY.md#core82-generators-2026-11-11).
SDK11.0.100/net11.0/runtime-async=on; SDK Roslyn5.12, pinned test Roslyn5.9,
Microsoft DI11. Смена зависимостей и production API не потребовалась.

| Проверка | Текущий результат |
| --- | --- |
| Cobalt runtime/adapter |179/179 каждый Debug/Release, failed/skipped0|
| Cobalt generator |96/96 каждый Debug/Release, failed/skipped0|
| Enerit generator |114/114 каждый Debug/Release, failed/skipped0|
| Enerit runtime/process |354/354 каждый Debug/Release, failed/skipped0|
| Independent packages |8fresh packages;132actual DI/IPC process scenarios, D/R build/publish, оба DI и microsoft-only без Cobalt analyzer; CS0234 negative control прошёл|
| Ordinary solution build |Debug0errors/510warnings; Release0errors/1414warnings, incremental с restore|

Исправлены два прежних test expectations: single cleanup Composite aliases
Microsoft DI11 и сравнение trace flags с Recorded mask header v1. Production
runtime/generator/adapter C# и wire не менялись. SDK requirements generator
metadata/guides уточнены до11. Первоначальные failures сохранены в evidence.

CORE8.1 ранее завершил61net11projects,45runners/общий MSBuild profile, CPM isolation
и16package configuration delivery; [его результаты](HISTORY.md#core81-profiles-2026-11-11)
не считаются повторёнными CORE8.2. Предыдущий STATUS сохранён
[целиком](HISTORY.md#core-status-before-core82-2026-11-11).

## Ограничения и следующий конкретный шаг

**CORE8.3:** Aether frozen game SDK/TFM, locks/manifests/cache и ordinary nested
restore; затем actual artifact→Runtime→stop/reap source/published. SDK10-only
selector пока не исправлен. CORE8.4 — все11test projects и wider runtime async
regression; CORE8.5 — итоговая delivery/native/GPU/documentation acceptance.
Предупреждения сборки и прежние diagnostics/drain/physical/release gates открыты.

<a id="core82-generators-2026-11-11"></a>
## CORE8.2: генераторы, runtime и DI adapter на .NET 11 — 2026-11-11

Этап завершён в Windows x64 managed scope. HEAD до/после проверки:
`2553f87d248cf748c858988e0ed38343290eda8c`; существующая незакоммиченная миграция
CORE8.0/8.1 сохранена. SDK11.0.100, MSBuild18.12.2, runtime11.0.0, TF/TestsTF=net11.0,
LangVersion=latest, runtime-async=on, base Version=0.3.0.1-alpha. xUnit2/VSTest
сохранены. SDK compiler Microsoft.CodeAnalysis.CSharp5.12.0.0; тестовые dynamic
compilations используют pinned5.9.0.0. Microsoft DI фактически11.0.0.0.
Смена Roslyn/dependency versions для совместимости не потребовалась.

### Полные тесты и исправленные ожидания

| Проект | Debug | Release | Failed / skipped в итоговом запуске |
| --- | --- | --- | --- |
| TrueMoon.Cobalt.Tests, runtime/adapter |179/179|179/179|0/0|
| TrueMoon.Cobalt.Generator.Tests |96/96|96/96|0/0|
| TrueMoon.Enerit.Generator.Tests |114/114|114/114|0/0|
| TrueMoon.Enerit.Tests, runtime/process |354/354|354/354|0/0|
| Всего, четыре проекта |743/743|743/743|0/0|

Первый полный запуск сохранён отдельно: Cobalt178/179 и Enerit353/354 в каждой
конфигурации; обе generator suites сразу прошли. Ошибки локализованы до правок:

- `CompositeAliases_ResolveSameConcreteSingleton`: выбранный Microsoft DI11
  освободил общий concrete singleton один раз вместо прежних ожидаемых трёх.
  Production adapter не менялся. Проверка сохраняет shared identity всех aliases,
  добавляет отсутствие раннего Dispose, ожидает один cleanup и проверяет повторный
  Dispose resolver без повторного освобождения. Это наблюдение конкретной версии,
  не перенос Cobalt continue-on-error guarantees на Microsoft DI.
- Process `trace`: фактические client flags3/server flags1, remote parent и kind
  корректны. В .NET11 локальный ActivityTraceFlags содержит RandomTraceId=2;
  существующий header v1 пишет и принимает только Recorded=1. Probe и родительский
  тест теперь сравнивают server flags с `ClientFlags & Recorded`, отдельно сохраняя
  исходные client/propagated/server значения. Trace/Span identity, privacy
  tracestate/baggage, stop/drain/reap assertions сохранены. Wire/runtime код не менялся.
  [Определение ActivityTraceFlags](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitytraceflags).

После правок прошли focused adapter1/1 и PipesProcessTests7/7, затем обе полные
runtime suites заново D/R. Итоговая таблица объединяет эти четыре full runtime runs
с четырьмя generator runs на неизменённых generator sources; focused cases повторно
в сумму не включены. Нет новых skipped, расширения deadlines или отключения guards.

### Независимая пакетная генерация и исполнение

`ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1` собрал8fresh nupkg версии
`0.3.0.1-alpha.units.20261111103406764`: TrueMoon, Contracts, Core, Cobalt, Enerit,
Enerit.Generator, Extensions.DependencyInjection, Mithril. Consumers скопированы
в изолированную папку с собственными props/global.json/NuGet config, свежим cache
TrueMoon packages и local feed; source ProjectReference нет. Внешние зависимости
могут читаться из штатного fallback cache, локальная package closure проверяется.

Матрица: both-generators на Cobalt и Microsoft DI, microsoft-only без Cobalt
analyzer, каждый Debug/Release и build/publish;11сценариев на комбинацию,
**132actual process scenarios**, Completed=true. Payload/concurrent/cancel-request/
remote-error/restart/budget/stop-pending/startup-failure/parent-exit/saturated-stop/
saturated-kill проверяют generated DI/IPC, pending/workers, lifecycle и child cleanup.
120cases подтвердили pending0 и завершённые workers; в12parent-exit cases
проверен выход собственного child, а in-process counters после смерти parent
не наблюдаются (pending=-1, WorkersCompleted=false).
В D/R consumer both-generators имеет10generated sources, microsoft-only4;
проверены compiler/analyzer paths/SHA, bundled Cobalt buildTransitive layout,
явный Enerit analyzer, metadata/runtime closure и отсутствие analyzer/Roslyn/native
graphics DLL в runtime output. Negative build без Enerit generator ожидаемо дал
exit1/CS0234. Автоматический binlog добавлен всем restore/build/publish/pack этого
runner; contracts, source payload и сценарии не изменены.

Descriptions обоих generator csproj и package guides Cobalt/Enerit.Generator
исправлены с требования SDK10 на SDK11; TFM берётся из прежнего `$(TF)`.
Из1769исходных code/build/script hashes изменились ровно6файлов:3test/probe C#,
2generator csproj только Description и этот PowerShell runner только binlog hook.
Runtime/generator/adapter production C# и shared SDK/dependency profile сохранены.

### Команды и финальная сборка

Из `src`, для каждого из четырёх проектов выше и каждой Debug/Release:

```powershell
dotnet test <project>/<project>.csproj --configuration <Debug|Release> --logger 'trx;LogFileName=tests.trx' --results-directory <run>/<project>-<configuration> --verbosity minimal '-bl:<run>/<project>-<configuration>/msbuild-{}.binlog'
pwsh -NoProfile -File ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1 -RunDirectory <units-run>
dotnet build TrueMoon.slnx -c Debug --verbosity minimal '-bl:<run>/final-Debug-{}.binlog'
dotnet build TrueMoon.slnx -c Release --verbosity minimal '-bl:<run>/final-Release-{}.binlog'
```

Full suites и финальный solution build выполнялись с обычным restore/build,
без `--no-build`/`--no-restore` и без test-only DirectoryPackagesPropsPath overrides.
Последние incremental solution builds: **Debug0errors/510warnings,
Release0errors/1414warnings**. Counts зависят от recompiled projects; это не
доказательство уменьшения warnings backlog. Массового suppression нет.

Raw artifacts: ignored `TestResults/CORE82-20261111-152049/` (первоначальный
`test-results.json`, full runtime reruns в `runtime-final/`, TRX/logs/binlogs,
host-versions/source-before/docs-before/final-build-results/verification JSON),
и `TestResults/UnitsPackages/core82-20261111-153406/` (summary/commands/generated
hashes/local feed). Хранение локальное, вне Git; гарантированный срок retention
не задан. Дата, inputs, команды, outcomes и ограничения сохранены здесь независимо
от доступности raw artifacts.

Во время финальной сверки прежний `C:\Program Files\PowerShell\7\pwsh.exe`
стал недоступен. Final solution builds и документирование выполнены через
Windows PowerShell5.1; OS/shell не переустанавливались. Ранее завершённые package
checks через PowerShell7 этим не повторяются и не теряют собственной даты/scope.

### Границы и продолжение

Проверены четыре suites, не все11test projects. .NET11 source/package compiler
load и фактическое generated execution прошли; production APIs/wire/ownership
не переработаны. Нет нового benchmark/other OS/RID/AOT/native/GPU proof и публикации
во внешний feed. Предыдущая CORE8.1 configuration collection имеет собственный
scope и не включена в132IPCcases. Следующий этап — CORE8.3 Aether frozen game
SDK/manifest/cache/ordinary nested restore; CORE8.4/5 full regression/release открыты.

<a id="core-status-before-core82-2026-11-11"></a>
## Core STATUS до CORE8.2 — архив 2026-11-11

# Core: состояние и точка продолжения

**2026-11-11 — CORE8.1 завершён в build-input/profile scope.**
[Этапы CORE8](PLAN.md#core8-dotnet11), [команды, результаты и ограничения](HISTORY.md#core81-profiles-2026-11-11).
Общий профиль SDK/TFM применяется в projects и active runners; C# sources и версии
пакетов сохранены. Это готовность инфраструктуры сборки, не всего выпуска.

| Проверка | Текущий результат |
| --- | --- |
| Framework/CPM | 61 проекта net11.0; 53 root CPM, 8 независимых package consumers без CPM |
| Scripts | 45 active runners на общем MSBuild profile; 68 scripts parse errors 0 |
| External restore | NU1008 воспроизведён и устранён; обычные restore/build SDK11 и SDK10 вне checkout прошли с исходным DirectoryPackagesPropsPath |
| Cobalt package consumer | По 11 checks в Debug/Release, bundled analyzer/generation и отрицательные CS1591 controls прошли |
| Configuration delivery | 16 fresh packages; 15 source/published/fresh profiles, по 230 checks и 4 owned children на вид поставки; оба DI, stop/reap |
| Ordinary solution build | Debug 0 errors / 1556 warnings; Release 0 errors / 1219 warnings, с restore |

`ManualTests/BuildProfile.ps1` получает выбранные SDK/TF/TestsTF/Version/LangVersion/
Features через MSBuild, сохраняет input SHA/binlog и экспортирует независимый
snapshot. Root CPM ограничен своим каталогом, явный opt-in сохраняется; obsolete
dotnet-xunit удалён без смены xUnit/VSTest. Package-only Aether consumers изолированы.

Native/UI/performance runners здесь не прошли полную execution acceptance.
Полные test suites/discovery не запускались; managed child proof не включает
renderer или game. SDK10-only frozen game selector остаётся CORE8.3 blocker.
После его исправления отдельно проверить lock/manifest/compiler/load/cleanup.
Исторические diagnostics/drain и physical/release gates сохраняют свои границы.
[Предыдущий STATUS](HISTORY.md#core-status-before-core81-profiles-2026-11-11).

## Следующий конкретный шаг

**CORE8.2:** полные Cobalt/Enerit generator suites и Cobalt runtime/adapter в D/R,
проверка Roslyn/compiler host и независимых generated DI/IPC consumers. Далее
CORE8.3 frozen game profile, CORE8.4 full runtime regression и CORE8.5 итоговая
source/publish/package/native acceptance и документация.

<a id="core81-profiles-2026-11-11"></a>
## CORE8.1: профили скриптов, CPM и независимая поставка — 2026-11-11

Завершена общая инфраструктура сборки CORE8.1 в managed scope. SDK и TFM взяты
из начатых пользователем настроек: SDK 11.0.100, net11.0, версия 0.3.0.1-alpha,
runtime-async=on. Предыдущий срез [TF/TestsTF](#core81-tfm-2026-11-11) сохранён.
C# runtime/generator sources, public APIs и SDK pin в этом продолжении не менялись.

### Изменения

- `ManualTests/BuildProfile.ps1` получает TF, TestsTF, выбранный SDK, Version,
  LangVersion и Features через обычную MSBuild evaluation. Профиль сохраняется
  вместе с binlog и SHA исходных props/global.json/helper. Ошибки evaluation
  наблюдаемы; скрытого fallback на прежний SDK нет.
- 45 активных PowerShell runners используют общий профиль вместо `net10.0` и
  `10.0.100`. Пути source test harnesses учитывают TestsTF, runtime/package
  consumers — TF. В 18 runners адресно уточнены 23 source output expressions.
  Параметры с путями по умолчанию вычисляются после чтения профиля. Обе csproj
  templates используют `$(TF)`. Исторический header Summarize.ps1, описывающий
  прежние performance samples, оставлен net10.0; старые результаты не переименованы.
- Копированные consumer props получают явные TF/TestsTF и SDK pin. Для новых
  независимых проектов writer создаёт минимальные props со снимком compiler
  settings и отключённым CPM. Package metadata репозитория не наследуется.
  Основной ConfigurationDelivery runner использует этот writer и записывает
  полный BuildProfile в verification manifest.
- Корневой Directory.Packages.props включает CPM по умолчанию только для своих
  descendants. Сравнение учитывает полный prefix с separator и Windows casing;
  явный ManagePackageVersionsCentrally сохраняется. При намеренном внешнем
  использовании root CPM его можно явно включить. Неиспользуемый
  DotNetCliToolReference dotnet-xunit 2.3.1 удалён; xUnit 2/VSTest packages сохранены.
- AetherPackageConsumer/Consumer получил собственный Directory.Packages.props:
  его три проекта с явными package versions больше не наследуют root CPM.
  Остальные package consumers сохраняют прежнюю изоляцию.

Распределение по всем 61 проектам после изменений: net11.0 у каждого,
53 workspace projects используют root CPM, у восьми package-only consumers
CPM отключён (семь false, один unset/default false). Это evaluation, а не
61 independent package build. PackageVersion entries и их версии сохранены.

### Воспроизведение и исправление restore

Два выделенных проекта созданы вне checkout, с обычными global.json/NuGet.Config.
В окружение передан исходный DirectoryPackagesPropsPath, указывающий на root file.
Никакая временная копия props и никакой test-only version override не применялись.

| Сценарий | До | После |
| --- | --- | --- |
| SDK 11.0.100, net11.0, явная версия Microsoft.Extensions.DependencyInjection 11.0.0 | Restore exit 1, NU1008: унаследованный CPM конфликтует с Version | Обычные restore и build exit 0 |
| SDK 10.0.404, пустая net10.0 library | Restore exit 0 | Обычные restore и build exit 0 |

SDK10 case — контроль отсутствия регрессии и границы импорта, а не поддержка
net10 consumers нового фреймворка. Ошибка старого dotnet-xunit в этом запуске
не воспроизвелась; его удаление не выдается за устранение наблюдавшегося tool failure.
Подтверждённый и устранённый отказ здесь — NU1008 внешнего проекта.

Команды: dotnet restore/build <external-project> --verbosity minimal
-bl:<case>-{}.binlog. Проекты работают с выбранным SDK через их global.json,
source package cache и пустой список feed; framework references уже установлены.

### Реальные проверки на .NET 11

| Проверка | Точный результат |
| --- | --- |
| PowerShell syntax | 68 scripts разобраны, parse errors 0; 45 runners переведены на профиль |
| Reflection runner с default path | Загрузил SkiaSharp 4.155.0.0 из bin/Debug/net11.0; renderer/GPU не создавались |
| CobaltGeneratorConsumer/Run.ps1 | Два свежих локальных пакета, consumer Debug/Release: по 11 checks, 9 generated sources, bundled analyzer SHA; отрицательный CS1591 control в каждой конфигурации прошёл |
| ConfigurationDelivery | 16 пакетов версии 0.3.0.1-alpha.core81.20261111145823; 5 consumers × source/published/fresh = 15 profiles |
| Checks/lifecycle | По 230 checks и 4 owned children на каждый вид поставки, всего 690 checks/12 children; Cobalt и Microsoft DI, bootstrap/identity, graceful stop/reap |
| Fresh graph/assets | Независимые caches/local feed, 0 ProjectReference в fresh graphs; runtime/analyzer/buildTransitive/native payload hashes проверены |
| Final ordinary solution Debug | Exit 0, 0 errors / 1556 warnings, restore включён |
| Final ordinary solution Release | Exit 0, 0 errors / 1219 warnings, restore включён |

Команды: ./ManualTests/CobaltGeneratorConsumer/Run.ps1 и
./ManualTests/ConfigurationDelivery/Verify.ps1 -EvidenceDirectory <run>/ConfigurationDelivery.
Последний сам создаёт отдельный run вне checkout; не публикует пакеты наружу,
не использует live feeds для consumers, не запускает окно или renderer.
Финальные команды: dotnet build TrueMoon.slnx -c Debug|Release --verbosity minimal
-bl:<case>-{}.binlog. Это ordinary incremental builds; разное число warnings
не объявляется их исправлением или новым nonincremental baseline.

Все исходные пользовательские изменения сохранены. C# SHA drift 0; package
versions, source version и global.json не менялись. Выполнение SDK11 configuration
delivery не подтверждает все unit/integration suites. Остальные native/UI/
performance runners получили новые inputs и прошли syntax review; их полная
execution acceptance остаётся CORE8.4/5. Benchmark jobs/measurements не менялись.

### Границы этапа и продолжение

CORE8.1 завершён в build-input/profile scope. Реальная внутренняя сборка repository
game всё ещё отклоняется SDK10-only selector из CORE8.0: это CORE8.3. После него
нужна интеграционная проверка frozen snapshots/lock/manifest/compiler и actual game
load/stop. Отдельный restore repro здесь не подменяет эту проверку. Старые post-reap
drain/diagnostics причины, physical/K7/device-loss/VRAM/VM и wider release gates
сохранены. Следующий этап — CORE8.2: полные Cobalt/Enerit generator suites и
runtime/adapter compatibility на новом SDK; package smoke этого не заменяет.

Артефакты: ignored TestResults/CORE81-Profiles-20261111-144035: source-before/diff,
before/after restore logs и binlogs, runner-migration/test-framework-paths JSON,
final-evaluations (61 projects), final-build-results, ConfigurationDelivery/
verification.json, delivery-summary и итоговый verification.json. Cobalt evidence:
TestResults/CobaltGeneratorFixes/packages-20261111-095720884. Configuration delivery
scratch: %TEMP%/TrueMoon-ConfigurationDelivery-b80263dde11a46359d28e440b33d345c.
Raw files сохраняются до очистки соответствующих TestResults/temp; команды, inputs,
outcomes и границы остаются в этой versioned записи независимо от retention.

Основание стандартного CPM/import поведения: [NuGet CPM](https://learn.microsoft.com/nuget/consume-packages/central-package-management),
[MSBuild property functions](https://learn.microsoft.com/visualstudio/msbuild/property-functions).

<a id="core-status-before-core81-profiles-2026-11-11"></a>
## Core STATUS до завершения CORE8.1 — архив 2026-11-11

# Core: состояние и точка продолжения

**2026-11-11 — CORE8.1 частично: TF/TestsTF согласованы во всех61проектах.**
[План CORE8](PLAN.md#core8-dotnet11), [изменения/команды/результаты](HISTORY.md#core81-tfm-2026-11-11).
11csproj и6props изменены; C# source, package versions и SDK pin сохранены.

## Текущие проверки на .NET11

| Область | Фактический результат |
| --- | --- |
| Central TFM | Все61checked-in csproj используют $(TF)/$(TestsTF) либо shared inheritance;61/61effective target=net11.0 |
| Independent consumers |5local props импортируют только TF/TestsTF;8alias/isolation controls прошли |
| Ordinary solution build | Debug0errors/1556warnings; Release0errors/1556warnings, с restore |
| Extra source consumers |6проектов×D/R: все exit0;5проектов0warnings, GuiConsumer1warning/profile |
| Прежний NU1201 |4manual TFM mismatches устранены ordinary D/R build, без props override |
| Test execution | Полные suites/discovery не запускались; compilation не является behavioral proof |

Root values TF/TestsTF остаются в Directory.Build.props; metadata/features/package
defaults не переносятся в isolated consumers. Для standalone copy явно передать
выбранные TF/TestsTF либо записать их в consumer-local props.8package-only projects
проверены evaluation, fresh package build/graphs остаются отдельным delivery gate.
[Предыдущий STATUS](HISTORY.md#core-status-before-core81-tfm-2026-11-11).

## Оставшиеся блокеры и следующий шаг

Довести CORE8.1: актуальные package profiles/output paths и ordinary nested inputs.
SDK10-only Aether selector не менялся; подтверждённый в CORE8.0 отказ остаётся
CORE8.3. После него отдельно проверить snapshot/lock/manifest/compiler policies.
Root legacy dotnet-xunit сохраняется; current root restore проходит, historical
nested issue не объявлен исправленным. CORE8.2 generators и CORE8.4/5 full runtime
async regression/packages/publish/native acceptance остаются открыты.

CORE7/CORE6, feature очереди и physical/reliability/release gates сохраняют прежний
scope. Прежние net10 tests не считаются текущими net11 результатами.

<a id="core81-tfm-2026-11-11"></a>
## CORE8.1: общие TF/TestsTF во всех checked-in проектах — 2026-11-11

По запросу пользователя все61tracked csproj проверены на использование параметров
из Directory.Build.props. В11manual/consumer csproj net10.0 заменён на $(TF) либо
$(TestsTF); ещё3Aether package projects наследуют TargetFramework=$(TF) из общего
consumer props. Unit test projects уже использовали $(TestsTF). Runtime/libraries,
Examples/Benchmarks и независимые package app consumers используют $(TF);
manual test harnesses — $(TestsTF), как существующие ManualTests probes.

TF/TestsTF по-прежнему определены только в корневом Directory.Build.props и имеют
текущие значения net11.0/net11.0. Они выделены в отдельный ранний PropertyGroup.
Остальные root properties/items выполняются при _TrueMoonFrameworksOnly != true.
Пять вложенных consumer Directory.Build.props явно импортируют родительский root
в этом внутреннем режиме, когда TF/TestsTF отсутствуют, затем очищают служебные
properties. Так получают только framework values, сохраняя прежнюю границу
metadata/features/package settings. CPM определяется прежними Directory.Packages.props;
изменения package versions/SDK pin/runtime code не выполнялись.

При вынесении consumer из checkout выбранные TF/TestsTF передаются явно через
его собственный Directory.Build.props либо global -p:TF=... и -p:TestsTF=...;
ссылки на repo-only contracts/metadata/native assets для выбора TFM не нужны.
Копированные проекты не должны получать пустой TargetFramework или молча выбирать
старый SDK. Versioned source/template csproj теперь используют общий параметр;
отдельные generated runtime/test fixtures, compiler lock/manifest и исторические
net10 literals не заменялись массово: это CORE8.3 и дальнейшие delivery runners.

### Фактическая проверка

Windows x64, SDK11.0.100/runtime11.0.0, Version0.3.0.1-alpha, root runtime-async=on;
task-local CLI/HTTP cache, штатный существующий NuGet package cache. Каждая MSBuild
команда имеет отдельный binlog, команды/console/exit сохранены. Никаких temporary
props overrides, --no-restore или подавления warnings в build не использовалось.

| Scope | Результат |
| --- | --- |
| XML audit всех61csproj плюс local props | Нет literal TargetFramework/TargetFrameworks в checked-in csproj; все — $(TF), $(TestsTF) либо shared $(TF) inheritance |
| MSBuild evaluation всех61projects |61/61exit0; каждый TargetFramework/TF/TestsTF=net11.0 |
| Alias controls,3cases | TF и TestsTF задавались разными global значениями; runtime, unit test и manual harness выбрали ожидаемый параметр |
| Independent copy controls,5cases | Реальные consumer csproj/props скопированы под isolation barrier; explicit TF/TestsTF работают, root Version/Features и sentinel parent metadata не наследуются |
| dotnet build TrueMoon.slnx -c Debug/Release | Каждый exit0,0errors/1556warnings, ordinary restore включён |
| AetherCameraContractProbe D/R | Каждый exit0,0errors/0warnings |
| AetherSourceWatchPerformance D/R | Каждый exit0,0errors/0warnings |
| AlloyFactoryConsumer D/R | Каждый exit0,0errors/0warnings |
| CoreHeadlessProbe D/R | Каждый exit0,0errors/0warnings |
| RichTextEditorTest D/R | Каждый exit0,0errors/0warnings |
| AetherPackageConsumer/Gui D/R | Каждый exit0,0errors/1warning |

Итого14build commands (solution×2 и6extra source projects×2), все exit0.
Прежние4manual NU1201 из CORE8.0 устранены полным ordinary restore/build в D/R.
ProcessOutputProbe дополнительно не повторялся: он уже входит в solution graph
через ProjectReference.61project evaluation не является61independent package build:
8package-only consumer projects требуют выбранной package version/feed и полного
delivery profile; fresh package acceptance здесь не выполнялась.

Изменено17build/source inputs:11csproj и6props (root плюс5local consumer props).
Ни один .cs, Directory.Packages.props или global.json не менялся; пользовательские
version/dependencies/SDK/runtime-async edits сохранены. Полные unit/integration suites,
native/GPU/publish/performance acceptance не запускались. Aether SDK major10-only
selector остаётся прежним блокером CORE8.3; этот срез не объявляет game build готовым.

В первых preflight commands исправлены ошибка shell quoting {} в -bl и некорректное
условие Import с вложенными кавычками; повторные валидные evaluations и все builds
прошли. Эти промежуточные CLI/import failures не относятся к итоговому baseline.

Артефакты: ignored TestResults/CORE81-TFM-20261111-142405, source-before.json,
changed-inputs.json, projects/evaluations/profile-checks JSON,14build result/console/
arguments и уникальные binlogs, verification.json. Raw files сохраняются до очистки
TestResults; эта versioned запись сохраняет команды/scope/outcomes независимо от них.
MSBuild evaluation: dotnet msbuild <project> -nologo
-getProperty:TargetFramework,TF,TestsTF,... -bl:<case>-{}.binlog.
Build: dotnet build <solution-or-project> --configuration Debug|Release
--verbosity minimal -bl:<case>/msbuild-{}.binlog.

CORE8.1 выполнен **частично**: TFM parameterization и подтверждённые manual
mismatches закрыты. Остались active package profiles/output-path scripts и ordinary
generated-game restore inputs; генераторы/frozen SDK/manifest и итоговая delivery
проверяются по CORE8.2–5. [Общий план CORE8](PLAN.md#core8-dotnet11).

<a id="core-status-before-core81-tfm-2026-11-11"></a>
## Core STATUS до TFM-среза CORE8.1 — архив2026-11-11

# Core: состояние и точка продолжения

**2026-11-11 — CORE8.0 завершён: получен baseline SDK11 и воспроизведены blockers.**
[План CORE8](PLAN.md#core8-dotnet11), [команды/профиль/диагностика](HISTORY.md#core80-baseline-2026-11-11).
Исправления SDK/TFM/runtime code в этом этапе не выполнялись;1767source/build inputs
совпадают с исходным снимком. Пользовательские правки и локальная заявка сохранены.

## Текущие проверки на .NET11

| Область | Фактический результат |
| --- | --- |
| Профиль | Windows x64, SDK11.0.100/runtime11.0.0, net11.0, Version0.3.0.1-alpha, LangVersion=latest, runtime-async=on |
| Ordinary solution restore | exit0; штатный существующий NuGet cache, без props override |
| Ordinary solution build | Debug0errors/1556warnings; Release0errors/1556warnings; оба с restore |
| Effective projects |61/61evaluation exit0:47net11.0,14net10.0;46solution roots плюс referenced ProcessOutputProbe;14legacy targets вне solution |
|11test projects | Собраны D/R, DLL существуют; full discovery/полные suites не запускались |
| Manual restore/build |4outside-solution projects: NU1201 net10 consumer→net11 producer; camera build воспроизводит тот же restore failure |
| Async control | Camera restore с global Features=runtime-async=off даёт тот же NU1201; default on сохранён |
| Repository game build | Один existing Aether test:1executed/1failed, Unsupported frozen SDK selection policy в SelectSdk line548, до inner restore |
| Binlogs | Уникальные raw logs; errors/warnings replay подтвердил root0errors и одинаковый NU1201 on/off |

## Подтверждённые блокеры и оставшиеся проверки

CORE8.1: согласовать active/manual TFM и scripts/consumer profiles. CORE8.3:
SDK10-only Aether selector отвергает текущий global.json; затем отдельно проверить
старые snapshot/manifest/lock/compiler inputs. Root legacy dotnet-xunit сохраняется,
но в текущем root restore failure не возник. Historical nested-restore issue не
закрыт; сейчас actual game не доходит до этого шага. Новых generator loader ошибок
не наблюдалось при compilation, исполнение и package proof ещё требуются.

Runtime async on только скомпилирован; full lifecycle/configuration/IPC/Nom/runtime
regression — CORE8.4. Root warnings не подавлялись. Package/publish/native/GPU и
physical/release gates не повторялись. CORE7 остаётся завершённым на предыдущем
net10 профиле: [guide](CONFIGURATION_GUIDE.md), [proof](HISTORY.md#core76-2026-11-10).
[Предыдущий STATUS](HISTORY.md#core-status-before-core80-2026-11-11).

## Следующий конкретный шаг

**CORE8.1:** исправить подтверждённый TFM mismatch актуальных manual consumers,
согласовать старые package profiles и стандартные restore inputs. Затем CORE8.2/3
generators/frozen game build, CORE8.4 full D/R regression и CORE8.5 delivery.
CORE6 и feature/release очереди сохраняются отдельно.

<a id="core80-baseline-2026-11-11"></a>
## CORE8.0: restore/build baseline на .NET 11 — 2026-11-11

Диагностический этап завершён; исправления CORE8.1–5 ещё не начаты.
HEAD2553f87d248cf748c858988e0ed38343290eda8c/main, текущая пользовательская
working copy сохранена. Семь пользовательских SDK/TFM/dependency файлов не правились;
Features теперь композиционно задан как $(Features);runtime-async=on — эта правка
пользователя уже присутствовала при старте. Непринятая game-surface заявка и .codex
сохранены. SHA1767tracked .cs/.csproj/.props/.targets/.json/.slnx/.ps1: drift0.

### Профиль и охват

Windows10.0.26340/win-x64, SDK11.0.100, MSBuild18.12.2-1.26529.118,
Microsoft.NETCore.App11.0.0. global.json11.0.100/allowPrerelease=false/
rollForward=latestMinor, Version0.3.0.1-alpha, LangVersion=latest.
Исходные46solution projects плюс ProcessOutputProbe через ProjectReference дают
47проектов net11.0. Все61tracked csproj прошли MSBuild evaluation exit0:
47net11.0 и14net10.0. Все14net10 вне solution;11задают TFM явно и ещё3наследуют
ManualTests/AetherPackageConsumer/Consumer/Directory.Build.props.
11test projects собраны в D/R, выходные DLL проверены; full tests/discovery всех
cases не выполнялись. Root compiler logs подтверждают Features=;runtime-async=on
в Csc; это проверка компиляции, не общей behavioral совместимости runtime async.

NuGet.Config: только https://api.nuget.org/v3/index.json с mapping '*'.
Использован существующий штатный C:/Users/MEGA6/.nuget/packages; fresh-cache
acceptance этим не заявлена. DOTNET_CLI_HOME=<run>/cli-home,
NUGET_HTTP_CACHE_PATH=<run>/http-cache, first-use/telemetry/tool-path отключены
через task-local env. Restore/build запускались с разрешённым доступом к сети,
штатному package cache и обычному temp; ошибок сети/доступа в валидных прогонах нет.
Root props, dependencies, SDK/TFM/Features для ordinary commands не подменялись.

### Фактические команды и результаты

Каждая реальная команда ниже получила уникальный -bl:<case>/msbuild-{}.binlog и
-flp:logfile=<case>/diagnostic.log;verbosity=diagnostic; console output сохранён.
Ни один failed restore не скрывался --no-restore в build командах.

| Команда/scope | Результат |
| --- | --- |
| dotnet restore TrueMoon.slnx --verbosity minimal | exit0,5.45s, ordinary restore успешен |
| dotnet build TrueMoon.slnx -c Debug --verbosity minimal | exit0,19.74s,0errors/1556warnings, restore включён |
| dotnet build TrueMoon.slnx -c Release --verbosity minimal | exit0,14.85s,0errors/1556warnings, restore включён |
| dotnet restore ManualTests/AetherCameraContractProbe/AetherCameraContractProbe.csproj | exit1, NU1201: net10.0 consumer → TrueMoon.Aether net11.0 |
| dotnet restore ManualTests/CoreHeadlessProbe/CoreHeadlessProbe.csproj | exit1, NU1201: net10.0 → TrueMoon/TrueMoon.Extensions.DependencyInjection net11.0 |
| dotnet restore ManualTests/AetherSourceWatchPerformance/AetherSourceWatchPerformance.csproj | exit1, NU1201: net10.0 → TrueMoon/TrueMoon.Aether.Editor net11.0 |
| dotnet restore ManualTests/RichTextEditorTest/RichTextEditorTest.csproj | exit1, NU1201: net10.0 → TrueMoon/TrueMoon.Alloy.Hosting net11.0 |
| dotnet build ManualTests/AetherCameraContractProbe/AetherCameraContractProbe.csproj -c Debug | exit1, тот же NU1201 на restore, C# compile не начинался |
| dotnet restore ManualTests/AetherCameraContractProbe/AetherCameraContractProbe.csproj -p:Features=runtime-async=off | exit1, тот же NU1201; bounded off-control отделяет TFM mismatch от async codegen |

Последняя команда задаёт Features как global MSBuild property; исходный default
on не менялся. Root off build не выполнялся: root on build уже успешен, а текущий
manual blocker находится до compiler. Runtime behavioral сравнение — CORE8.4.

Для actual repository-game build выполнен один существующий xUnit2/VSTest test:

```text
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-build --no-restore -c Debug
  --filter FullyQualifiedName=TrueMoon.Aether.Tests.Builds.GameBuildPreparationTests.RepositoryGameProject_FreezesAncestorPropsAndContractReferences
  --logger "trx;LogFileName=game-build-repro.trx" --results-directory <case> --diag <case>/vstest.log
  -bl:<case>/msbuild-{}.binlog
```

Exit1, ровно1executed/1failed/0passed/0notExecuted, duration2.10s runner.
Original exception: System.NotSupportedException: Unsupported frozen SDK selection
policy. Stack: GameBuildPreparation.SelectSdk line548, preparation ctor/capture,
GameBuildCompiler.PrepareAsync/RunAsync, test line726. Условие minimum.Major != 10
отвергает repo global.json11.0.100 **до запуска внутреннего restore/compiler**.
Таким образом несовместимость Aether profile с SDK11 подтверждена исполнением;
это не наблюдавшаяся ошибка NuGet или compiler host loader. Fixtures/assertions
и production code не менялись. Новые вопросы cleanup/process acceptance здесь не
закрывались; прочие Aether/full-module tests не запускались.

### Разбор binlogs, предупреждения и границы вывода

Binlog MCP tools отсутствуют. Через documented dotnet msbuild <binlog> -noconlog
с errorsonly/warningsonly file loggers воспроизведены Debug и два camera on/off
restore binlogs. Replay exit0 — лишь чтение записанных событий, не новый build и
не успешный restore. Debug replay:0errors/1556warning events; camera on/off дают
одинаковый NU1201. Original logs/binlogs сохранены; бинарный файл как текст не читался.

Наиболее частые Debug warning locations: CS1591=1306, CS8669=59, CS8602=49,
CS8603=44, CS8618=27; полный список кодов в debug-warning-codes.json. Есть RS1041
и другие analyzer/nullability/xUnit warnings; они не объявлены исправленными или
новыми регрессиями SDK11 без same-input comparison. Root build не warning-clean.

Первый вспомогательный batch evaluation получил61MSB1008 из-за ошибочного
формирования массива аргументов -bl в диагностическом PowerShell runner.
Binlogs тогда не созданы, compile/restore не запускались. Это ошибка инструмента
сессии, не61ошибка проектов: исправлен только ignored runner, повтор61/61exit0.
Итоговый инвентарь использует project-evaluations-corrected/summary.json.

Новые root restore/build failures **не воспроизвелись**. Подтверждены две группы:
manual consumers TFM mismatch (CORE8.1) и Aether SDK10-only selector (CORE8.3).
Root dotnet-xunit2.3.1 остаётся, но в current SDK11 root restore не упал.
Его historical nested-restore issue не доказано исправленным и не назначено
причиной текущего game failure, который возник раньше restore. Остальные старые
snapshot/manifest/lock/package policies найдены чтением; следующие failures после
исправления selector нужно воспроизводить отдельно, не выдумывать заранее.

Следующий шаг — CORE8.1: привести актуальные workspace/manual consumers к net11,
согласовать active versus compatibility package profiles и ordinary nested inputs.
Затем CORE8.2 generator execution/package proof и CORE8.3 frozen game profile.
Полная runtime async regression11suites D/R, package/native/GPU/publish acceptance
остаются CORE8.4/5. Новых release/performance/physical proofs нет.

Raw artifacts: ignored TestResults/CORE80-20261111-140556. inventory.json,
source-before.json, working-before.diff, dotnet-info.txt, исходные SDK/props/config,
command.json/result.json/console.log/diagnostic.log и уникальные binlogs каждого
case;61corrected evaluation binlogs/JSON, command-summary.json, TRX/VSTest logs,
binlog-replay/, test-build-inventory.json, итоговый verification.json. Длинные
diagnostic logs занимают около1.2GiB; сохраняются локально до очистки TestResults,
в Git не включаются. Основные scope/commands/outcomes сохранены здесь независимо
от retention ignored files. Исходная сверка — [planning record](#core8-planning-2026-11-11).

<a id="core-status-before-core80-2026-11-11"></a>
## Core STATUS до CORE8.0 — архив2026-11-11

# Core: состояние и точка продолжения

**2026-11-11 — запланирован CORE8: восстановление solution на .NET11.**
[Единственный план и критерии](PLAN.md#core8-dotnet11),
[исходная сверка/команды](HISTORY.md#core8-planning-2026-11-11).
Реализация миграции в этой сессии не начата; семь пользовательских файлов сохранены.

## Текущая сверка

| Область | Факт на новых inputs |
| --- | --- |
| SDK/runtime | Установлены11.0.100/11.0.0; global.json выбирает11.0.100 |
| Пользовательский профиль | TF/TestsTF=net11.0, Version=0.3.0.1-alpha, LangVersion=latest, runtime-async=on; DI11.0.0/Test.Sdk18.11.1 |
| MSBuild evaluation | App, Cobalt.Generator и Core tests: exit0, ожидаемые SDK/TFM/Features; это не compile |
| Инвентарь |46solution projects,61tracked csproj,11test projects;11manual/consumer csproj и отдельный consumer props ещё net10.0 |
| Расхождения | Aether frozen compiler/SDK selector/manifest/lock policy закреплены на10; manual/package runners содержат старые SDK/TFM/output paths |
| Restore/build/test | На net11 ещё не запускались; работоспособность solution не подтверждена |

## Предыдущий проверенный профиль

CORE7.0–7.6 завершены в configuration scope на net10.0/SDK10:
[guide](CONFIGURATION_GUIDE.md), [keys](CONFIGURATION_KEYS.md),
[точные результаты и ограничения](HISTORY.md#core76-2026-11-10).
Предыдущие solution D/R0errors,7suites6945/6945 D/R и16package App/child proof
не распространяются на SDK11, новые зависимости и runtime async.
[Предыдущий STATUS целиком](HISTORY.md#status-before-core8-2026-11-11).

## Блокеры и ограничения

Первый воспроизведённый compile/runtime blocker ещё не получен. Static mismatches
выделены отдельно от доказанных failures. Общий obsolete dotnet-xunit и наследование
CPM inputs во вложенные fixtures требуют ordinary restore проверки; прежний test-only
props override не является исправлением. Сначала CLI evaluation упёрлась в запрет
записи first-use sentinel вне workspace; повтор с isolated DOTNET_CLI_HOME прошёл.
Это ограничение среды агента, не ошибка кода TrueMoon.

Новые native/GPU/physical/release результаты не получены. Старые diagnostics/drain
причины, K7/device loss/VRAM и Nom VM/power-loss scope сохраняются.

## Следующий конкретный шаг

**CORE8.0:** получить ordinary restore/build baseline с binlogs на текущем
runtime-async=on, согласовать точный support profile и классифицировать failures.
Затем CORE8.1/2 общая сборка/generators,8.3 Aether build,8.4 regression,8.5 delivery.
CORE6 и дальнейшее расширение configuration остаются самостоятельными задачами.

<a id="core8-planning-2026-11-11"></a>
## CORE8: план перехода на .NET 11, исходная сверка — 2026-11-11

Запрос пользователя — запланировать восстановление solution после частичного
обновления SDK. HEAD2553f87d248cf748c858988e0ed38343290eda8c/main.
Изменены пользователем7файлов: global.json, Directory.Build.props,
Directory.Packages.props, Benchmarks/TrueMoon.Cobalt.Benchmarks и
Benchmarks/TrueMoon.Enerit.Benchmarks csproj, Examples/AetherEditor и
Examples/AetherRuntimeHost csproj. Новая непринятая engine заявка остаётся локально,
Claim/Decision не менялись. В этом planning turn source/props/packages не правились.

Фактическое чтение: git status/diff, общие и Core PLAN/STATUS, PLANNING и requests
rules; git ls-files/XML inventory всех csproj/solution; поиск старого TFM/SDK
в tracked .cs/.ps1/.props/.targets/.csproj/.json. Найдены46solution projects,
61tracked csproj,11test projects.11manual/consumer csproj явно net10.0 плюс
ManualTests/AetherPackageConsumer/Consumer/Directory.Build.props; всего79files
с net10.0/NET10/10.0.100. Это список классификации, не79runtime defects.

dotnet --version =11.0.100; --list-sdks/--list-runtimes подтвердили установленный
SDK11.0.100 и Microsoft.NETCore.App11.0.0 рядом с прежними SDK/runtime.
global.json:11.0.100/allowPrerelease=false/rollForward=latestMinor.
Пользовательский root profile: TF/TestsTF net11.0, Version0.3.0.1-alpha,
LangVersion latest, Features runtime-async=on, DI11.0.0/Test.Sdk18.11.1;
Roslyn5.9.0 и xunit2.9.3/runner4.0.1 пока без изменений.

Выполнено только evaluation, для TrueMoon/TrueMoon.csproj,
TrueMoon.Cobalt.Generator/TrueMoon.Cobalt.Generator.csproj и
TrueMoon.Tests/TrueMoon.Tests.csproj:

```text
dotnet msbuild <project> -nologo -getProperty:TargetFramework,LangVersion,Features,Version,NETCoreSdkVersion
```

Каждый exit0, SDK11.0.100/net11.0/latest/runtime-async=on/0.3.0.1-alpha.
Первый запуск CLI без isolated home завершился UnauthorizedAccessException на
C:/Users/MEGA6/.dotnet/11.0.100.toolpath.sentinel: sandbox не разрешает эту запись.
Повтор с DOTNET_CLI_HOME=<run>/cli-home, DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1,
DOTNET_CLI_TELEMETRY_OPTOUT=1, DOTNET_NOLOGO=1 и
DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0 прошёл без расширения разрешений.
Это environmental first-use issue, не compile failure проекта.

Исходное чтение нашло SDK major10 в GameBuildPreparation.SelectSdk; net10.0 в
restore/build args, GameBuildInputSnapshot, GameBuildPackages lock target/lib
allowlist и GameBuildArtifact manifest; wrappers/fixtures/output paths также10.
Root DotNetCliToolReference dotnet-xunit2.3.1 сохраняется. Прежний CORE7.6 Aether
passing требовал bounded test-only props clone, поэтому ordinary nested restore
требуется проверить и исправить штатно. Новые restore/build/test/native/pack/publish
в этой сессии **не запускались**, первый причинный baseline остаётся CORE8.0.

Внесён [единственный подробный план CORE8.0–8.5](PLAN.md#core8-dotnet11):
baseline→common restore/build→generators→frozen Aether game profile→runtime async
и11suites D/R→source/published/fresh package delivery и docs. Runtime async on
сохранён как целевой input, off только diagnostic control. Старые artifacts требуют
явной invalidation/rebuild policy, а поддержка net10 consumers не обещана.
Общий roadmap/STATUS теперь ставят рабочий SDK11 baseline перед продолжением
feature очереди; реализация CORE7, engine/Nom и прежние release gates сохранены.

Внешняя сверка11ноября: [SDK/runtime release](https://dotnet.microsoft.com/en-us/download/dotnet/11.0),
[breaking changes](https://learn.microsoft.com/dotnet/core/compatibility/11),
[runtime/async](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/runtime).
Runtime page помечен RC2-oriented; он задаёт checklist, не доказательство поведения
установленного SDK и не основание объявлять build green.

Raw artifacts: ignored TestResults/DotNet11Planning-20261111-032635,
inventory.json/user-changes.json/evaluations.json и property logs; сохранены до
очистки TestResults. Versioned запись здесь сохраняет scope/commands/outcome без
зависимости от наличия ignored artifacts. SHA семи пользовательских файлов
сверяется после документационных правок; локальные source/.codex сохраняются.

<a id="status-before-core8-2026-11-11"></a>
## Core STATUS перед планированием CORE8 — архив2026-11-11

# Core: состояние и точка продолжения

**2026-11-11 — CORE7.0–7.6 завершены в configuration scope.**
[Рабочий guide](CONFIGURATION_GUIDE.md), [139keys/17schemas](CONFIGURATION_KEYS.md),
[единый runner](../../ManualTests/ConfigurationDelivery/README.md),
[команды/результаты/границы](HISTORY.md#core76-2026-11-10).

## Реализация и проверки

| Область | Текущий запуск CORE7.6 |
| --- | --- |
| Solution | Debug0errors/256warnings, Release0errors/190warnings, --no-restore |
|7full suites | Core661, Mithril155, Enerit354, Nom874, Hosting171, Editor1496, Aether3234 каждый D/R |
| Test total |6945/6945 каждый D/R, Failed:0, Skipped:0; не все test projects solution |
| Delivery |16packages version0.2.5.2-core76.20261110234501;5programs×source/published/fresh;230checks/profile |
| Owned children |4/profile,12total; Cobalt/MS DI, actual bootstrap/identity/control, graceful stop/reap; без renderer/game/native runtime |
| Metadata/catalog |16projects/16archives verified; public metadata17schemas/139keys; shared/module README/license/icon/analyzers/native payload hashes |
| API/examples |8managed surfaces added0/removed0;4API doc blocks compiled, guide example executed во всех3forms |
| Preservation |1673/1673baseline C#/csproj сохранены; runtime API/dependencies/TF/source version0.2.5.2-alpha не менялись |

Reader/profile/options, atomic reload/notifications/explicit writer, Core/Mithril
и модульные adapters готовы по guide. Старые callbacks и protected launch contracts
сохранены. [Предыдущий STATUS целиком](HISTORY.md#status-before-core76-2026-11-10).

## Ограничения

Aether full passing получен последовательно с documented test-only props copy:
obsolete dotnet-xunit CLI reference исключён, CPM ограничен workspace descendants.
Root props не менялся, ordinary nested restore без override не объявлен исправленным.
Старые diagnostics timeout причины не закрываются этим passing run.

Стандартные module fields StartupOnly. Configuration proof не включает новый
native/GPU/hardware/device-loss/VM/release acceptance. Полные generator/UI suites
не повторялись. Межпроцессная запись, power-loss durability, other OS/RID/AOT
и hard termination произвольного callback/driver не обещаны.

## Следующий конкретный шаг

Применять общий слой при целевых изменениях consumers; новые providers/options/
lifecycle возможности согласовывать отдельными задачами. Самостоятельные CORE6
routing/metrics/quota, Mithril MIT6, engine f.4 и wider release очередь сохранены.

<a id="core76-2026-11-10"></a>
## CORE7.6: документация, поставка и App/child consumers — 2026-11-10 UTC / итог11ноября по Екатеринбургу

По запросу пользователя завершён итоговый configuration scope CORE7.0–7.6.
HEAD2e3b2b1a00d67ef10d44e8c8deab1d811584d42d; рабочая копия предыдущих этапов
и новая game-surface заявка TALES-20261110-ae80510f сохранены, Claim не создавался.
Новый native/game host не реализовывался: это engine f.4, отдельный запрос.

### Документация и helpers проверки

[Рабочий guide](CONFIGURATION_GUIDE.md) сводит sources/приоритеты, typed options,
legacy IConfiguration против unified reader, callbacks/copies, reload/changes/
explicit writer, protected process boundary и aliases.
[Каталог](CONFIGURATION_KEYS.md) содержит139keys из17standard schemas:
types/default JSON/aliases/required, StartupOnly и symbolic entry-dependent App:Name.
Export-Keys читает public frozen metadata успешных fresh assemblies без App,
provider IO или GPU. Sensitive defaults маскируются; source/published metadata
не подменяет semantic validation и runtime configuration.

Обновлены docs/README, AGENTS, модульные ссылки и корневой README репозитория:
устаревшие source version/SDK/Mithril test count заменены текущими значениями либо
датированной навигацией; добавлена configuration точка входа. Root README выше src
заменён только по подготовленной копии и SHA guard, с разрешённым расширением
доступа к этому конкретному файлу. Source version **0.2.5.2-alpha** не менялась.

Новый [общий runner](../../ManualTests/ConfigurationDelivery/README.md) объединяет
существующие profile/App/module программы и новый BootstrapConsumer:
реальный child читает bounded RuntimeBootstrap до App, проверяет protected
identity/report/delivery и captured canonical GPU flag над legacy0, строит/
освобождает configured runtime graph без его запуска. Затем отдельный Core/Mithril
App проверяет unit/parent/control identity, source spoof77/CLI999 против actual1,
готовность и graceful stop/reap. Runtime/game/renderer не активируются.

Verification tools получили bounded параметры:
ConfigurationApiDesign/Verify.ps1 — fresh OutputDirectory вместо перезаписи старого
CORE70 evidence; Check-PackageMetadata — IncludeProject (alias Project) и NoBuild,
а CLI first-run/PATH noise отключён для машинного JSON. Production C#/csproj/
package dependencies/TF и native binaries не менялись.

### Текущие builds/tests

SDK10.0.403, net10.0, xUnit2/VSTest, Windows x64 environment.
Все команды build/test/restore/pack/publish имеют отдельный binlog.

| Проверка | Debug | Release |
| --- | --- | --- |
| Solution build, --no-restore -m |0errors/256warnings |0errors/190warnings |
| Core full |661/661 |661/661 |
| Mithril full |155/155 |155/155 |
| Enerit runtime full |354/354 |354/354 |
| Nom full |874/874 |874/874 |
| Hosting full |171/171 |171/171 |
| Editor full |1496/1496 |1496/1496 |
| Aether full |3234/3234 |3234/3234 |
|7suites total |6945/6945, failed0/skipped0 |6945/6945, failed0/skipped0 |

Solution command: `dotnet build TrueMoon.slnx --no-restore -c Debug|Release -m
"-bl:<CORE76>/solution-<configuration>-{}.binlog"`.
Test command: `dotnet test <project>/<project>.csproj --no-build --no-restore
-c Debug|Release --logger "trx;LogFileName=<suite>-<configuration>.trx"
--results-directory <CORE76> "-bl:<CORE76>/<suite>-<configuration>-{}.binlog"`.
Projects: TrueMoon.Tests, TrueMoon.Mithril.Tests, TrueMoon.Enerit.Tests,
TrueMoon.Nom.Tests, TrueMoon.Alloy.Hosting.Tests, TrueMoon.Aether.Editor.Tests,
TrueMoon.Aether.Tests. Report counts извлечены из14TRX в test-summary.json.
Это7suites, а не все тестовые проекты solution; generator/полная UI suite отдельно.

Полные Aether D/R шли последовательно после остальных SDK работ:8m24s /8m58s.
APPDATA/CLI home изолированы. Для nested fixtures test process получал ignored
DirectoryPackagesPropsPath: копия package props без obsolete dotnet-xunit2.3.1,
ManagePackageVersionsCentrally включён только для workspace descendants.
Версии PackageVersion сохранены. Root Directory.Packages.props не правился.

Это current full passing baseline на указанном input profile. Ordinary nested
restore без override не объявлен исправленным. SDK .version cross-process locking
исключён порядком запуска; прежние diagnostics timeouts в этом запуске не
воспроизвелись, их причинное reliability закрытие не выведено из passing/replay.
Прежние CORE7.5 non-green attempts остаются в истории.

### Единая поставка и реальные children

Успешный финальный proof:
**0.2.5.2-core76.20261110234501**, CORE76/delivery-complete/verification.json.
16пакетов одной версии: Contracts, Core, Cobalt, TrueMoon, Extensions.DependencyInjection,
Enerit.Generator, Enerit, Mithril, Argentis, Alloy, Rendering.Skia, Platform.Silk,
Alloy.Hosting, Aether, Aether.Editor и Nom.

| Программа | Checks на source / published / fresh profile | DI / процессный scope |
| --- | --- | --- |
| Standalone profile |15 | Pure profile, DI не требуется |
| App/reload/write |108 | Cobalt/MS DI,2actual owned children/profile |
| Modules |76 | Cobalt/MS DI, actual World/Nom admission и empty Editor startup |
| Aether bootstrap child |30 | Cobalt/MS DI,2actual owned children/profile |
| Guide example |1 | Реальный Cobalt App Start/Stop, canonical CLI name; код извлечён из guide |

Итого15program/form combinations, **230checks на каждый из3profiles**,690checks
суммарно.4owned children/profile, **12owned children** total, graceful stop и reap.
App/modules/bootstrap покрывают оба DI; standalone не зависит от DI, guide использует
default Cobalt. Source Debug и published Release используют source ProjectReference
graph; fresh Release имеет0ProjectReference, отдельный пустой cache для каждого
consumer, exact local-feed provenance и installed/deployed runtime DLL SHA equality.
No network feeds, external nupkg seeded только точных существующих versions.
WindowRun/GpuRun/NativeRuntimeStarted=false у соответствующих headless proofs.

Runner: `./ManualTests/ConfigurationDelivery/Verify.ps1 -EvidenceDirectory <fresh>`.
У него собственные run/feed/cache вне checkout и unrelated cwd. Parent env не
меняется; selected flags/identity передаются в ProcessStartInfo. Parent/child
deadlines/finally обеспечивают cleanup; верхний timeout ограничивает и убирает
owned process tree при ошибке. All recorded sources оставались неизменными.

Package payload hashes сверены с текущим shared README (15пакетов; Nom намеренно
удаляет этот item), own PACKAGE.md, LICENSE/icon, Cobalt/Enerit analyzer DLL и
targets, bundled extended Skia/native targets. Отдельный existing metadata checker
подтвердил16projects/16intended packages/16archives:
`eng/Check-PackageMetadata.ps1 -Pack -NoBuild -IncludeProject <16paths>
-DeferredProject @() -RunDirectory <fresh-under-TestResults/PackageMetadata>`.
Его final report — PackageMetadata/CORE76-verified/summary.json; metadata archives
используют source alpha version и не являются заменой primary unique-version feed.

Промежуточные verification attempts сохранены:
- Первая delivery версия0.2.5.2-core76.20261110231422 прошла12profiles/229checks
  до final shared README/guide-example проверки.
- delivery-final0.2.5.2-core76.20261110233838 остановлен строгой payload проверкой:
  Nom по проектному правилу не включает общий README. Checker учитывает это явно
  и проверяет его отсутствие; обязательный own PACKAGE.md проверяется.
- Broad metadata checker обнаружил unrelated ManualTests/CobaltGeneratorConsumer
  как packable project. Scope ограничен16library paths, чужой consumer не менялся.
  Новый параметр получил отдельное имя IncludeProject, чтобы не конфликтовать
  с PowerShell foreach $project. Далее исключён ненужный implicit restore/
  недоступный user NuGet.Config и first-run PATH noise в JSON. Все intermediate
  directories сохранены; final metadata16/16 прошёл с isolated APPDATA/CLI home.

### API, документационные примеры и сохранность

Четыре api: blocks declarations/headless/Mithril/Editor из CONFIGURATION_API.md
скомпилированы against actual assemblies Release:0errors/0warnings; не исполнялись.
Новый guide example исполнялся без изменения кода в source/published/fresh forms.

Managed public declarations8assemblies против fresh CORE7.5:
Contracts, TrueMoon, Mithril, Aether, Editor, Enerit, Nom, Hosting —
**added0/removed0**. Это managed surface comparison, не ABI/platform acceptance.
1673из1673baseline C#/csproj hashes сохранены; новый C# — только BootstrapConsumer.
Новые/изменённые proof scripts и docs не являются изменением runtime API.
Root shared files/package targets/native inputs проверены по hashes; изменения
планов/статусов выполнялись с актуальным SHA и atomic replacement.
Предыдущие STATUS сохранены целиком в своих HISTORY с исходным SHA.

Raw retention: ignored CORE76 хранит все logs/binlogs/TRX, API/payload/catalog/
source manifests и все attempts; PackageMetadata directories и independent TEMP
run/feed/cache сохранены. Автоматическая очистка может удалить raw files, поэтому
scope/commands/counts/failures/limitations зафиксированы здесь versioned текстом.
External publish/commit/push, новый native/GPU/physical/VM/device-loss/K7/VRAM
acceptance не выполнялись. CORE7 завершён в configuration scope; engine f.4 и
остальные самостоятельные module/reliability/release очереди не закрыты.


<a id="core75-2026-11-10"></a>
## CORE7.5: Aether, Editor и общая конфигурация модулей — 2026-11-10

Реализовано по запросу пользователя. HEAD2e3b2b1a00d67ef10d44e8c8deab1d811584d42d;
сохранялась незакоммиченная рабочая копия предыдущих этапов. Подробный контракт:
[модули/ключи/aliases/ownership](CONFIGURATION_MODULES.md),
[точные49method names/110cases](CONFIGURATION_MODULE_TESTS.md).
Рабочая версия0.2.5.2-alpha и внешние dependency versions/TF не менялись.

### Изменения и архитектурная проверка

- Aether Engine/ECS World/Scheduler/SceneLoading/Primitives/RenderResources/Authoring,
  Editor Options/Diagnostics/SourceEditor/Window, Enerit:Pipes, Nom и Alloy:Window:
  data-only schemas → common profile → all-schema prepare/validate →
  existing App/DI registration. Все поля StartupOnly; callback и private copies
  сохраняются. Existing Use* API продолжает работать.
- AetherComposition отделяет identity/factories/resources от AetherSettings.
  NativeRuntimeLaunch переносит frozen GPU flags только в child StartInfo;
  runtime host не читает mutable env. RuntimeBootstrap использует selected capture,
  сохраняет envelope limits/identity checks и typed parent/control; обычные sources
  не переопределяют protected launch report/delivery/identity.
- AetherEditorHost launch CLI использует общий ParseSingleUse с прежними aliases,
  inline/canonical forms, false flags, strict duplicate scalar/flag rejection и
  безопасными ошибками. Обычные Parse/ParseKnown остаются last-wins.
- Финальная сверка совместимости сохранила legacy validation env для Editor при
  отсутствующем CLI flag. ValidationSpecified/ResolveRuntimeSettings различают
  absent и explicit false; true/false перекрывают invalid lower env без его bind.
  Captured inputs объединяются до App без глобального SetEnvironmentVariable.
  Существующий CLI Fact дополнен этой regression проверкой без новых methods/cases;
  последняя полная Editor suite снова1496/1496 каждый D/R
  (editor-final-cli-Debug/Release.trx). Host опубликован Release;6source/published
  CLI executions проверили help/duplicate/redacted invalid input без window/GPU/
  runtime child. Опубликованный host DLL совпадает по SHA с проверенным Release.
  Это executable-only delta: все source hashes successful module package manifest
  повторно совпали (drift0), packed libraries/API не изменились.
- Первая полная Editor suite выявила1495/1496: LibraryArtifact_HasNoEntryPointBootstrapDependencyOrExecutableProbeTypes.
  Тест не ослаблялся. Pure schema factory/codecs/metadata и optional stage binding
  размещены в Contracts; ConfigurationSchemas.Create остаётся фасадом TrueMoon с
  прежним публичным API. Прямых production dependencies Editor/Hosting/Nom/Enerit
  на bootstrap не добавлено. Hosting.Tests получил прямую test-only ProjectReference
  на TrueMoon; первая сборка после удаления transitive edge была non-green,
  исправление подтверждено полными171tests D/R.
- Два test agents работали только в6новых test files по code-testing-agent workflow:
  source research/pairing, directed behavior plan, assertion и behavioral gap review.
  Pairing — heuristic, не coverage; повторные presence-only проверки не подменяют
  actual World/scene/scheduler/authoring/render admission, transport quota, Nom
  stores, Editor startup, copied process command и owned cleanup.
- Source audit: выбранных GetEnvironmentVariable(s)/GetCommandLineArgs осталось
 4calls/4files только в общих capture/providers. Остальные OS facts, protected child
  serialization, isolated compiler environment, document/asset JSON и executable
  probe/benchmark commands классифицированы в guide, не превращены в providers.

### Текущие проверки

SDK10.0.403, net10.0, xUnit2/VSTest. APPDATA/CLI home изолированы в ignored
TestResults/ConfigurationPlan/CORE75. Каждый restore/build/test/publish/pack имеет
отдельный binlog; console logs/TRX сохранены рядом.

| Suite | Debug | Release | Scope |
| --- | --- | --- | --- |
| Core |661/661 |661/661 | Полная; общий schema/conversion/facade/reader слой |
| Mithril |155/155 |155/155 | Полная; protected bootstrap и прежний process lifecycle |
| Enerit |354/354 |354/354 | Полная runtime;11new configuration cases; generator suite не повторялась |
| Nom |874/874 |874/874 | Полная;12new cases |
| Hosting |171/171 |171/171 | Полная;17new cases; Debug verified-hosting replay после test reference repair |
| Editor |1496/1496 |1496/1496 | Полная;28new cases и сохранён architecture boundary test |
| Aether migration + old failure replay |47/47 |47/47 |42new configuration cases +5старых случаев, последовательно |
| New cases total |110/110 |110/110 |6новых файлов /49methods, failed/skipped0 |

Основная команда: `dotnet test <project>/<project>.csproj --no-restore -c Debug|Release
-m --logger "trx;LogFileName=<name>.trx" --results-directory <CORE75>
"-bl:<CORE75>/<name>-{}.binlog"`. Projects: TrueMoon.Tests, TrueMoon.Mithril.Tests,
TrueMoon.Enerit.Tests, TrueMoon.Nom.Tests, TrueMoon.Alloy.Hosting.Tests,
TrueMoon.Aether.Editor.Tests, TrueMoon.Aether.Tests. После reference repair Debug
Hosting использовал BuildProjectReferences=false и существующие свежие library DLL.

Serial Aether replay: та же команда с --no-build --no-restore и filter OR по
Configuration.ModuleConfigurationMigrationTests, Configuration.RuntimeSettingsMigrationTests,
Capture_PinsExecutableRuntimeSdkAndEnvironmentWithStableIdentity,
Packages_ConsumesSdkGeneratedLockWithoutChangingOriginalLockOrArchive,
BlockedDiagnosticImporter_DoesNotBlockIndependentControlConnection и
CompiledGameFixtures_PreserveSelectedBuildCheckpointRollbackAndCleanup (2scenario cases).
Артефакты serial-aether-Debug/Release.trx. В new-cases.json извлечены фактические
110Passed каждого profile из TRX; это не перенос test count из прежнего этапа.

### Полные Aether attempts и ограничения

Полная suite не объявляется green:

1. Первый Debug3232/3234: прежние build-artifacts/replacement fixtures упали на
   inherited legacy DotNetCliToolReference dotnet-xunit2.3.1 / NU1100 и SDK path error.
2. Debug final3233/3234: локальная env DirectoryPackagesPropsPath сначала включила
   CPM в чужом temporary fixture; Packages_ConsumesSdkGeneratedLockWithoutChangingOriginalLockOrArchive
   получил NU1008. Для следующих запусков клонированный props ограничил
   ManagePackageVersionsCentrally только workspace projects.
3. Дополнительный full Debug verified3232/3234 и full Release3233/3234 выполнялись
   одновременно и пересеклись на эксклюзивном SDK .version в
   Capture_PinsExecutableRuntimeSdkAndEnvironmentWithStableIdentity.
   В дополнительном Debug также TaskCanceledException в
   BlockedDiagnosticImporter_DoesNotBlockIndependentControlConnection (12s wait).
   Это observed failures; полные concurrent runs не являются clean baseline.
4. После завершения процессов serial47/47 D/R подтвердил все5затронутых old cases,
   включая2real compiled-game process fixtures, и42new configuration cases.
   Replay не исправляет исторический non-green full-run result и не доказывает
   устранение прежней diagnostics reliability причины.

Root Directory.Packages.props не изменялся. Для offline restore использовалась
ignored копия без obsolete DotNetCliToolReference, версии пакетов сохранены,
NuGet.Config с пустыми feeds и RestorePackagesPath=C:/Users/MEGA6/.nuget/packages.
Environment override текущих serial tests — только процесс запуска, не App setting
и не новое глобальное поведение production. Чистый последовательный full baseline
и стандартная изоляция nested fixtures остаются отдельной reliability задачей.

### Source / published / fresh-package delivery

Новый independent consumer [Program/runner](../../ManualTests/ConfigurationModulesConsumer/README.md):
`./ManualTests/ConfigurationModulesConsumer/Verify.ps1 -EvidenceDirectory
<fresh-directory>`. Fresh directories вне checkout, unrelated cwd, local-only feed,
свежий NuGet cache,0ProjectReference в package graph.

Успешная версия: **0.2.5.2-core75.20261110212150**.
16пакетов: Contracts, Core, Cobalt, TrueMoon, Extensions.DependencyInjection,
Enerit.Generator, Enerit, Mithril, Argentis, Alloy, Alloy.Rendering.Skia,
Alloy.Platform.Silk, Alloy.Hosting, Aether, Aether.Editor, Nom.
Source Debug/published Release/fresh Release: каждый **76checks** на Cobalt/MS DI.
Actual ECS quota/tick, empty Editor startup, typed diagnostic budget, actual Nom
store admission/close/reopen/drain, provenance/callback/copies, atomic restart
rejection, lazy UI composition, flag aliases и Editor bootstrap independence.
Runtime factory1creation/1dispose на provider; GpuRun=false, WindowRun=false,
ChildProcessRun=false. Это отдельный configuration proof, не повтор native UI/GPU
или fresh real-child acceptance CORE7.4.

Первый delivery attempt успешно pack/restore, затем намеренно строгая graph
проверка обнаружила отсутствие PrivateAssets Enerit.Generator в transitive graph.
Fresh consumer получил явный build-only PackageReference, как требует existing
Enerit package guide; весь runner повторён с новой версией/пустыми directories.
Оба attempt сохранены, успешный — CORE75/delivery-final/verification.json.
Все16versions/feed provenance, exact cached external nupkg, hashes installed/output
TrueMoon DLL и отсутствие source drift проверены runner. Packages не публиковались
во внешний feed. Независимые consumer build/publish:0errors/0warnings.

### API, сохранность и документы

Reflection public declarations: removed0 в7assemblies. Contracts1655→1668 (+13),
TrueMoon1073→1076 (+3), Mithril433→433, Aether9128→9317 (+189),
Editor3484→3561 (+77), Enerit406→414 (+8), Hosting571→642 (+71).
Сравнение с сохранёнными fresh artifacts CORE7.4 (первые3), RenderSnapshots (Aether)
и ED5b (Editor/Enerit/Hosting); это comparison к указанным artifact baselines,
не заявление о всех ABI/platform сценариях. Nom existing source bodies/API files
сохранены hash-аудитом, добавлены2новых API files.

Baseline1649C#/csproj:1633без изменений,14целевых изменений (12C#+2csproj),
2internal files перенесены TrueMoon→Contracts,26C# добавлены (включая2переноса,
6tests и independent consumer). Head/Directory.Build.props/прежние ED5b,
Cobalt, SplitView, rendering snapshots и src/.codex сохранены.
Планы/статусы Core/engine/Enerit/Nom/Alloy и общий roadmap обновлены под SHA guard;
предыдущие STATUS сохранены целиком в HISTORY. Module PLAN содержит только ссылку
на детальный CORE7.5 task, без второй очереди реализации.

Raw retention: CORE75 сохраняет ignored logs/TRX/binlogs/manifests/hashes, оба
delivery attempts и source-before/audit; standalone run/feed/cache оставлены в
записанных TEMP directories. На автоматическую очистку TEMP/ignored artifacts
вечной ссылки нет; дата/команды/counts/failures/scope сохранены здесь.
Полный solution build/tests и новый hardware/native/GPU acceptance не заявляются.
Далее CORE7.6: финальная сверка документации/поставки и совместный App/child consumer;
engine f.4, MIT6, физические input/device-loss/K7/VRAM и wider release отдельно.


<a id="core74-2026-11-10"></a>
## CORE7.4: Core settings и защищённый Mithril bootstrap — 2026-11-10

По запросу пользователя выполнен следующий этап единого configuration profile.
CoreConfigurationSchemas.App/Paths и UseCoreConfiguration связывают App:Name и
Paths:Root после всех Setup/legacy callbacks и до DI. App.Name получает private
snapshot; IPathResolver получает собственную копию, даже при изменении injected
DTO до первого resolve. Legacy-only App.Name остаётся dynamic. AddLegacySources
импортирует только appName/default/env/args и CLI-root на стандартных levels;
обычные JSON/env/args/overrides продолжают общую priority/validation семантику.

PathResolver использует common ParseKnown/schema/profile до App/DI, новое ctor
PathSettings принимает готовые данные. Common Capture — одна OS args boundary.
Старый CommandLineArgsProvider сохраняет original tokens вместе с прежней args
projection; separate values, aliases и last-wins обрабатывает общий parser.
Legacy Set отслеживается явно, включая null/same-value, не сравнением значений.
Regression ExplicitLegacyArgumentMutationOverridesOrErasesCapturedIdentity дал
RED1 (null уже был в projection) → GREEN после override virtual Set; тест не ослаблен.

DiagnosticsSettings содержит все33data fields pipeline: budgets, filters/rules,
retention, emergency и telemetry. Maps/arrays deep copied, enum maps имеют explicit
codec с duplicate/range validation. UseConfiguredDiagnostics связывает data до
ресурсов, отдельно принимает borrowed factories/listeners/emergency writer.
Активация DiagnosticSubscription и IDiagnosticsConfiguration идут через DI;
старый object-bag key остаётся compatibility mirror прежнего UseDiagnostics.
Все standard Core/diagnostics fields StartupOnly; reload возвращает restart-required
без замены Current/resources. Динамическое применение к ресурсам не назначено.

Mithril AddUnit читает один immutable UnitLaunchSettings через UnitBootstrap;
parent handler получает его явно. Parse использует общие parser/profile/schema
без App/DI/OS capture. Read — ограниченный legacy adapter. Env group -u/-p/control
authoritative целиком, без дополнения CLI/defaults. Unit/parent/endpoint валидируются
до unit actions/resources; UnitId без parent допустим как unmanaged selection.
User profile Mithril:UnitId и arbitrary sections не участвуют в identity.

Новый optional IConfigurationBootstrapInputs возвращает original standard
args/env/default provider sections до ordinary overlays. env.json/args.json/default.json
не подменяют trusted envelope, удалённый provider не перечитывает ОС. Custom
IConfiguration без capability предоставляет trusted bounded sections по договору
совместимости. Existing interfaces не получили mandatory членов; legacy selection
helpers/parent IConfiguration ctor сохранены. AddUnit сохраняет ArgumentException
boundary для malformed/unknown selection; direct Parse/Read — ConfigurationException.

Mithril добавил необходимую ProjectReference на TrueMoon, где живут common concrete
parser/schema/profile. Contracts/Core сохранены, цикла нет. NuGet dependency
TrueMoon.Mithril→TrueMoon проверена в nupkg и fresh6package graph. New external
package versions/TF/assembly identities не назначены, общая0.2.5.2-alpha не менялась.
Process launch/env generation, parent/pipe ownership, stop/restart/kill/reap
алгоритмы не переработаны; их прежние suites повторены ниже.

### Свежая проверка

| Проверка | Результат и область |
| --- | --- |
| New focused tests | Core42/42, Mithril44/44;86newcases, оба DI и негативные paths |
| Full Core |661/661 Debug и Release, failed/skipped0 |
| Full Mithril |155/155 Debug и Release, failed/skipped0; existing real process/composition/restart/parent-control cases включены |
| Ordinary incremental solution Debug |0errors/790warnings; full-solution tests не запускались |
| API specimen |4blocks Release0errors/0warnings against actual assemblies; no runtime proof этим compile |
| Source Debug / published Release / fresh6packages |по108checks/profile, Cobalt/MS DI; real owned child на обоих DI в каждом профиле |
| Public surface | Contracts1652→1655, TrueMoon835→1073, Mithril395→433 reflection declaration lines; removed0. Это surface comparison, не все binary compatibility сценарии |

Fresh version `0.2.5.2-core74.20261110190453`, independent temp consumer, fresh local
feed/cache, unrelated cwd, no ProjectReference в package graph. Два child на профиль
(6всего): protected env против conflicting CLI/user profile, selected unit1 вместо999/99,
реальный parent PID/control pipe, graceful stopped marker, exit/reap и drained monitor.
No GPU/window. Source includes former2MainProcess units and reload/writer checks.
Nupkg Mithril dependencies Contracts/Core/TrueMoon указывают новую proof version.

Baseline1635files (1574C#+61csproj):1621hashes сохранены,13C#+1Mithril.csproj
целенаправленно изменены. Добавлены11production и3test C#; текущее перечисление
1588C#/894production. Остальная dirty работа и .codex сохранены. Новых settings OS
readers в сервисах нет; два прежних GetCommandLineArgs сведены к common Capture.
Current selected-call ledger7calls/6files; прежние ledger/counts остаются историей.

### Requirement → конкретные tests

| Требование | Test |
| --- | --- |
| Common grammar/strict compatibility | BootstrapConfigurationTests.SelectedParserRetainsUnknownArgumentsAndUsesCommonKnownGrammar; StrictParserStillRejectsUnknownOption |
| Core aliases/priority/capture | CoreConfigurationMigrationTests.LegacyAdapterUsesNamedSourcesAndCapturesTheirValuesWithNormalPriorities; OriginalArgumentsPreserveSeparatedRootAndAppNameAliasOrdering |
| Frozen Name/path и app isolation | CoreConfigurationMigrationTests.StandardCoreCompositionFreezesAppNameAndPathsWhileReaderReturnsCopies; IndependentAppsHaveIndependentStandardSettingsAndResolvers |
| Invalid winner до DI/resources / restart | CoreConfigurationMigrationTests.InvalidWinningCoreValueFailsBeforeRegistrationOrFactory; CoreStartupChangesRequireRestartAndKeepTheCurrentGeneration |
| Data-only diagnostics / ownership | CoreConfigurationMigrationTests.ConfiguredDiagnosticsUsesDiWithoutLegacyMirrorAndOwnsSinkProducts |
| Nested/rules/deep copies/real telemetry | CoreConfigurationMigrationTests.DiagnosticRulesAndNestedSettingsBindIntoPipelineAndRemainDeepCopied; InvalidNestedDiagnosticsAndRulesCannotCreateResources |
| Env authoritative group/no partial merge | UnitBootstrapConfigurationTests.ProtectedEnvironmentIdentityReplacesTheWholeCliGroup; PartialEnvironmentIdentityDoesNotBorrowCliParentOrEndpoint; InvalidProtectedWinnerNeverFallsBackToValidCli |
| Bounded original-provider bridge | UnitBootstrapConfigurationTests.ArbitraryProviderCannotSpoofProtectedInputsEvenUsingStandardSectionNames; RemovedStandardEnvironmentIsNotRecapturedFromUnknownProvider; CustomConfigurationWithoutBootstrapCapabilityRetainsTrustedBoundedFallback |
| Actual standard token grammar/explicit null | UnitBootstrapConfigurationTests.OriginalStandardProviderTokensPreserveSelectedAliasOrder; ExplicitLegacyArgumentMutationOverridesOrErasesCapturedIdentity |
| Frozen App selection/disable/parent adapter | UnitBootstrapConfigurationTests.AppUnitSelectionCapturesTypedLaunchBeforeLaterLegacyMutation; DisableControlPreventsMainUnitActionAndControllerCreation; TypedAndLegacyParentHandlersBorrowLifetimeWithoutStartingOnConstruction |

Exact40-method matrix, research/plan/quality review сохранены в nonstageable TEMP
testagent state. Static pairing2929source/687test heuristic включает TestResults;
это не measurement coverage и не основание менять явное Core/Mithril ownership.

### Команды, restore и артефакты

SDK10.0.403; APPDATA/DOTNET_CLI_HOME изолированы в ignored CORE74 evidence,
first-run certificate/telemetry выключены. Для новой ProjectReference выполнен
offline restore graph: временная копия Directory.Packages.props с теми же exact
package versions, без старого unrelated DotNetCliToolReference dotnet-xunit2.3.1;
NuGet.Config без sources, audit disabled для offline run, явный существующий package
cache. Repo Directory.Packages.props не менялся. Первая попытка с redirected default
cache дала NU1100; повтор с прежним cached package path успешен. Это tooling/cache
boundary, не новая framework dependency или online restore proof.

```powershell
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~CoreConfigurationMigrationTests|FullyQualifiedName~BootstrapConfigurationTests' --logger 'trx;LogFileName=core-focused-final.trx' --results-directory TestResults/ConfigurationPlan/CORE74 -m '-bl:TestResults/ConfigurationPlan/CORE74/focused-green-{}.binlog'
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~UnitBootstrapConfigurationTests' --logger 'trx;LogFileName=mithril-focused-final.trx' --results-directory TestResults/ConfigurationPlan/CORE74 -m '-bl:TestResults/ConfigurationPlan/CORE74/focused-green-{}.binlog'
# Полные обе suites повторены с -c Debug и -c Release, без filter; TRX core-final-*/mithril-final-*.
dotnet build TrueMoon.slnx --no-restore -c Debug -m '-bl:TestResults/ConfigurationPlan/CORE74/solution-debug-{}.binlog'
& ManualTests/ConfigurationAppConsumer/Verify.ps1 -EvidenceDirectory TestResults/ConfigurationPlan/CORE74/consumer-delivery
& ManualTests/ConfigurationApiDesign/Verify.ps1
```

Все build/test/restore/pack/publish имеют unique binlogs. Raw logs/TRX/source baseline,
package/dll hashes/manifests, API diff и final audit — ignored
`TestResults/ConfigurationPlan/CORE74/`; four-block shape — `CORE70Api/` рядом.
[Руководство](CONFIGURATION_CORE_MITHRIL.md),
[consumer](../../ManualTests/ConfigurationAppConsumer/Program.cs),
[runner](../../ManualTests/ConfigurationAppConsumer/Verify.ps1).

Далее CORE7.5 у владельцев Aether/Editor/остальных модулей. Их RuntimeBootstrap,
GPU env flags, Play/Build captures, identity/version/size limits не мигрированы этим
шагом. MIT6 containment/trends, existing native fixture failures и hardware/release
acceptance остаются самостоятельными задачами; новых broad OS/durability promises нет.

<a id="status-before-core74-2026-11-10"></a>
## Core STATUS перед CORE7.4 — снимок 2026-11-10

**2026-11-10 — CORE7.3 reload/changes/writer реализован и проверен.**
[Reload guide](CONFIGURATION_RELOAD.md), [App stage](CONFIGURATION_APP.md),
[standalone](CONFIGURATION_USAGE.md), [API](CONFIGURATION_API.md),
[план](PLAN.md#core7-unified-configuration), [evidence/limits](HISTORY.md#core73-2026-11-10).

## Реализация и проверки

| Область | Текущее состояние |
| --- | --- |
| CORE7.0–3 | Sources/schema/parser/App stage; atomic raw+options reload, startup-only whole rejection, safe rollback, bounded/coalesced changes, explicit JSON patch |
| Ownership | Один App-owned runtime;4nondisposable aliases; Stop/failed-start закрывают admission и drain callbacks, Current доступен до Dispose; issued snapshots остаются значениями |
| Core tests |619/619 Debug и Release, failed/skipped0;65newcases, оба DI, concurrency/cancel/failures/retry/capacity/reentrancy/cleanup |
| Delivery | Source Debug/published Release/fresh6package Release по72checks/profile; Cobalt/MS DI и2Mithril MainProcess units, no OS child/GPU/window. Version0.2.5.2-core73.20261110164712 |
| Solution/API | Ordinary incremental Debug0errors/855warnings;4API blocks Release0errors/0warnings; не full-solution tests |
| Legacy/CORE6 | IConfiguration/App.Name/модульные CLI/bootstrap не мигрированы; diagnostics routing/metrics/generation/release отдельно |

Commands/scope/SHA — в HISTORY. Общая0.2.5.2-alpha не менялась;1552из1560baseline
C# hashes сохранены,8целевых исходников изменены,11production/3tests добавлены.
[Прежний STATUS](HISTORY.md#status-before-core73-2026-11-10).

## Ограничения

Нет automatic watcher и автоматического применения dynamic options к ресурсам:
injected T остаётся startup copy. Diff сравнивает typed raw value/ReadState/Origin
до configure. Callbacks чистые, schema.Copy обеспечивает deep copies. Callback
уведомления не ждёт App shutdown; blocking/ignoring-cancellation callback/source
не завершается принудительно. Custom profile для App требует optional catalog;
без optional writer capability запись отклоняется. Writer не публикует/reload
автоматически; общий gate не даёт interprocess/external-writer/power-loss durability.
Protected process identity/OS facts/UI documents не стали user settings.
Modules ещё не переведены (CORE7.4/5), native failures/release gates не закрыты.

## Следующий конкретный шаг

CORE7.4: перевести Core/App name/diagnostics defaults и Mithril bootstrap на общий
профиль/options, сохранить legacy CLI/env aliases и protected process identity,
parent monitoring, start/stop/restart/cleanup и ownership. Детали — в одном Core PLAN.
Engine f.4 независим.

<a id="core73-2026-11-10"></a>
## CORE7.3: атомарный reload, bounded changes и явная запись — 2026-11-10

По запросу пользователя реализован следующий этап общего configuration profile.
Frozen App stage сохраняет schema/configure plan; каждый reload загружает все
sources и заново готовит все typed options/copies/validation. Одной атомарной
заменой публикуются raw+prepared values. Нет повторного module register/UseModule,
DI activation и пересоздания сервисов. Failed/canceled candidate сохраняет прежний
объект Current/generation; no-op тоже не публикует. Любой changed StartupOnly
ключ отклоняет весь candidate с RestartRequired. Успех увеличивает generation на1.

Diff — canonical typed raw values через codec.Write, ReadState и Origin; keys
Ordinal sorted. Configure чистый/frozen и не является diff-reader; зависимость
startup resource от dynamic поля требует module contract. Typed snapshot readers
закрываются reflection один раз при composition, не в Current/Read/GetOptions.
Existing injected options и issued snapshots не меняются. Custom profile обязан
предоставлять immutable snapshots, а custom schema/copy/codec — чистые deep copies.

ConfigurationUpdate/ConfigurationEdit и reloader/changes/writer добавлены в
Contracts без новых обязательных членов existing interfaces. Optional
IConfigurationMutableSource/IConfigurationProfileWriter допускают расширение
записи; built-in JSON mutable, args/env/defaults/overrides read-only. Exact source
ID, целиком validated/copied patch до IO, delete vs explicit JSON null. JSON
staging рядом с target и rename; success после commit не превращается в canceled
write. Нет implicit save/reload/notification при записи. Один runtime gate
сериализует reload/write, не межпроцессные external writers/power-loss durability.

На subscription max64slots и1pending latest update с union ChangedKeys.
Callback вне gate/lock, исключения изолированы с value-safe diagnostic code.
Dispose handle снимает только подписку, retiring callback удерживает slot до выхода.
Все4public aliases nondisposable, owner runtime один. Borrowed lifecycle adapter
стартует перед user services: Stop и failed-start rollback закрывают admission,
отменяют pending operations/delivery и ожидают callbacks. Reader живёт до Dispose;
Stop token ограничивает ожидание, не прерывает blocking callback/source. Callback
не должен ждать App Stop/Dispose, который сам ожидает его завершения.

### Свежие проверки и точная область

| Проверка | Фактический результат |
| --- | --- |
| Focused Core73 |65/65 new cases, failed/skipped0, xUnit2/VSTest; Cobalt и MS DI |
| Core suite |619/619 Debug и619/619 Release, failed/skipped0 |
| Ordinary incremental solution Debug |0errors/855warnings; не запуск всех test projects |
| Mithril Release rebuild |0errors/232warnings incremental; не новая Mithril suite |
| API specimen |4blocks Release0errors/0warnings against actual assemblies; no runtime/child |
| Public API comparison | Reflection declarations из предыдущих CORE7.2 и новых fresh packages: Contracts1571→1652, TrueMoon835→835; removed0. Это surface/signature comparison, не анализ всех binary compatibility scenarios |
| Source Debug / published Release / fresh packages |по72checks/profile, Cobalt+MS DI; прежние2actual Mithril MainProcess units сохранены, OS child/GPU/window не запускались |

Fresh delivery: `0.2.5.2-core73.20261110164712`; six packages Contracts/Core/Cobalt/
TrueMoon/MSDI/Mithril из local feed в fresh isolated cache, без ProjectReference,
unrelated cwd. Общая0.2.5.2-alpha, identities, dependencies и TF не менялись.
В baseline1560C# изменены только8целевых файлов;1552hashes сохранены. Добавлены
11production и3test files. Остальная dirty работа, планы соседних модулей и .codex
сохранены. Это Core-owned proof, не module migration или release acceptance.

### Requirement → точное test evidence

| Требование | Test (TrueMoon.Tests.Configuration) |
| --- | --- |
| Atomic raw/options, old values/services unchanged | ConfigurationReloadTests.ReloadPublishesOneAtomicGenerationAndRetainsOldOptionsServicesAndSnapshots |
| Typed no-op / provenance | ConfigurationReloadTests.EquivalentTypedValuesAreNoOpDespiteDifferentJsonRepresentation; ProvenanceChangePublishesEvenWhenTypedValueAndStateStayEqual |
| Startup-only reject | ConfigurationReloadTests.StartupOnlyChangeRejectsWholeCandidateIncludingReloadableChanges |
| Late-schema atomic rollback/retry | ConfigurationReloadTests.LateUnregisteredSchemaFailureRollsBackWholeCandidateAndRetryPublishesBoth |
| Pure configure rollback/redaction/retry | ConfigurationReloadTests.PureConfigureFailureIsRedactedAndRollbackRemainsRetryable |
| Startup-only provenance | ConfigurationReloadTests.StartupOnlyProvenanceChangeRequiresRestartEvenWithEqualValue |
| Real JSON IO failure/retry | ConfigurationWriteTests.RealJsonIoFailureRetainsSnapshotAndRetryPublishesOnlyAfterExplicitReload |
| Failed/canceled candidate | ConfigurationReloadTests.InvalidRawOrSemanticCandidateNeverReplacesCurrentSnapshot; CancelledSourceAwaitRetainsCurrentAndSubsequentReloadCanSucceed |
| JSON null/delete, no implicit reload | ConfigurationWriteTests.JsonPatchDistinguishesExplicitNullAndDeleteAndNeverReloadsImplicitly |
| Preflight / readonly / unknown | ConfigurationWriteTests.EveryPatchEditIsValidatedBeforeAnySourceIo; UnknownAndReadonlySourcesFailWithoutCallingAnyWriter; CustomProfileWithoutWriterCapabilityIsReadonly |
| Shared gate / copied edits | ConfigurationWriteTests.WriteAndReloadShareOneGateAndReaderStaysReadableDuringIo; QueuedWriteWaitsForReloadAndCopiesCallerEdits |
| Write failure/cancel and commit boundary | ConfigurationWriteTests.CancelledOrFailedWriteNeverPublishesAndErrorsHideValues; CancellationAfterSuccessfulSourceCommitDoesNotReportWriteAsCancelled |
| JSON parent normalization | ConfigurationWriteTests.JsonPatchUsesOneCaseInsensitiveParentForDifferentlyCasedSchemaPaths |
| Capacity / coalescing | ConfigurationChangesTests.SubscriptionCapacityIs64AndDisposalReleasesSlotWithoutOwningRuntime; SlowObserverGetsLatestPendingGenerationWithUnionOfCanonicalChangedKeys |
| Callback isolation/reentrancy | ConfigurationChangesTests.ObserverExceptionsDoNotRollbackPublicationOrBlockOtherObservers; CallbackCanSynchronouslyRequestReloadBecauseDeliveryIsOutsideGate; CallbackCanUnsubscribeItselfWithoutDisposingTheRuntime |
| Stop/failed start | ConfigurationChangesTests.StopWaitsForRunningCallbackAndClosesAllAdmissionWhileCurrentRemainsReadable; FailedStartupClosesReloadWriteAndSubscriptionsBeforeAppDisposal |

All late-schema/configure failures, startup provenance and real JSON IO/retry cases
также входят в65cases; точные имена в TRX. Source-to-test pairing — static heuristic,
не measurement coverage. Broad-test research/plan/quality status находятся только
в nonstageable TEMP; исходные существующие tests не переписаны.

Команды из src, SDK10.0.403, --no-restore использует существующие assets. APPDATA и
DOTNET_CLI_HOME изолированы внутри ignored evidence; first-run certificate/telemetry
отключены. Все build/test/pack/publish/restore имеют unique quoted -bl:...-{}.binlog.

```powershell
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~ConfigurationReloadTests|FullyQualifiedName~ConfigurationWriteTests|FullyQualifiedName~ConfigurationChangesTests' --logger 'trx;LogFileName=reload-final.trx' --results-directory TestResults/ConfigurationPlan/CORE73 -m '-bl:TestResults/ConfigurationPlan/CORE73/reload-final-{}.binlog'
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --no-restore -c Debug --logger 'trx;LogFileName=core-final-Debug.trx' --results-directory TestResults/ConfigurationPlan/CORE73 -m '-bl:TestResults/ConfigurationPlan/CORE73/core-final-{}.binlog'
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=core-final-Release.trx' --results-directory TestResults/ConfigurationPlan/CORE73 -m '-bl:TestResults/ConfigurationPlan/CORE73/core-final-{}.binlog'
dotnet build TrueMoon.slnx --no-restore -c Debug -m '-bl:TestResults/ConfigurationPlan/CORE73/solution-debug-{}.binlog'
& ManualTests/ConfigurationAppConsumer/Verify.ps1 -EvidenceDirectory TestResults/ConfigurationPlan/CORE73/consumer-delivery
& ManualTests/ConfigurationApiDesign/Verify.ps1
```

Logs/TRX/binlogs/source baseline/delivery manifest/hash audit находятся в ignored
`TestResults/ConfigurationPlan/CORE73/`; shape proof — `CORE70Api/` рядом.
[Рабочее руководство](CONFIGURATION_RELOAD.md), [App stage](CONFIGURATION_APP.md),
[полный API](CONFIGURATION_API.md), [runner](../../ManualTests/ConfigurationAppConsumer/Verify.ps1).
Нет automatic watcher, live module resources migration, interprocess writer
transaction, принудительного callback termination или новых native/GPU guarantees.
Следующий конкретный шаг — CORE7.4; CORE7.5 идёт у соответствующих module owners.

<a id="status-before-core73-2026-11-10"></a>
## Core STATUS перед CORE7.3 — снимок 2026-11-10

**2026-11-10 — CORE7.2 App stage/reader реализован и проверен.**
[App guide](CONFIGURATION_APP.md), [standalone](CONFIGURATION_USAGE.md), [API](CONFIGURATION_API.md),
[план](PLAN.md#core7-unified-configuration), [проверки/limits](HISTORY.md#core72-2026-11-10).
All-schema deferred binding/configure/validation/copies до register/DI, typed module
setup и один App-owned runtime с borrowed reader. Legacy путь сохраняется отдельно.

## Реализация и проверки

| Область | Текущее состояние |
| --- | --- |
| CORE7.0–2 | Sources/parser/raw snapshots и App stage/reader готовы; optional catalog API добавлен без mandatory членов старых interfaces |
| Core tests |554/554 D/R, failed/skipped0;48новых App-stage cases, оба DI, ordering/copies/failures/cleanup/contexts/isolations |
| Delivery | Source Debug/published Release/fresh6package Release по36checks; Cobalt/MS DI и2Mithril MainProcess units, no OS child/GPU/window. Version 0.2.5.2-core72.20261110161630 |
| Solution | Обычный incremental Debug0errors/855warnings; Mithril Release rebuild0errors/65warnings. Не full-solution tests и не новая Mithril suite |
| Legacy/CORE6 | IConfiguration/App.Name/модульные CLI/bootstrap не мигрированы; diagnostics routing/metrics/generation/release отдельно |

Commands/scope/SHA/runner — в HISTORY. Общая0.2.5.2-alpha не менялась; source baseline
сохранён кроме3намеренно изменённых App/profile файлов.
[Прежний STATUS](HISTORY.md#status-before-core72-2026-11-10).

## Ограничения

Нет reload/write/notifications (CORE7.3); Current generation пока1. Standalone
GetOptions не получает stage options; App snapshot предоставляет private copies.
Custom profile для App должен реализовать optional catalog. Consumer register
callbacks выполняют composition; собственные внешние side effects не откатываются.
Protected identity/compiler environment/OS facts/UI documents не стали user settings.
Modules ещё не переведены автоматически (CORE7.4/5), старые native failures/release
gates не закрыты; новые durability/OS/RID/hardware guarantees не назначены.

## Следующий конкретный шаг

CORE7.3: атомарный candidate raw+prepared options, explicit reload, startup-only
restart rejection, bounded notifications/ownership и явный write mutable source.
Не повторять register/UseModule и не пересоздавать services при reload; failed
candidate сохраняет старую generation. Далее согласованный consumer cutover.
Engine f.4 независим.

<a id="core72-2026-11-10"></a>
## CORE7.2: App stage, typed options и borrowed reader — 2026-11-10

По запросу пользователя подключён opt-in profile к App: AppConfigurationContext
реализует optional IAppConfigurationStage, extensions UseConfiguration/ConfigureModule
объявляют профиль/модули без загрузки/активации. После всех Setup и legacy callbacks
stage frozen; source.LoadAsync работает в worker без caller SynchronizationContext,
все schemas Bind/configure/validate и private copies готовы до первого register.
Register callbacks и legacy callbacks, которые они добавили, выполняются отдельными
группами; AddUnit Prepare видит полный набор declarations. Bind/register/DI остаются
на caller thread, нет новых scopes/async service activation или hot-loop resolution.

Все registered schemas готовятся, даже без module callback. GetOptions требует exact
schema instance, возвращает копию prepared data без повтора configure/validation;
raw Read/Origin не переписываются callbacks. Один App-owned ConfigurationRuntime
создаётся registration factory и обязательно активируется перед CreateApp, чтобы
provider владел им даже без consumer reader. IConfigurationReader — nondisposable
borrowed alias. Dispose App закрывает Current, выданные snapshots остаются данными.
CreateApp failures очищают returned resolver; failed startup сохраняет обычный App
ownership contract. Legacy IConfiguration/App.Name/DI interfaces не подменены.

Уточнение public API: optional IConfigurationProfileSchemas.VisitSchemas и
IConfigurationSchemaVisitor.Visit<T> передают весь typed catalog без downcast/reflection.
IConfigurationProfile и остальные существующие interfaces не получили mandatory
членов. Custom profile без catalog остаётся standalone, в App явно NotSupported.
Дубликаты/unknown schema/frozen/reader collision отвергаются, malformed generation0/2
не доходит до register/DI. Copy/custom-schema ошибки маскируются как configuration errors.

### Выполненные проверки

- SDK10.0.403, полный TrueMoon.Tests: **554/554 Debug и554/554 Release**, failed/skipped0.
 48new App-stage cases, обе DI: phase ordering, all-schema validation, private copies,
 callbacks/provenance, worker/caller contexts, empty/custom catalogs, identity/denials,
 two Apps/repeated Build и cleanup. Прежние506Core cases повторены в этих же runs.
 Команда для D/R из src: `dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --no-restore --configuration <Debug|Release> --logger "trx;LogFileName=core-final-<Debug|Release>.trx" --results-directory TestResults/ConfigurationPlan/CORE72 -m -bl:TestResults/ConfigurationPlan/CORE72/final-{}.binlog -v:minimal`.
 Process-local APPDATA/DOTNET_CLI_HOME в TestResults/ConfigurationPlan/CORE72;
 first-use/certificate/tool-path/telemetry отключены. Новых test dependencies нет.
- `dotnet build TrueMoon.slnx --no-restore --configuration Debug -m -bl:TestResults/ConfigurationPlan/CORE72/solution-{}.binlog -v:minimal`:
 **0errors/855warnings**, обычная incremental сборка; не full-solution test run.
 Mithril Release dependency rebuild —0errors/65warnings, без собственной новой suite.
- [Actual App consumer](../../ManualTests/ConfigurationAppConsumer/Program.cs) и
 [runner](../../ManualTests/ConfigurationAppConsumer/Verify.ps1):
 `& ManualTests/ConfigurationAppConsumer/Verify.ps1 -EvidenceDirectory TestResults/ConfigurationPlan/CORE72/consumer-delivery`.
 Source Debug, published Release и fresh6package Release — **по36checks**, в каждом
 Cobalt иMicrosoft DI,2actual Mithril MainProcess definitions, start/stop/dispose,
 service options injection, snapshots/copies/legacy separation, repeated Build и
 invalid unbound schema before factory. OS child/GPU/window не запускались.
 Fresh packages: Contracts/Core/Cobalt/TrueMoon/Extensions.DependencyInjection/Mithril,
 version **0.2.5.2-core72.20261110161630**; cache/feed вне checkout, unrelated cwd,
 ProjectReferences0, exact cached external NuGet, local-only feed и SHA package/output.
 Общая версия0.2.5.2-alpha и project references framework не менялись.
- Shape runner CORE7.0 использует реальные App-stage types из текущих assemblies;
 заглушки оставлены только CORE7.3 APIs. Его compilation не является runtime proof.
 Новые production поля/namespace соответствуют структуре; Markdown links/anchors/
 task IDs/whitespace и source witness проверены отдельно.

### Матрица требований и тестов

| Требование CORE7.2 | Exact test evidence |
| --- | --- |
| Setup/legacy/bind/register/drain/DI порядок | AllSetupAndLegacyActionsPrecedeBindingAndAllBindingsPrecedeRegistration; LegacyConfigurationCallbackCanDeclareProfileAndBinding |
| Все schemas проверены до register/factories | EveryCatalogSchemaIsValidatedAndPreparedEvenWithoutModuleRegistration; InvalidUnboundSchemaStopsEveryRegisterAndResolverBeforeFactories; FailedLaterConfigureCallbackPreventsEarlierModuleRegistration |
| Копии, provenance, без повторного callback | ReaderReturnsPreparedCopiesWithoutReplayingCallbacksOrChangingRawProvenance; AllRegistrationCopiesArePreparedBeforeAnyRegisterCallback |
| Catalog/empty/generation compatibility | CustomProfileCatalogPreservesSchemaTypesAndBindsUnregisteredSchemas; EmptyProfileStillCreatesAReaderWithoutInventingOptions; CustomProfileInvalidInitialGenerationFailsBeforeBindRegisterOrResolver |
| Два App, repeated Build и owner cleanup | TwoAppsAndRepeatedBuilderBuildHaveIndependentStagesAndReaderLifetimes; DisposingAppInvalidatesReaderButIssuedSnapshotsRemainValues; FailureAfterResolverCreationDisposesRuntimeAndOwnedServices; StartupFailureRetainsOriginalFailureAndDisposesOwnedGraphOnce |
| Worker-only data load | OnlySourceLoadUsesWorkerWithoutCallerSynchronizationContext |
| Graph/capability/reader denials | ForeignContextAndProfileWithoutSchemaCatalogAreRejectedExplicitly; InvalidStageGraphFailsBeforeResolverAndFactories; FrozenStageRejectsNewProfilesAndBindingsInsideRegister; UserRegistrationCannotReplaceReservedReader |

Тесты: [AppConfigurationStageTests](../../TrueMoon.Tests/Configuration/AppConfigurationStageTests.cs).
Broad test-gap/assertion review выполнен, state в OS TEMP вне Git; mutants/coverage
не запускались. Pairing heuristic не является coverage; IDE findTests недоступен.
Изменены3существующих C# файла (AppBuilder/AppConfigurationContext/ConfigurationProfiles),
прочие1545из исходного1548source baseline сохранены; добавлены4shared contract files,
6implementation files,1test и1consumer. src/.codex и прежние dirty изменения сохранены.

Локальные ignored artifacts — TestResults/ConfigurationPlan/CORE72: TRX/logs/binlogs,
consumer-delivery/verification.json и три consumer profiles. Здесь сохранены команды/
scope/outcomes, а runner находится в Git. Коммит/push не выполнялись. Следующий шаг
CORE7.3 reload/notifications/write; module/process bootstrap migration — CORE7.4/5.

<a id="status-before-core72-2026-11-10"></a>
## Статус перед CORE7.2 — снимок 2026-11-10

**2026-11-10 — standalone CORE7.1 реализован и проверен.**
[Usage](CONFIGURATION_USAGE.md), [API](CONFIGURATION_API.md), [карта](CONFIGURATION.md),
[план](PLAN.md#core7-unified-configuration), [evidence/limits](HISTORY.md#core71-2026-11-10).
Opt-in profiles/schemas/defaults/codecs, normalized paths/приоритеты, CLI/env/JSON,
immutable raw snapshots доступны без App/DI. Legacy consumers не мигрированы.

## Реализация и проверки

| Область | Текущее состояние |
| --- | --- |
| CORE7.0/1 | Проектный контракт и standalone implementation готовы; metadata Codec уточнён добавочно, прежние interfaces не ломались |
| Unit/source | Core506/506 D/R, failed/skipped0;108новых cases, в том числе errors/redaction/copy/cancellation и все source priority pairs |
| Delivery | Source Debug/published Release/fresh3package Release consumer по15checks, unrelated cwd/isolated cache/local feed; version0.2.5.2-core71.20261110141224 |
| Solution | Обычный incremental Debug build0errors/236warnings; full-solution tests не запускались |
| Legacy/CORE6 | Прежние App/bootstrap/diagnostics/lifecycle semantics сохранены; CORE6 routing/metrics/generation/release отдельно |

Результаты датированы этой работой; source/command/scope — в HISTORY. Общая версия
0.2.5.2-alpha не менялась. Неизменность всех1515 прежних C# файлов проверена SHA;
новые production types —29файлов. [Прежний STATUS](HISTORY.md#status-before-core71-2026-11-10).

## Ограничения

Нет App Current reader/отложенного binding stage/reload/write/notifications:
это CORE7.2/3. Standalone GetOptions бросает InvalidOperationException; использовать
schema.Bind(snapshot). Модули пока используют прежние callbacks/CLI/bootstrap;
CORE7.4/5 проведут согласованный cutover. Protected identity/captured compiler env,
OS facts и UI/documents/operations не объединяются с пользовательскими settings.
Новых native/hardware/OS/RID/durability гарантий нет; чужие suite failures не закрыты.

## Следующий конкретный шаг

CORE7.2: App-owned deferred stage, bind всех schemas/overrides/callbacks и validation
до factories/DI, borrowed IConfigurationReader и typed options snapshots. Проверить
Cobalt/MS DI, два App, repeated setup, invalid startup без owned resources,
legacy AddUnit/configuration ordering и отсутствие resolution в горячих циклах.
Engine f.4 остаётся независимым.



<a id="core71-2026-11-10"></a>
## CORE7.1: standalone sources, keys и parser — 2026-11-10

По запросу следующего этапа реализованы [standalone unified profiles](CONFIGURATION_USAGE.md)
в TrueMoon/Configuration и добавочные shared contracts в TrueMoon.Contracts/Configuration.
Schema metadata/defaults, invariant scalar/nullable/enum/Guid/TimeSpan/collection
conversion, explicit codecs, normalized Section:Key/aliases, declared priority и
immutable generation-one snapshots доступны без App/DI. JSON только по явному
absolute path, env по prefix/allowlist, CLI поддерживает inline/separate/flags/
negative/repeats/collections/positionals/--. GetOptions на standalone snapshot
явно отклонён до будущего App stage; schema.Bind доступен как data-only utility.

Старые IConfiguration/ConfigurableBase/ConfigurationBuilder/providers/App/Mithril/
Editor code paths не правились. Все1515 ранее учтённых C# source hashes сохранены;
добавлены29production C# файлов (19Contracts+10TrueMoon),3test files и1consumer.
Доступно862production C# файла вместо833; два новых OS-reader boundary находятся
в ConfigurationEnvironment.Capture и ConfigurationEnvironmentSource, не в services.
Source error diagnostics не удерживают raw exception message/inner exception;
custom codec exceptions и unsolicited cancellation маскируются. Requested source
cancellation сохраняет token. Отклонённая декларация не занимает ID/section builder.

Уточнение API: ConfigurationKey получил optional Codec property и nongeneric
IConfigurationValueCodec для передачи explicit converter через public interfaces.
Прежний constructor и generic codec<T> сохранены; mismatched types отклоняются.
Схема запрещает branch/leaf overlap и неподдержанные типы без codec до IO.
Deep copy является контрактом options/codec; identity Copy и disposable options
отклоняются. App stage/reader/reload/write/notifications не реализованы и не
регистрируются как готовые services; module consumers ещё не мигрированы.

### Фактические проверки

- SDK10.0.403, TrueMoon.Tests: **506/506 Debug и506/506 Release**, failed/skipped0.
  Из них108новых cases (CLI24/source17/profile67),27existing ConfigurationTests;
  остальные прежние Core/lifecycle/process/diagnostics cases повторены полным проектом.
  Команда из src для каждого D/R: `dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --no-restore --configuration <Debug|Release> --logger "trx;LogFileName=core-final-<Debug|Release>.trx" --results-directory TestResults/ConfigurationPlan/CORE71 -m -bl:TestResults/ConfigurationPlan/CORE71/final-{}.binlog -v:minimal`.
  Process-local APPDATA/DOTNET_CLI_HOME направлены в TestResults/ConfigurationPlan/CORE71;
  certificate/first-use/tool-path/telemetry отключены. Новых test/package dependencies нет.
- `dotnet build TrueMoon.slnx --no-restore --configuration Debug -m -bl:TestResults/ConfigurationPlan/CORE71/solution-{}.binlog -v:minimal`:
  **0errors/236warnings**, обычная incremental сборка; это не запуск всего solution.
  Первый ввод несуществующего TrueMoon.sln дал MSB1009; actual target найден как .slnx.
  Предыдущие aggregate warnings и native fixture restore failures не объявляются исправленными.
- [Независимый consumer runner](../../ManualTests/ConfigurationProfileConsumer/Verify.ps1):
  `& ManualTests/ConfigurationProfileConsumer/Verify.ps1 -EvidenceDirectory TestResults/ConfigurationPlan/CORE71/consumer-delivery`.
  Source Debug, published Release и isolated fresh-package Release — **по15checks**.
  Fresh cache/feed вне checkout, unrelated cwd, ProjectReferences0, только local
  sources, exact cached external dependencies, package/feed/installed/output SHA.
  Fresh3packages: TrueMoon.Contracts/TrueMoon.Cobalt/TrueMoon,
  version **0.2.5.2-core71.20261110141224**; общая0.2.5.2-alpha не менялась.
  Consumer проверяет effective priority/provenance, env/JSON/CLI, custom codec,
  missing/null, copied arrays, explicit overrides, failed reload isolation,
  grammar/secret redaction и coexistence с legacy builder. App lifecycle/GPU/native не запускались.
- Declarations/examples runner CORE7.0 обновлён: implemented types берёт из real
  local assemblies, только будущие APIs остаются signature stubs. Его type-check
  не считается runtime proof. Markdown links/anchors/whitespace проверяются отдельно.

### Матрица требований и проверок

| Требование CORE7.1 | Точное test evidence |
| --- | --- |
| Приоритеты всех пар источников, same-level order | EverySourcePairUsesDeclaredPriorityInsteadOfRegistrationOrder; LastSourceAtSamePriorityWins |
| Ключи/sections/aliases, collisions/branch-leaf | MetadataRejectsCaseInsensitiveAliasAndPathCollisions; ProfileRejectsBranchLeafOverlapAcrossSchemasInEitherOrder |
| Missing/null/invalid без fallback и утечек | DefaultsMissingNullEmptyAndWrongReadTypeRemainDistinct; InvalidWinningValueNeverFallsBackOrLeaksSensitiveContent; SourceCannotForgeDiagnosticIdentityOrLeakItsExceptionPayload; CustomCodecFailuresCannotLeakRawValuesThroughConfigurationExceptions |
| Invariant conversion, custom codec, snapshots | ConversionIsInvariantAndSupportsEnumNullableGuidAndTimeSpan; ExplicitCustomCodecConvertsDefaultsInputsAndOverrides; SnapshotDetachesSourceJsonAndEveryReturnedCollection |
| CLI grammar/repeats/flags/negative/-- | AliasesInlineValuesNegativeNumbersFlagsAndDelimiterCompose; RepeatedCollectionsPreserveOccurrenceOrderWithoutCommaSplitting; FlagsDoNotConsumeNextPositionalAndInlineFalseIsExplicit |
| JSON optional/required/malformed/nested/arrays/IO | OptionalAbsenceUsesDefaultsButRequiredAbsenceFails; ExistingInvalidJsonNeverUsesOptionalFallbackOrLeaksValue; JsonFlattensNestedSectionsAndKeepsArraysAsOneTypedValue; ExistingDirectoryIsAnIoFailureEvenForOptionalFile |
| Env prefix/allowlist/collision/bootstrap capture | EnvironmentMapsPrefixAndDoubleUnderscoreButPreservesSingleUnderscore; EnvironmentAllowlistFiltersBeforeUnknownKeyValidationAndIsCopied; EnvironmentRejectsUnknownSelectedKeyAndAmbiguousNormalizedNames; CaptureReadsOnlyRequestedNamesAndRetainsMissingAsNull |
| Freeze/copy/cancellation/failed load | BuildersCannotBeUsedAfterTheirConfigurationCallbackCompletes; IndependentLoadsAndFailureCannotMutateEarlierSnapshots; CancellationAfterSourceAwaitDoesNotReturnPartialOrChangeOldSnapshot |

Тесты находятся в [TrueMoon.Tests/Configuration](../../TrueMoon.Tests/Configuration/UnifiedConfigurationProfileTests.cs)
(3файла). Broad test-gap/assertion review выполнен; mutant execution/coverage проценты
не заявлены. IDE findTests недоступен; static pairing analyzer выполнен однократно,
его heuristic Editor.Tests suggestion переопределён явным Core ownership; pairing
не является coverage. Исследование/план/quality status хранятся в OS TEMP вне Git.

Подробные local artifacts: ignored TestResults/ConfigurationPlan/CORE71:
core-final-Debug/Release.trx, final-Debug/Release.log, unique binlogs,
consumer-delivery/verification.json и три consumer profile reports. История фиксирует
scope/outcome и runner даже при удалении ignored artifacts. Старые dirty изменения
и src/.codex сохранены; коммит/push не выполнялись. Следующий шаг — CORE7.2.

<a id="status-before-core71-2026-11-10"></a>
## Статус перед CORE7.1 — снимок 2026-11-10

**2026-11-10 — CORE7.0 завершён как проектный этап.**
[Карта использования](CONFIGURATION.md), [точный добавочный API и три примера](CONFIGURATION_API.md),
[план](PLAN.md#core7-unified-configuration), [проверка и ограничения](HISTORY.md#core70-api-2026-11-10).
Это подготовка реализации: новый профиль/providers/binding/App reader/reload/write
ещё не поставлены в production API. Следующий шаг — CORE7.1.

## Реализация и проверки

| Область | Текущее состояние |
| --- | --- |
| CORE7.0 | Карта источников/consumers/legacy keys/options, точные signatures/semantics и примеры headless Cobalt/MS DI, Mithril, Editor/Aether child зафиксированы |
| Type-check API | SDK10.0.403, Release, 4 C# блока, 15 существующих local assembly: 0 errors/0 warnings. Bodies новых методов — неисполнявшиеся заглушки; production проекты не пересобирались |
| Действующая конфигурация | Args/env/default, opt-in JSON, прежние Get/GetSection/Set/priority/invariant conversion/Refresh; не изменены |
| App/CORE0–5 и CORE6 | Прежние lifecycle/ownership/configuration и частичные diagnostics результаты сохраняют даты/scope; App-private routing/aggregate metrics/generation/release отдельно |

[Повторяемый runner](../../ManualTests/ConfigurationApiDesign/Verify.ps1) фиксирует
SHA документа/assembly. Проверены ссылки/anchors, уникальность этапов и whitespace;
C# исходники фреймворка не менялись. Module/unit/native/package проверки не запускались.
Прежние Core398/398 D/R и narrow56/56 от 2026-10-30 — [исторические результаты](HISTORY.md#архив-jsonl-и-конфигурация-для-ed8a--2026-10-30).
[Прежний STATUS](HISTORY.md#status-before-core70-api-2026-11-10).

## Ограничения

CORE7.1–6 не завершены. Новый reader не заменяет legacy IConfiguration; mandatory
члены старых interfaces не меняются. Protected bootstrap, compiler environment,
OS facts и UI/documents/operation data отделены от user settings. Старые CLI/JSON/
GetOrCreate consumers требуют повторной проверки при migration. Type-check не
подтверждает runtime DI, lifecycle, snapshots/reload, child identity или fresh packages.
Не назначены новые динамические module fields и durability/OS/RID/native гарантии.

## Следующий конкретный шаг

CORE7.1: реализовать sources/schema metadata/normalized keys/effective values и
standalone parser CLI/env/JSON по CONFIGURATION_API, сохраняя legacy contracts.
Проверить приоритеты пар источников, aliases/collisions, null/invalid, `--`/repeats/
negative/unknown/missing и независимость от App/DI. Затем CORE7.2 binding/App stage,
CORE7.3 reload/write; CORE7.4/5 переводят consumers. Engine f.4 независим.



<a id="core70-api-2026-11-10"></a>
## CORE7.0: API и примеры, завершение проектного этапа — 2026-11-10

Продолжение по запросу пользователя: создан [точный API-контракт](CONFIGURATION_API.md)
для отдельного unified-v1 reader, profiles/sources/schema metadata/codecs, raw/typed
snapshots, source provenance, optional IAppConfigurationStage, reload/changes/write.
Исходные IConfiguration/IConfigurable/builders/context/resolver interfaces не получают
новых обязательных членов, legacy семантика сохраняется. Module runtime identity,
factory delegates и captured compiler environment не становятся user settings.

App порядок зафиксирован отдельно: все binding/callback/validation до register
и DI; legacy callbacks, добавленные register, выполняются после всей группы,
чтобы AddUnit Prepare увидел полный список declarations. Reader nondisposable,
один App-owned runtime; reload атомарно готовит raw+options, startup-only candidate
требует restart, callbacks/services повторно не подключаются. Explicit persistence
не маскируется чтением и не публикует generation автоматически. Динамические поля
по умолчанию не назначены; пределы legacy persistence/stop/DI disposal сохранены.

Добавлен [повторяемый type-check runner](../../ManualTests/ConfigurationApiDesign/Verify.ps1).
Команда из src: `& ManualTests/ConfigurationApiDesign/Verify.ps1`.
Runner извлекает из документа четыре C# блока: declarations, headless App Cobalt/MS DI,
Mithril parent/child composition, Editor и pre-App Aether Runtime. Будущие executable
API bodies бросают NotImplementedException и **не выполняются**. Примеры компилируются
как библиотека против 15 существующих локальных Debug/net10.0 assembly; SHA inputs
сохранены. Это проверка формы API/типов, не запуск services/DI/child и не build модулей.

SDK10.0.403, Release, TreatWarningsAsErrors, package sources пусты, local imports
Directory.Build.props/targets/Directory.Packages.props изолированы: **0 errors/0 warnings**.
Production C# hashes не изменились; full-module/native/package tests не запускались.
Предыдущие probes/ED5b/observer/snapshots/Cobalt и src/.codex сохранены, версия не менялась.
Первый вспомогательный build остановился на чтении user NuGet.Config; после изоляции
APPDATA проявился унаследованный dotnet-xunit2.3.1 tool reference из общего
Directory.Packages.props. Изоляция только каталога runner устранила эти restore
препятствия. Общая сборка/прежние native fixture failures этим не исправлялись.

Артефакты: ignored TestResults/ConfigurationPlan/CORE70Api/inputs.json,
verification.json, bin/Release/net10.0/ConfigurationApi.dll и отдельный build-*.binlog
на каждую попытку. Итоговый runner фиксирует SHA документа/assembly, runtime=false.
Markdown links/anchors/task IDs и whitespace проверены отдельно.

CORE7.0 завершён **как проектный этап**; production feature не объявлен реализованным.
Следующий шаг CORE7.1 — sources/schema metadata/normalized keys/effective values
и standalone CLI/env/JSON. Binding/App reader — CORE7.2, reload/write — CORE7.3,
consumer migration — CORE7.4/5; API diff/номер выпуска перед поставкой реализации.
Engine f.4 и прежние датированные проверки независимы от этой записи.

<a id="status-before-core70-api-2026-11-10"></a>
## Статус перед API CORE7.0 — снимок 2026-11-10

**2026-11-10 — CORE7.0 начат, карта текущей конфигурации готова.**
[Карта и контракт поведения](CONFIGURATION.md), [единственный план](PLAN.md#core7-unified-configuration),
[запись сверки](HISTORY.md#core70-inventory-2026-11-10).
Карта охватывает sources/readers/overrides, typed setup, CLI/env/JSON и bootstrap
по Core/Mithril/Aether/Editor/Enerit/UI/Nom, а также примеры и ручные consumers.
Стандарт — TrueMoon.Configuration независимо от DI. Код и API не менялись.

## Реализация и проверки

| Область | Текущее состояние |
| --- | --- |
| CORE7.0 | Частично: карта, legacy→target mapping, группы options и контракт поведения готовы. Точные добавочные API и примеры подключения ещё не записаны |
| Действующая конфигурация | Args/env/default, явный JSON, case-sensitive keys, global Get отличается от default GetSection, поздний AddProvider выше args. Существуют invariant conversion и атомарная публикация массива секций при Refresh; общего validated options binding/reload пока нет |
| Модульные options | Aether/Editor/Enerit/Alloy.Hosting/Nom уже копируют и проверяют typed options; автоматического binding источников нет. Mithril использует IConfiguration для unit identity; Editor parsers и pre-App RuntimeBootstrap отдельны |
| App/CORE0–5 и CORE6 | Прежние lifecycle/ownership/configuration и частичные diagnostics результаты сохранены со своими датами; App-private routing/aggregate metrics/generation/release остаются отдельно |

Статическая сверка: 833 production C# файла, 6 прямых args/env вызовов в 5 файлах;
счётчик/исключения и source links — в карте. Проверены ссылки/anchors, whitespace
и неизменность C# исходников; сборки/тесты/native/package проверки не запускались.
Исторический Core 398/398 D/R и выборочные 56/56 от 2026-10-30 сохраняют прежнюю
[область проверки](HISTORY.md#архив-jsonl-и-конфигурация-для-ed8a--2026-10-30).
[Прежний STATUS](HISTORY.md#status-before-core70-inventory-2026-11-10).

## Ограничения

CORE7.0 ещё не завершён; новый профиль, binding и API снимков/reload/write
не реализованы. Legacy GetOrCreate/JSON и mutable object bag сохраняют поведение.
Служебные parent/session/nonce/build identity и compiler environment не становятся
обычными user settings; OS facts и состояние UI/документов выделены отдельно.
Миграция CLI/env/JSON требует явной совместимости и повторной проверки consumers.
Cooperative shutdown, persistence и provider-specific disposal сохраняют прежние
границы; новые durability/OS/RID/native гарантии не заявлены.

## Следующий конкретный шаг

Завершить [CORE7.0 API-схему](CONFIGURATION.md#remaining-core70): выбор профиля,
source priority/schema/result/provenance, отложенная bind/validation фаза App,
explicit reload/write и три примера App/Mithril/Editor, без новых обязательных
членов legacy interfaces. Затем CORE7.1 parser/providers, CORE7.2 typed options;
consumer migration — по CORE7.4/5. Engine f.4 остаётся независимым.


<a id="core70-inventory-2026-11-10"></a>
## CORE7.0: карта использования конфигурации — 2026-11-10

По запросу пользователя начат CORE7.0; создана [карта и контракт поведения](CONFIGURATION.md).
Поиск по 1515 C# файлам без bin/obj/TestResults/Designer, из них 833 production
TrueMoon* без Tests: IConfiguration/IConfigurable/providers, прямые args/env,
process launch environment, App setup/options/CLI. Прочитаны найденные реализации;
примеры, ManualTests и Benchmarks сгруппированы отдельно. Production ledger:
6 мест вызова выбранных args/env API в 5 файлах (2 command args, 1 env enumeration,
3 env reads, 0 process env writes), плюс отдельно 4 записи child bootstrap keys.
Это не счётчик всех IO/Environment вызовов и не число дефектов.

Подтверждены несовпадения global Get/default GetSection, case-sensitive raw keys,
приоритет позднего AddProvider над args и default Set ниже источников, отдельные
Editor parsers, pre-App bootstrap и GPU validation env. Сохранены уже существующие
invariant conversion, atomic publication массива секций при Refresh и typed
options snapshots модулей: их наличие не равно новому bind/reload контракту.
Выделены protected identity, compiler captured environment, OS facts и данные
операций/сцен, которые не мигрируют механически в user settings.

В карте есть правила legacy→target keys и групп options, контракт источников,
совместимости/отложенной проверки/снимков/записи, контрольные App/Mithril/Editor
сценарии. CORE7.0 частичен: для завершения нужны точные добавочные API и примеры
подключения; CORE7.1 и consumer migration не начинались. Входящих Core заявок в
docs/requests/core на момент сверки нет; отдельный Claim чужой заявки не создавался.

Основа — HEAD 2e3b2b1a00d67ef10d44e8c8deab1d811584d42d и текущие незакоммиченные
файлы. Предыдущие изменения и src/.codex сохранены. C# код, зависимости и версия
не менялись; сборки/тесты/native/package проверки не запускались. Документационные
проверки: относительные ссылки/anchors, уникальность CORE7.0–6, whitespace diff,
повторное сравнение source SHA. Дополнительные локальные артефакты — ignored
TestResults/ConfigurationPlan/CORE70 (source hashes и ledger), не единственная
запись результата. Текущий engine f.4 и прежние датированные проверки не менялись.

<a id="status-before-core70-inventory-2026-11-10"></a>
## Статус перед картой CORE7.0 — снимок 2026-11-10

**2026-11-09 — запланирован CORE7, единая конфигурация App и всех модулей.**
[План](PLAN.md#core7-unified-configuration), [решение и сверка исходников](HISTORY.md#configuration-unification-plan-2026-11-09).
AGENTS закрепляет TrueMoon.Configuration/общие контракты, typed options через DI,
общие providers/parsers и явный bootstrap adapter. Новые правила приоритетов/ключей,
CLI/binding/снимки и поэтапный переход ещё не реализованы.

## Реализация и проверки

| Область | Текущее состояние |
| --- | --- |
| Действующая конфигурация | App автоматически подключает args/env/default; JSON подключается явно; Get<T> ищет по приоритету, GetSection без имени выбирает default. Mithril использует IConfiguration, Editor/pre-App runtime частично разбирают настройки отдельно. Сверено по исходникам 9 ноября |
| CORE7 | План и инструкция готовы; контракт/карта использования/parser/typed options/reload и переход модулей запланированы |
| App/CORE0–5 | Lifecycle/bootstrap/configuration/owned-borrowed disposal сохраняют прежние bounded results |
| CORE6 | Structured diagnostics, history/import/archive/configuration readers и часть operations есть; App-private routing/aggregate metrics/generation/release остаются отдельно |

Эта работа меняет Markdown: сборки, тесты и native/package проверки не запускались.
Последняя собственная датированная Core проверка — 398/398 D/R от 2026-10-30,
выборочные 56/56; [запись](HISTORY.md#архив-jsonl-и-конфигурация-для-ed8a--2026-10-30).
Она не подтверждает новые CORE7 API. [Прежний STATUS](HISTORY.md#status-before-core7-plan-2026-11-09).

## Ограничения

Текущие contracts не обещают автоматическую typed binding/hot reload/migration,
power-loss persistence, concurrent atomic create или защиту от любого external writer.
Cooperative stop не прерывает ignoring-token Task. Provider-specific DI disposal
сохраняется. CORE7 не отменяет public/CLI compatibility, child identity validation
или ownership; production options не получают неявного live изменения.
App-private diagnostics routing/aggregate metrics и wider OS/RID/release отдельно.

## Следующий конкретный шаг

CORE7.0: общий контракт/профиль по умолчанию и карта источников/потребителей
с переходом со старых keys/CLI/env/JSON. Затем CORE7.1 parser/providers и CORE7.2
typed options на headless App/Cobalt/MS DI; переход модулей по готовому контракту.
Engine f.4 остаётся независимым; старые drain failures требуют причинного evidence,
а не изменения ProcessOutputCapture/ThreadPool/timeouts без подтверждения.


<a id="configuration-unification-plan-2026-11-09"></a>
## CORE7: план единой конфигурации — 2026-11-09

Запрос: улучшить конфигурацию App, унифицировать источники и их использование во всём
фреймворке, добавить правило в инструкции. Прочитаны исходники и обновлена документация;
новые runtime/API/parser/binding не реализовывались, зависимости и версии пакетов
не менялись. Прежние незакоммиченные ED5b/COB5a/SplitView/observer/snapshots сохранены.

Прочитаны AppBuilder, ConfigurationBuilder/CommonConfiguration, configuration
extensions/providers, Mithril unit launch/parent monitor, RuntimeBootstrap и Editor
entry points. Подтверждено: источники args/env/default, CLI key=value, снимок env
при явном refresh, JSON по явному подключению, различие Get<T> и GetSection без имени,
приоритет последнего добавленного provider, typed callbacks модулей и отдельный
разбор Editor/pre-App runtime. Сборки и тесты для этой сверки не запускались.

Детальная задача — [CORE7](PLAN.md#core7-unified-configuration), этапы CORE7.0–6:
контракт/карта использования, общие источники/parser, typed options/проверка App,
атомарный снимок/reload/запись, переход Core/Mithril, остальных модулей и поставка.
Выбран приоритет defaults < files < env < args < явные переопределения; логические
ключи с разделами, env с префиксом, общий CLI parser, защищённый pre-App bootstrap
и typed options через DI. Новые сигнатуры/профиль совместимости определяются в CORE7.0.

AGENTS устанавливает общий механизм конфигурации TrueMoon, независимость от DI
provider, общий разбор настроек и границы системных сведений/аргументов операций/
документов. Существующие потребители переходят по CORE7.4/5 с совместимостью CLI.
Общий план/статус ссылаются на Core; f.4/другие выбранные задачи не отменялись.

Проверены whitespace в diff, относительные ссылки и уникальность новых anchors;
сборки/тесты не запускались для изменения Markdown. Прежние Core 398/398 от 30 октября
и другие исторические результаты сохраняют свои даты и область проверки.

<a id="status-before-core7-plan-2026-11-09"></a>
## Статус перед планом CORE7 — снимок 2026-11-09

**Сверка 2026-11-05:** документация и межмодульная очередь; код и проверки не менялись.
[План](PLAN.md#current-core-queue), [прежняя сводка целиком](HISTORY.md#status-before-planning-revision-2026-11-05).

## Реализация и проверки

App/bootstrap/configuration, cooperative lifecycle/rollback и owned/borrowed
disposal реализованы в своих bounded scopes. CORE6 частичен: structured events,
early filters/bounded pipeline, console/rotating JSONL/memory sinks, operations,
health/read windows и borrowed history/import/archive/configuration readers есть.
App остаётся owner; reader aliases не создают новых owners или sink flush.

Последняя собственная указанная проверка — **2026-10-30: Core398/398 Debug/Release**,
failed/skipped0, narrow56/56. [Archive/configuration evidence](HISTORY.md#архив-jsonl-и-конфигурация-для-ed8a--2026-10-30).
Предыдущие file-worker/borrowed-health/headless/package proofs сохраняют даты
и условия в HISTORY. Editor console/report/child health уже имеют scoped consumers;
ED8a.2 больше не назначается будущим prerequisite Core.

## Ограничения

Cooperative shutdown не прерывает ignoring-token Task. Configuration persistence
не обещает power-loss durability, concurrent atomic create или arbitrary
external-writer protection. DI adapters сохраняют provider-specific disposal.
App-private diagnostics routing, bootstrap-before-pipeline, общий artifact/report
quota и full aggregate metrics не выводятся из текущих JSONL/readers contracts.
CORE6e generation и performance — отдельные задачи после конкретного consumer.
Other OS/RID/AOT/долгие trends и общая release matrix не подтверждены.

## Следующий конкретный шаг

Текущий Editor не требует нового Core API для group reset/camera. Для reliability
предоставлять evidence по [повторному post-reap drain](../engine/PLAN.md#arch2e4b-drain-diagnostics);
не менять ProcessOutputCapture/ThreadPool/timeouts без подтверждённой причины.
Для собственного развития сначала выбрать consumer и контракт App-private/bootstrap
routing либо aggregate metrics CORE6d; затем implementation и собственные проверки.
Типизированный generator не является prerequisite прикладного Editor.
Общий порядок — [roadmap](../PLAN.md#current-roadmap).



## Архив JSONL и конфигурация для ED8a — 2026-10-30

Для Editor report добавлены nondisposable `IDiagnosticArchiveReader` и
`IDiagnosticConfigurationReader`, вместе с тремя прежними aliases они заимствуют
один App-owned pipeline. Повторный legacy setup снимает все пять registrations;
factory/DI aliases не получают повторное Dispose. Новый общий контракт не зависит
от Aether/UI. Scalar configuration копируется под gate: live minimum level и filter
counts, frozen queue/ring/field/retention/emergency/telemetry options. Paths, rule
names, sink factories и environment не входят в snapshot.

Archive reader работает в background task, не flush-ит sinks и не запускает
maintenance. Recognized flat slots/managed marker runs, current-run exception для
собственного marker lease; чужие active/недоступные entries могут быть пропущены.
Reparse points отвергаются. Discovery256entries, default16files/4MiB actual read,
512records/2MiB estimated retained/64KiB line/2s cooperative. File tails читаются
newest-modified first, records refreeze/clip по Core field limits, retained-window
dedup по collector identity, chronological output. Incomplete boundary/final lines,
malformed/oversized lines, disappearing files, IO failures и budget omissions имеют
раздельные counters. Это independent snapshot без полноты async хвоста/durability;
произвольный filesystem stall не прерывается жёстким таймером.

Проверки Windows x64/SDK10.0.403/net10.0/xUnit2, VSTest:

- Core narrow56/56; archive/health/retention. Новых11cases; full **398/398 Debug**
  и **398/398 Release**, failed/skipped0. `RecordBudget_KeepsLatestAndReportsExactOmissions`
  проверяет разные retained windows; `FlatHistory_RejectsMalformedOversizedAndIncompleteLinesWithoutReadingOtherFiles`
  — невалидные строки и чужие файлы; `TailRead_RespectsActualByteBudgetAndRetainsLastCompleteRecord`
  — реальные bytes и last complete record.
- `CurrentManagedRun_IsReadableWithoutFlushOrWaitingForBlockedOtherSink` удерживает
  custom sink и читает уже записанный current JSONL без его drain. Managed closed
  runs/unknown dirs, old-run rotation slots после уменьшения JsonMaxFiles,
  file budget/dedup, disabled/cancelled reads и отсутствие IO при
  disabled capability проверены отдельно.
- `ConfigurationViews_AreBorrowedFrozenPathFreeAndRemovedByLegacyReplacement`
  проверяет Cobalt и Microsoft DI, live minimum/filter counts, freeze исходных options,
  отсутствие путей/rule names и повторный setup.
- Exact command: `dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --no-restore`
  и тот же проект с `-c Release`; фактически с TRX, `-v minimal`,
  `-p:UseSharedCompilation=false -nodeReuse:false`.
  [Initial runner](../../TestResults/DiagnosticReport-ED8a-20261030-1/RunFull.ps1),
  [final Core runner](../../TestResults/DiagnosticReport-ED8a-20261030-1/RunCoreFinal.ps1),
  [audit/TRX](../../TestResults/DiagnosticReport-ED8a-20261030-1/verification.json).

Aether1570/Editor842 Debug/Release и published Release/Cobalt consumer проверены
отдельно: parent rotation/corrupt tail, real CPU preview normal/crashed children,
bounded report и5000imports. [Scope](../engine/HISTORY.md#ed8a1-архив-и-управляемый-экспорт--2026-10-30).
После проверки old-run rotation slots full Core Debug/Release и published consumer
повторены на окончательном reader. Full solution, fresh NuGet packages, UI/Hosting/Enerit/Mithril suites и hardware/
release matrix не запускались. Общий CORE6 остаётся частичным.

## Borrowed health и output windows — 2026-10-29

Для ED6a.2 добавлен общий `IDiagnosticHealthReader`/`DiagnosticHealthSnapshot` в
Contracts. `DiagnosticRecordAccess` предоставляет этот alias вместе с history/
importer, не реализует Dispose и не становится отдельным owner pipeline.
UseDiagnostics legacy/repeated setup удаляет все три access registrations.
ReadHealth копирует collector counters под его gate, затем independent worker
snapshots; не flush-ит sinks, не запускает retention и не читает файлы. Optional
emergency/retention может быть disabled/not-yet-available. UI reader gaps, ring
eviction/capture failures и per-sink drops не суммируются в один loss. Accepted/
Written не объявляются durable delivery. Bounded LastError пригоден для local
display; Editor metadata exporter исключает его текст.

ProcessOutputCapture.Read(maxChars) копирует только выбранный suffix ring с UTF16
boundary. Прежний Read() сохранён. Original capture/file counters неизменны;
ProcessOutputSnapshot.OmittedCharacters отдельно описывает display window,
Limit(...) работает над independent snapshot. Window0 даёт counters без текста.
Reader/file tasks, queue8, file budget, borrowed TextReader и2s/5s owner waits
не менялись. Это не ещё одно исправление исторического file-worker timeout.

Core **387/387** Debug/Release, failed/skipped0;13новых cases. Tests
`Health_ReadsBlockedSinkWithoutDrainAndSeparatesMemoryEvictionFromSinkLoss`,
`AppHealth_IsBorrowedAndRepeatedLegacySetupRemovesEveryAccessView` (Cobalt/MS DI),
`FailingSink_HealthReportsFailureAndBoundedErrorInsteadOfSuccessfulWrite`,
`ReadWindow_PreservesCaptureLossAndSeparatesDisplayOmission`,
`WindowBoundary_DoesNotExposeHalfSurrogateOrDoubleCountCaptureLoss` и invalid-window
case проверяют новые contracts напрямую. Commands: `dotnet test
TrueMoon.Tests/TrueMoon.Tests.csproj --no-restore -c Debug` и `-c Release`, с unique
binlogs/TRX. [Audit/8TRX](../../TestResults/ED6a2-20261029-1/audit.json).
Engine/UI/native results имеют [свой scope](../engine/HISTORY.md#ed6a2-raw-streams-и-health--2026-10-29);
Core не получает UI dependency. Full solution/packages/published/hardware не запускались.

## File-drain без shared ThreadPool — 2026-10-29

По продолжению ARCH2e.4 добавлен [Core-only ProcessOutputProbe](../../ManualTests/ProcessOutputProbe/README.md).
Предварительные controls:1000pairs/2000files в одном процессе,50cold processes
по20pairs и1000pairs с App/JSON diagnostics прошли. Отдельный diagnostic consumer
на прежних nupkg с sync failure marker прошёл30replays/90children. Fresh run со
сбором heap только по фактическому marker также прошёл8cases; failure heap не
получен, дамп успешного процесса вместо fault не выдаётся за root-cause proof.
Исходный external consumer source восстановлен/проверен по baseline SHA перед
созданием отдельной diagnostic copy; старый immutable feed не менялся.

Контролируемый isolated process доказал конкретную liveness dependency: после
допуска/записи24bytes reader доходит до EOF, затем единственный shared worker
удерживается completion callback. Старый async file worker остаётся
WaitingForActivation/WaitingForChunk и не завершает drain за прежние2s при
ReadCompletion=RanToCompletion,24written/24characters/0dropped.
[Exact RED](../../TestResults/ARCH2e4-FileDrain-20261029-1/starvation-exact-red/report.json).
Один LongRunning worker с прежним `WaitToReadAsync().AsTask()` тоже дал RED:
async-to-Task bridge всё ещё требовал pool callback.
[Промежуточный RED](../../TestResults/ARCH2e4-FileDrain-20261029-1/starvation-green/report.json).

**Решение2026-10-29:** file-enabled capture получает один dedicated background
worker с synchronous file IO. Channel сохраняет8bounded chunks; TryWrite/drop и
UTF8/file budgets прежние. Sync semaphore будит worker отдельно на каждый
admitted chunk и на EOF. No-file mode worker не создаёт. Semaphore освобождается
после обоих tasks; borrowed reader остаётся у caller. FileStage/ReadCompletion/
FileCompletion и file errors/loss counters сохраняются. Runtime/compile budgets
2s/5s не увеличены. Цена решения — один background worker на active file capture;
caller по-прежнему управляет количеством captures. App/DI/async activation API
не менялись; arbitrary uncooperative filesystem liveness не обещается.

[GREEN](../../TestResults/ARCH2e4-FileDrain-20261029-1/starvation-green2/report.json)
получен при том же занятом pool worker. Автоматическая регрессия
`ProcessOutputFileWorkerTests.FileDrain_PoolWorkerHeldAfterEof_CompletesAndPreservesBorrowedReader`
проверяет оба completion tasks, реальное содержимое файла,24bytes/0loss/null IO
error и borrowed reader. Pool settings меняет только child process, test host не
меняется. Fixture подключён к существующему TrueMoon.Tests через ProjectReference
без runtime reference и Copy/FileWrites target, отдельного test project нет.
Core capture subset20/20, full Core374/374 и Aether1550/1550 Debug/Release прошли.
Post-fix controls:1000pairs/2000files непосредственно и ещё столько же внутри
App/JSON diagnostics прошли. Это lifecycle/content proof, не perf baseline.

Точные успешные test команды (в Debug и Release, отдельные TRX/binlogs):

```powershell
dotnet test ./TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --logger 'trx;LogFileName=TrueMoon.Tests-Debug.trx' --results-directory ./TestResults/ARCH2e4-FileDrain-20261029-1 --verbosity quiet '-bl:./TestResults/ARCH2e4-FileDrain-20261029-1/TrueMoon.Tests-Debug-{}.binlog'
dotnet test ./TrueMoon.Tests/TrueMoon.Tests.csproj -c Release --logger 'trx;LogFileName=TrueMoon.Tests-Release.trx' --results-directory ./TestResults/ARCH2e4-FileDrain-20261029-1 --verbosity quiet '-bl:./TestResults/ARCH2e4-FileDrain-20261029-1/TrueMoon.Tests-Release-{}.binlog'
dotnet test ./TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug --logger 'trx;LogFileName=TrueMoon.Aether.Tests-Debug.trx' --results-directory ./TestResults/ARCH2e4-FileDrain-20261029-1 --verbosity quiet '-bl:./TestResults/ARCH2e4-FileDrain-20261029-1/TrueMoon.Aether.Tests-Debug-{}.binlog'
dotnet test ./TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Release --logger 'trx;LogFileName=TrueMoon.Aether.Tests-Release.trx' --results-directory ./TestResults/ARCH2e4-FileDrain-20261029-1 --verbosity quiet '-bl:./TestResults/ARCH2e4-FileDrain-20261029-1/TrueMoon.Aether.Tests-Release-{}.binlog'
```

Связь controlled starvation с точной причиной прежнего intermittent эпизода
не установлена: у исторического failure нет heap и точной file phase. Исправлен
доказанный shared-pool dependency, historical observation сохранён отдельно.
Passing повторы сами по себе не объявляются доказательством отсутствия иных IO
faults. Hardware/driver/K7/general release и полный solution scope не меняются.

## Фазы process output capture — 2026-10-29

Для ARCH2e.4 ProcessOutputCapture получил ReadCompletion/FileCompletion/FileStage при
прежнем Completion=WhenAll и borrowing contract. Это позволяет отличить EOF/read
failure от незавершённого file sink в bounded cleanup timeout. Constructor,
ProcessOutputSnapshot, бюджеты и поведение file errors не менялись. Actual stream
ownership исправлен в Aether, Core не закрывает borrowed reader.
Core373/373 Debug/Release прошли; новый combined read/close/timeout proof находится
в Aether output-owner tests. [Точная история, команды и ограничения](../engine/HISTORY.md#arch2e4-stdio-ownership-и-диагностика-drain--2026-10-29).
Свежий run e4-Packages1 показал completed readers при pending file sinks; добавлены FileStage и thread-pool counters. Исходная причина intermittent timeout не установлена; новая диагностика не
объявляется исправлением этого observation. Общий solution run не повторялся.

## Чтение и импорт child diagnostics — 2026-10-27

Contracts добавляет IDiagnosticRecordReader/Importer и DiagnosticOrigin; старый
DiagnosticRecord positional constructor сохранён. UseDiagnostics(pipeline)
регистрирует nondisposable borrowed access views, один owner pipeline для Cobalt
и MS DI. Повторный setup/legacy overload заменяет views без создания discarded sinks.
[API/семантика](DIAGNOSTICS.md#чтение-и-импорт-bounded-history),
[transport owner](../engine/DIAGNOSTICS_DELIVERY.md).

ReadAfter — exclusive cursor, oldest-first independent bounded snapshot1..1024.
LatestSequence включает unavailable slots. Import назначает host appid/sequence
и origin child appid/sequence, сохраняет PID/UTC/trace/exception strings через
local clipping/refreeze. Authoritative context копируется первым; live filters,
ring и sink queues остаются общими. Self/invalid/recursive origins отвергаются;
импорт не публикует global DiagnosticListener event.

Новые18methods/59cases:
[Requirement | Evidence matrix](../../TestResults/DiagnosticsDelivery-20261027-1/tests-core-matrix.md),
[assertion/gap review](../../TestResults/DiagnosticsDelivery-20261027-1/tests-core-quality.md).

| Требование | DiagnosticImportTests |
| --- | --- |
| Cursor/bounds/eviction и independent snapshot | ReadAfter_UsesExclusiveHostCursorAndOldestFirstBoundedOrder; ReadAfter_MaximumPageAndEvictionKeepEarlierSnapshotsIndependent; ReadAfter_InvalidBoundsThrowWithoutChangingHistory |
| Refreeze/context priority/origin/trace и clipping | TryImport_RefreezesClippedAuthoritativeContextAndPreservesChildCorrelation; TryImport_PropertyBudgetPrioritizesContextAtAndBelowExactBoundary; TryImport_TracePairIsPreservedOrClearedTogether |
| Invalid/recursive/closed/sink budgets, no global replay | TryImport_InvalidIdentityOrRecursiveOriginRejectsBeforeAdmission; TryImport_DoesNotReemitToGlobalListenersOrFeedAnotherCollector; TryImport_UsesReservedBoundedSinkQueueAndClosedLateWriteAccounting; TryImport_MemoryAndSinkByteLimitsAcceptExactBoundaryAndRejectOneByteBelow |
| Both-DI replacement и exactly-once pipeline ownership | AppRegistration_ReplacementProvidesOneBorrowedAccessAndDrainsOwnedPipeline; AppRegistration_DefaultOrLegacyHasNoDeliveryAccessAndNeverCreatesDiscardedPipeline |

Initial narrow59/59. Whole-Core RED372/373 выявил неверное абсолютное ожидание
пустого collector в App-test: Expected0 Actual2, DiagnosticImportTests line395,
parallel Apps публиковали process-global TrueMoon events. Fix только в тесте:
уникальная imported provenance, sink barrier только для своей записи, relative
cursor и exact closed-history/disposal checks. Global routing не менялся.
Rebuilt whole-Core GREEN373/373; RED/GREEN artifacts core-full-debug-repro-1.*
и core-full-debug-fixed-1.* сохранены в evidence directory.

Final Core373/373 Debug/Release, failed/skipped0; все59newcases passed.
Full solution4285/4285 в каждой конфигурации, обе сборки0errors:
Debug nonincremental1546warnings/Release incremental1316warnings, SDK10.0.403.
[Общая matrix/команды/границы](../engine/HISTORY.md#child-diagnostic-delivery--2026-10-27),
[evidence](../../TestResults/DiagnosticsDelivery-20261027-1/verification.json).
Source hashes1020/changes0, test binaries20, user DI10.0.14 SHA unchanged.
Aether source3-child proof относится к transport consumer; GPU/published/package/
performance acceptance не повторён. CORE6 остаётся частичным.


## Explicit parent и kind для IPC — 2026-10-26

IDiagnosticTelemetry/DiagnosticTelemetry получили StartOperation(name,kind,parent,properties)
для Client/Server spans с explicit remote parent. Old overload использует Internal
и current flow. Parent argument обязателен, null выбирает current flow; default literal
в старом двухаргументном вызове не стал неоднозначным. Default interface implementation
вызывает старый API custom owner без принудительного вычисления properties.
Invalid ActivityKind отклоняется до observer/property callbacks. Bounded sampling,
метрики, callback isolation и deferred ownership release сохраняются.

Новые6cases в DiagnosticTelemetryTests: ExplicitRemoteParent_UsesRequestedKindInsteadOfAmbientParentAndRestoresCurrent(2),
InvalidActivityKind_IsRejectedBeforePropertiesOrObservers(2),
InterfaceKindOverload_ForwardsToExistingOldOnlyImplementationWithoutEvaluatingProperties(1),
OldOperationOverload_DefaultLiteralRemainsUnambiguousAndUsesInternalKind(1).
Final narrow62/62, full Core314/314 Debug/Release. Full solution Debug4021/4021,
Release4020/4021 из-за прежнего Aether CPU receiver failure; общий green не заявлен.
Builds0errors, Debug nonincremental1530warnings/Release incremental1300warnings,
SDK10.0.403. [Общая matrix/команды/ошибки](../enerit/HISTORY.md#ene6d1-trace-context-через-pipes--2026-10-26),
[evidence](../../TestResults/DiagnosticsIpc-20261026-1/verification.json).
Source fingerprints993/test binaries20, source changes0. Native/published/GPU/
package/performance проверки не повторены. CORE6 остаётся частичным.

## Операции и трассы диагностики — 2026-10-26

Продолжение запроса «давай следующий шаг»: первый CORE6d профиль и engine consumer.
Contracts определяет IDiagnosticTelemetry/IDiagnosticOperation/DiagnosticOperationOutcome;
TrueMoon владеет ActivitySource/Meter с именем TrueMoon. Опции копируются до resources.
Базовый App лениво регистрирует единственный interface owner; UseDiagnostics заменяет его.
[API, sampling, численные limits и lifetime](DIAGNOSTICS.md#трассы-и-метрики-операций).

Метрики truemoon.operation.count и truemoon.operation.duration (секунды) имеют только
operation/outcome tags. Registry64имени (предел256), trace properties16×256,
имя/ключ128; sampling None/PropagationData не вычисляет custom properties.
Первый Complete фиксирует outcome, Dispose публикует once; без Complete — Incomplete.
Observer/property failures и sync reentry изолированы, status содержит счётчики.
Закрытый owner сохраняет source/meter до Dispose последнего active scope.
Exception objects не удерживаются, error.type ограничен; внешних exporters/MEL нет.

| Требование | Точное поведенческое доказательство в DiagnosticTelemetryTests |
| --- | --- |
| Unobserved/disabled/sampling и lazy properties | NoConsumers_ReturnsNullWithoutEvaluatingPropertiesOrRejectingWork; EnableFlags_IndependentlyControlTraceAndMetricConsumers; Sampling_ControlsScopeAndLazyPropertiesWithoutCountingConsumerRejection; AllDataProperties_BoundInspectedEntriesClipSurrogatesAndPreserveReservedMetadata |
| Async parent и TraceId/SpanId событий | NestedAsyncOperations_RestoreCurrentAndCorrelateDiagnosticRecords |
| Counter/duration/outcomes и bounded dimensions | OperationOutcome_FirstCompleteWinsAndRepeatedDisposePublishesExactlyOnce; MetricsOnly_UsesNoActivityOrPropertiesAndNeverAddsHighCardinalityTags; NameCardinality_RejectsNewNamesAtLimitButContinuesKnownOperations |
| Options snapshot и границы | Options_AreCopiedBeforeCallerMutatesFlagsAndEveryLimit; InvalidBudgets_AreRejectedBeforeAppCreatesAnySink; BudgetBounds_AcceptFirstAndLastValidValues |
| Нет удержания Exception/payload | Completion_RetainsOnlyExceptionTypeWhileScopeIsStillActive |
| Listener faults/construction/reentry | ThrowingCallbacks_AreCountedAndRestoreParentWithoutBreakingCaller; ReentrantCallbacks_RejectNestedWorkWithoutRecursingOrCorruptingOuterScope; Constructor_ContainsInstrumentPublishedFailuresAndLeavesTracingUsable; Constructor_ContainsShouldListenFailureAndKeepsMetricsUsableWithoutActivitySource |
| Default App, обе DI, lifecycle и повторный setup | DefaultAppRegistration_ProvidesUsableTelemetryWithoutExplicitDiagnostics; AppRegistration_HasOneInterfaceOwnerAndDefersReleaseForActiveScope; AppRepeatedSetup_ReplacesTelemetryAndCopiesNestedOptionsForBothResolvers; CloseWhileActive_FencesAdmissionAndDefersInstrumentationReleaseUntilLastScope |

[Тестовый source](../../TrueMoon.Tests/DiagnosticTelemetryTests.cs).
Новые56Core cases; финальный Core308/308 Debug/Release.
[Aether matrix](../engine/HISTORY.md#операции-и-трассы-диагностики--2026-10-26) имеет25новых cases.
Полный solution **3921/3921 Debug и3921/3921 Release**, failed/skipped0:
Core308, Cobalt179/generator91, Enerit166/generator114, Mithril99,
UI893/Hosting114, Aether1216/Editor741 в каждой конфигурации.

SDK10.0.403. Финальный Debug build nonincremental0errors/1530warnings;
Release incremental0errors/1300warnings. Разница warnings отражает scope rebuild.
Команды из src, каждое MSBuild invocation имеет отдельный binlog:

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v:quiet -m -bl:TestResults/DiagnosticsTelemetry-20261026-1/build-Debug.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug -v:minimal --logger trx --results-directory TestResults/DiagnosticsTelemetry-20261026-1/Debug -bl:TestResults/DiagnosticsTelemetry-20261026-1/tests-Debug.binlog
dotnet build TrueMoon.slnx --no-restore -c Release -v:quiet -m -bl:TestResults/DiagnosticsTelemetry-20261026-1/build-Release.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Release -v:minimal --logger trx --results-directory TestResults/DiagnosticsTelemetry-20261026-1/Release -bl:TestResults/DiagnosticsTelemetry-20261026-1/tests-Release.binlog
```

Первый compile attempt имел3CS0200 из-за readonly return type preview context;
исправлен Dictionary return type. Initial full Debug до default registration:
3831passed/88failed из3919; все88 — IDiagnosticTelemetry отсутствовал в базовом App
без UseDiagnostics. Исправлен AppBuilder graph, добавлены2regression cases обеих DI.
Следующие Core narrow56/56 и полный Aether1216/1216 Debug прошли, затем приведённые
выше full Debug/Release. Failed artifacts сохранены в build-initial/Debug-initial;
это не скрытый повтор для получения green результата.

[Verification](../../TestResults/DiagnosticsTelemetry-20261026-1/verification.json):
34TRX,12binlogs,986source hashes,20test binary hashes, source changes во время
финальной проверки0. Narrow artifact prefixes agent-core-default-1793034022970
и agent-aether-full-1793034043722. Intermediate test-agent state сохранён в OS temp,
в git changes его нет. Assertions/gaps review не выявил оставшихся обязательных
fixes в объявленном scope; root повторно прочитал новые assertions.
Документационная проверка:87relative links/anchors разрешены,4ссылки на ранее
недоступный diagnostics artifact от25октября отмечены historical unavailable;
новых missing targets/anchors0. git diff --check HEAD прошёл.

Manual native/published/GPU/package/performance checks не повторялись.
Исторические Mithril ready-file/CPU receiver races этим passing run не исправлены.
CORE6d частичен: ENE6d IPC trace propagation, дополнительные domain/frame aggregate
metrics, live Editor console/export, bootstrap/private routing/generation и release gates
остаются отдельными задачами.

## Emergency и managed JSONL runs — 2026-10-26

Продолжен CORE6c: независимый finite emergency worker и opt-in run directories
под постоянным JSONL root. [API/defaults/ownership](DIAGNOSTICS.md).
DiagnosticEmergencyOptions: enabled, borrowed Console.Error, queue8 включая
in-flight,16attempts на pipeline, report≤1024chars с LF. Snapshot содержит только
App/Stage/Sink/type/sequence; без payload/exception objects/messages. Writer
failure не вызывает Report; reentry ограничен общим lifetime budget. Health
отделяет rejected queue attempts от последующих Suppressed.

Managed JSONL: JsonUseRunDirectories + copied JsonRetention, default8closed runs /
32MiB /7суток от creation UTC. Lazy GUID directory, exclusive versioned marker
до закрытия файлов, root maintenance lease, oldest-first pruning, строгие
границы с разрешённым equality, newest/explicit protection и observable
overbudget. Только известные плоские marker/events files удаляются нерекурсивно;
active, legacy/unknown/unsafe directories остаются отдельно. Crash освобождает
lease, распознанный run доступен следующему sweep. Flat API/старый4arg constructor
сохранены. Failed maintenance не подменяет успешную запись; status и emergency
показывают проблему. Никакого общего quota report/artifact roots не заявлено.

Normal sink cleanup запускается off caller, включая синхронную часть DisposeAsync.
Normal cleanup затем emergency drain используют один ShutdownTimeout; rollback
всех уже приобретённых sinks идёт параллельно и также имеет один deadline.
Timeout не освобождает исполняющееся IO и borrowed writer. Background cleanup
остаётся до завершения; direct pipeline disposal допускает retry. До создания
самого pipeline bootstrap capture этим срезом не реализован.

| Требование | Evidence: точное имя теста |
| --- | --- |
| Ограниченные metadata, control sanitization, LF и отсутствие exception retention | EmergencyReport_UsesOnlyBoundedMetadataAndOneLiteralNewline; EmergencyReport_DoesNotRetainExceptionWhileWriterIsBlocked |
| Queue включает executing, lifetime attempts/drop/suppression различаются | EmergencyReport_CountsExecutingQueueAndDroppedAttemptsBeforeLifetimeSuppression |
| Writer failure/borrowed lifetime и finite reentry | EmergencyWriterFailure_CountsTypeWithoutMessageOrRecursiveReportsAndKeepsWriterBorrowed; EmergencyWriterReentry_IsFiniteAndNeverWaitsOnItsOwnWorker |
| Normal/capture/overflow failures без payload | Pipeline_ReportsWriteAndDisposeFailuresWithoutPayloadOrExceptionMessage; Pipeline_CaptureFailureUsesEmergencyWithoutPublishingPartialRecord; Pipeline_ReportsOnlyErrorAndCriticalQueueLossWhileNormalSinkIsBlocked |
| Producer/healthy sink независимы от blocked emergency IO | BlockedEmergencyWriter_DoesNotBlockProducersOrHealthySink |
| Flush/timeout/retry/off-caller cleanup/common rollback | PipelineFlush_WaitsForEmergencyReportProducedAfterNormalFence; PipelineShutdownTimeout_RetainsBlockedOwnerAndAllowsRetry; PipelineDispose_SynchronousSinkCleanupRunsOffCallerAndTimeoutKeepsOwnership; ConstructorRollback_UsesOneTimeoutAndRetainsConcurrentCleanupUntilReleased |
| Startup error, disabled и validation до IO | PipelineConstructionFailure_DrainsStartupReportAndReleasesOwnedSinkOnly; Pipeline_EmergencyDisabledDoesNotWriteOrChangeNormalFailureHandling; InvalidEmergencyBudgets_AreRejectedEvenWhenDisabledBeforeSinkCreation |
| Closed runs count/bytes/age и equality | Sweep_CountLimitDeletesOldestByCreationInsteadOfDirectoryMtime; Sweep_ByteBudgetIncludesMarkerAndRetainsExactEquality; Sweep_AgeExpiresOnlyStrictlyAfterCreationLimit |
| Active/protected/unknown/reparse ownership | Sweep_ProtectsActiveRunAcrossStoreInstancesUntilItsLeaseCloses; Sweep_ProtectsNewestAndExplicitJobAndReportsUnsatisfiedBudget; Sweep_PreservesUnknownIncompleteAndNonflatDirectoriesWhilePruningOwnedJobs; Sweep_RejectsReparseAncestryAndEntriesWithoutTouchingTargets |
| Maintenance lease/failure/retry и legacy safety | Sweep_MaintenanceContentionPreservesJobsAndRecoversWithoutBlocking; Sweep_DeleteFailurePreservesOwnershipContinuesAndRetriesAfterHandleRelease; Sweep_PreservesLegacyFlatFilesAndUnrelatedDirectories |
| Managed sink lazy/independent and prefix validation | ManagedJsonSink_CreatesLazyIndependentRunsAndRetainsOnlyNewestClosedRun; UnusedJsonSink_DisposeDoesNotCreateOutput; ManagedJsonSink_RejectsPrefixesOutsideOwnedWhitelistBeforeIo |
| Retention failure/overbudget до emergency closure | Pipeline_RetentionMaintenanceFailurePreservesRecordAndReportsEmergencyThenRecovers; PipelineDispose_ReportsProtectedOversizedRunBeforeClosingEmergencyOutput |
| App Cobalt/MS DI фиксирует nested options и ownership | AppManagedDiagnostics_CopiesNestedOptionsForBothResolvers |

Новые73cases: Emergency22, RunRetention41, ManagedPipeline10. Narrow134/134 с
прежними61pipeline/legacy cases. Полный Core252/252 Debug/Release. Финальные
solution3840/3840 Debug и3840/3840 Release, failed/skipped0; Aether1191/Editor741,
Cobalt179/generator91, Enerit166/generator114, Mithril99, UI893/Hosting114.
SDK10.0.403, Windows x64/net10.0. Debug nonincremental build0errors/1530warnings;
Release incremental0errors/302warnings (разный rebuild scope, не warning cleanup).
Evidence: [verification](../../TestResults/DiagnosticsCore-20261026-1/verification.json),
30TRX включают initial/final runs,6unique binlogs,978source и20tested binary hashes.

Первый полный Debug был3838/3840:73новыхcases прошли, но legacy
AppDiagnosticsAreObservedAndDisposedEvenWhenAListenerThrows использовал
TrueMoon-prefixed probe, на который подписывался параллельный App с default filter.
Тест исправлен на UniqueName()/Filters(name) вне общего префикса; lifecycle
assertions сохранены, production global routing/совместимость не менялись.
После этой правки отдельный Core252/252 и новый full Debug3840/3840 прошли.

В том же initial Debug повторился прежний MithrilRestartProbe ready-1.json sharing
IOException, scenario concurrent-stop. Mithril production/fixture не менялись;
последующие99/99 Debug/Release не означают исправление race. Initial failures
и exact errors сохранены в verification и initial Debug TRX/log; исходный run
не подменён passing повтором.

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v:quiet -m -bl:TestResults/DiagnosticsCore-20261026-1/build-Debug-final.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug -v:minimal --logger trx --results-directory TestResults/DiagnosticsCore-20261026-1/Debug-final -bl:TestResults/DiagnosticsCore-20261026-1/tests-Debug-final.binlog
dotnet build TrueMoon.slnx --no-restore -c Release -v:quiet -m -bl:TestResults/DiagnosticsCore-20261026-1/build-Release.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Release -v:minimal --logger trx --results-directory TestResults/DiagnosticsCore-20261026-1/Release -bl:TestResults/DiagnosticsCore-20261026-1/tests-Release.binlog
```

Parent Editor переключён на постоянный %TEMP%/TrueMoon/Diagnostics/Editor root;
smoke остаётся в report/diagnostics/editor, Runtime использует общий
report/diagnostics/runtime. Каждый logger владеет отдельным managed run.
Отдельные native/published/UI/GPU/hardware/package/performance acceptance не
повторены; source wiring compiled и managed Editor741/regressions прошли.
Общие reports/artifacts/raw output не становятся owned Core quota.
Далее CORE6d spans/metrics и engine producers/ENE6d IPC context; UI console/export,
App-private routing, bootstrap-before-pipeline capture, generator и измерения
allocations/throughput сохраняют самостоятельные задачи.

[План](PLAN.md), [текущий статус](STATUS.md), [общий статус](../STATUS.md).

## Структурированная диагностика: первый профиль — 2026-10-25

По запросу приступить к согласованным улучшениям реализован первый CORE6a–c профиль.
Own contracts DiagnosticLevel/Message/Record, lazy structured Write/IsEnabled,
frozen bounded snapshots, live source/event gates, per-sink levels, independent
bounded workers, memory ring, console/JSONL, health и bounded disposal с retry.
Legacy synchronous OnEvent/process-global subscription semantics сохранены.
[API, budgets и ownership](DIAGNOSTICS.md).

Core primitive ProcessOutputCapture читает borrowed TextReader независимо от
file writer, не ожидает newline, хранит bounded live tail и фиксирует file loss/
errors. UTF8 file limit учитывает bytes, а не characters. Source namespaces
соответствуют RootNamespace/пути; новых packages/providers/scopes нет.

| Поведение | Конкретная проверка |
| --- | --- |
| Lazy/live source/event/sink filters | SourceAndEventFilters_UseLongestPrefixSnapshotAndLiveChangesBeforeLazyProperties; SinkLevelFilters_AreIndependentSnapshotsAndDoNotFilterMemoryHistory |
| Immutable/clipped snapshots и Activity IDs | StructuredRecord_CopiesAndBoundsPropertiesMessagesAndExceptionsWithoutSplittingSurrogates; StructuredRecord_CapturesCollectorProcessUtcAndActivity |
| Queue count/bytes/in-flight и error reserve | BoundedSink_ReservesErrorSlotsAndCountsExecutingWrites; BoundedSink_RejectsByteOverflowAtExactBudgetAndRecoversAfterDrain |
| Slow/failing sink isolation | SlowAndFailingSinks_AreIndependentAndFailureDoesNotStopLaterWrites |
| Memory eviction/byte boundary и flush fence | MemoryRing_EnforcesExactByteBudgetAndRejectsOversizedRecord; FlushAsync_WaitsOnlyForWritesAcceptedBeforeItsCall |
| Shutdown/failed cleanup ownership | DisposeTimeout_ClosesAdmissionAndRetainsSinkOwnershipUntilRetry; SinkDisposeFailure_IsObservableAndRetryReleasesExactlyOnce |
| App setup/cleanup на Cobalt и Microsoft DI | RepeatedAppSetup_CreatesOnlyReplacementPipelineAndSink; AppDispose_DrainsStopAndServiceCleanupEventsBeforeDisposingSinks |
| JSONL ownership/rotation/reopen | JsonSink_ReopenShrinksOwnedSlotsAndKeepsOtherNamesAndPrefixes; JsonSink_ExclusiveOwnerRejectsConcurrentWriterAndReleasesLeaseOnDispose |
| Continuous raw capture/borrowed reader | TwoCaptures_ExposeLiveIndependentTailsBeforeEofAndBorrowReaders |
| UTF8/file failure не останавливает drainage | FileCapture_RespectsUtf8ByteBudgetAcrossSplitSurrogateChunks; FileFailure_IsObservableAndDoesNotStopReadingOrTailRetention |

Все новые assertions просмотрены, broad research/plan/status находятся в OS temp,
не в stageable repository content. Finding-tests provider отсутствовал (Tool
findTests not found); применён разрешённый source/search fallback и Roslyn pairing.
Во время narrow review обнаружен обход legacy Subscribe override при новом
predicate overload: legacy path возвращён,18existing diagnostics cases прошли.
Новых production fixes за пределами diagnostics/capture не заявлено.

Проверки Windows x64/net10.0/SDK10.0.402:

- Final focused Core80/80:43pipeline +19capture +18legacy; Aether7/7 отдельно.
- Full Core179/179 Debug и179/179 Release:62новых +117прежних cases.
- Full solution3725/3725 в каждой конфигурации, failed/skipped0,20TRX total.
- Nonincremental Debug build и Release build:0errors/1530warnings каждый.
- Current native consumer: Debug + published Release вне checkout, по6verbose
  children/stream bounds и launch failure; source/binary fingerprints сохранены
  в [общем evidence](../../TestResults/Diagnostics-20261025-1792916778832/verification.json).

Команды (каждая build/test/publish команда имела отдельный binlog):

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v:quiet -m -bl:TestResults/Diagnostics-20261025-1792916778832/final-build-Debug.binlog
dotnet build TrueMoon.slnx --no-restore -c Release -v:quiet -m -bl:TestResults/Diagnostics-20261025-1792916778832/final-build-Release.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug -v:minimal --logger trx --results-directory TestResults/Diagnostics-20261025-1792916778832/Debug -bl:TestResults/Diagnostics-20261025-1792916778832/solution-tests-Debug.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Release -v:minimal --logger trx --results-directory TestResults/Diagnostics-20261025-1792916778832/Release -bl:TestResults/Diagnostics-20261025-1792916778832/solution-tests-Release.binlog
```

CORE6 в целом частичный: source-private App routing, bootstrap-before-resolver,
age/total directories retention, independent emergency output, operation spans/
metrics/IPC propagation и generator впереди. Fresh NuGet consumers, performance/
allocations, physical/golden/GPU loss/VRAM не повторены. Existing provider-specific
ownership и cooperative shutdown limits сохранены.

## Сквозной headless proof — 2026-10-15

Последняя часть согласованного Core quality прохода: configuration + diagnostics +
App lifecycle/Cobalt в настоящем console consumer. Production/runtime/contracts и
unit tests этим шагом не менялись. Расширены общие исходники CorePackageConsumer,
добавлен source project CoreHeadlessProbe и общий runner. Same sources используются
через ProjectReference и свежие независимые пакеты. Script не входит в solution;
сам source project собран и опубликован отдельно Debug/Release.

| Сценарий | Проверенные observables |
| --- | --- |
| normal | lifetime.Cancel → штатный stop/Dispose; точный trace |
| cancel | Original external token после cleanup; отдельный shutdown token |
| start-failure | Original StartError; reverse rollback только запущенных services |
| stop-failure | Original StopError; остановка остальных services и Dispose продолжаются |
| build-failure | Original BuildError, диагностическое payload identity; уже созданный ресурс закрыт, startup не начат |
| config-failure | Original ConfigurationError; регистрации/ресурсы/subscriptions не начинают работу |
| cleanup-failure, только Cobalt | AggregateException с двумя original StartError/CleanupError; остальные owned объекты освобождены |

Во всех runtime paths кроме config-failure JSON null преобразуется через
GetOrCreate в ConsumerOptions с BatchSize23, сохраняется в файл и повторно читается
с той же identity до refresh. Malformed JSON refresh сохраняет прежние sections;
после восстановления файла refresh работает, старший provider даёт Answer17.
Двойной UseDiagnostics не доставляет obsolete events и имеет одну subscription/
factory; throwing observer изолирован, framework/consumer events наблюдаемы.
После shutdown новые listeners не подписаны, запись в owned source запрещена.

OwnedFactory открывает FileStream/FileShare.None и пишет marker. Во время Start/
Stop/build-failure exclusive reopen отвергается, после cleanup внутри того же
процесса файл открывается эксклюзивно и marker читается. Счётчики собственных/
transient/borrowed objects проверяются независимо от файла. Для config-failure
файл не существует и opens/releases0; для остальных opens/releases1. Cobalt shared
Composite Dispose1, Microsoft DI alias calls сохраняют provider semantics.
Cleanup-failure специально ограничен Cobalt; native Microsoft DI Dispose-error
поведение не адаптировалось и не объявляется equivalent.

Предварительно: source Debug build0errors/0warnings;3Cobalt процессы normal/config-
failure/cleanup-failure прошли в TestResults/CoreHeadless/preflight-20261015-113857858.
Затем финальный единый run:

```powershell
pwsh -NoProfile -File ManualTests/CoreHeadlessProbe/RunHeadlessProof.ps1
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --logger 'trx;LogFileName=core-debug.trx' --results-directory TestResults/CoreHeadless/run-20261015-113928712/validation
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Release --logger 'trx;LogFileName=core-release.trx' --results-directory TestResults/CoreHeadless/run-20261015-113928712/validation
dotnet build TrueMoon.slnx -c Debug --no-incremental --no-restore --verbosity quiet
```

Все exit0; Windows x64/net10.0/SDK10.0.402/.NET10.0.13, xUnit2.9.3/VSTest.
Core107passed/0failed/0skipped в каждой Debug/Release,2TRX/214passed суммарно.
Full nonincremental Debug solution build0errors/1183warnings. Отдельных новых unit
tests нет; текущий suite является regression после свежей package сборки.

Source run: TestResults/CoreHeadless/run-20261015-113928712, Completed=true,
52processes (Cobalt28/Microsoft24), Debug/Release × build/publish;57commands
включая4build/publish и nested package runner. Source manifest130files.
Package run: TestResults/CorePackages/core-quality-20261015-113939900,
Completed=true; version0.2.2.7-alpha.headless.20261015113940134,4local nupkgs,
80matrix processes (Cobalt headless28 +direct4/Microsoft48) +3delivered README.
MS-only compiler opt-out проверен; negative opt-out при обязательной generation —
expected CS0246/RHeadlessConsumer. Package source manifest127files.

Пакеты восстановлены из собственного feed в изолированный cache с проверкой source
metadata/version/assets. Analyzer path/SHA и generated ModuleInitializer проверены;
runtime output не содержит analyzer/Roslyn/UI/IPC/units assemblies. Source и package
fingerprints повторно сверены до обоих Completed=true;57command logs,52report SHA
и package-summary SHA находятся в source summary, подробные package logs/reports
в nested summary. Старые64+3 runs относятся к прежней5-scenario матрице; текущая
80+3 включает реальные ресурсы и6/7сценариев, их нельзя смешивать с COB7 graph v1.
Benchmark graph v1/source-файлы не менялись, измерения этим шагом не повторены.

Финальный VerifyFinal.ps1/final-audit.json в source run:130/127source fingerprints,
mismatches0;52report SHA/package-summary SHA и3copies каждого consumer source
совпали. Из128App reports108 подтверждают file resource,20 — config failure без
ресурса,8 — Cobalt combined startup/cleanup failure. Все package App reports имеют
matching package informational version. Проверены2TRX,9документов/177local links/
94anchors, AST обоих runners и scoped git diff --check. Source/reference consumer
Debug/Release builds0warnings/0errors; это не zero-warning всего solution.

Согласованный Core quality проход закрыт в bounded scope: configuration/bootstrap,
diagnostics и source/package App proof. Нет подтверждённых новых Core блокеров
для следующего модуля; это не general release. Сохраняются cooperative shutdown30s,
process-global prefix diagnostics/in-flight callback limit, JSON concurrency/
crash-safe writes/permission/disk-full limitations, warnings и непроверенные
OS/RID/trimming/AOT modes. Не выполнялись собственные Cobalt/generator/Enerit/
Mithril suites, IPC/units, UI/native и performance; library→app/legacy delivery
controls отдельного RunDeliveryChecks также не повторялись.
Далее Enerit quality review/ENE5 baseline; движок ждёт сводки остальных модулей.

## Diagnostics quality — 2026-10-15

Продолжение собственного Core quality после configuration/bootstrap. Исправлены
DiagnosticObserver и UseDiagnostics registration; existing DiagnosticsTests
расширен7methods/10cases. App/service/diagnostics interfaces и dependencies прежние.

Подтверждены две ошибки: dictionary держал completed DiagnosticListener до Dispose
общей подписки; repeated UseDiagnostics оставлял прежние descriptors, и ResolveAll
создавал старые subscription/factory вопреки выбранной последней конфигурации.
Original red:3failed/0passed/0skipped (GC ownership1, повторный setup2providers).
Baseline8cases до изменений прошёл. Новые observers удаляют завершённый source по
identity и освобождают его handle. Reserve выполняется до Subscribe, сам Subscribe
и освобождение handles — вне общего lock; поздний handle после completion/Dispose
отсоединяется. Already-disposed listener с пустым handle также удаляется.

Поведение Subscribe для already-disposed source сверено с
[официальным исходником runtime10.0.0](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/DiagnosticListener.cs):
подписка может вернуться без OnCompleted. Текущая проверка воспроизводит это на
установленном runtime через полностью созданный DiagnosticListener subclass,
закрывающийся до/после base.Subscribe; отдельно считает Dispose возвращённого handle.
Первоначальный стенд с Dispose прямо из AllListeners.OnNext смешивал этот контракт
с глобальным списком во время конструктора источника и заменён. Intermediate
edge-cases log/TRX содержит также исправленную ошибку filter prefix тестового
multi-App event; это не final passing evidence и не новый внешний блокер.

Повторный UseDiagnostics удаляет прежние subscription/factory registrations до
добавления новых, сохраняя last-call-wins settings. Prefix observation остаётся
process-global; тесты нескольких Apps доказывают независимость cleanup, а не
приватность событий. Factory по-прежнему владеет своими sources до Dispose.

| Требование текущего этапа | Exact test name в TrueMoon.Tests.DiagnosticsTests |
| --- | --- |
| Завершённые источники не удерживаются живой подпиской, same-name survivor/replacement продолжают доставку | CompletedSourcesAreReleasedWhileSubscriptionRemainsAlive |
| Completion до возврата Subscribe и already-disposed source; exactly-once handle Dispose;2cases | SourceCompletingDuringSubscribeIsReleased |
| Повторная настройка оставляет одну subscription/factory, старые callbacks не возвращаются;2providers | RepeatedDiagnosticsSetupKeepsOnlyLastRegistration |
| Закрытие одной App не мешает другой; собственные sources закрываются, future sources наблюдаются;2providers | DisposingOneAppKeepsOtherAppDiagnosticsActive |
| Новые filters применяются только при discovery | NewFiltersOnlyApplyToSourcesDiscoveredAfterTheChange |
| Dispose из callback отключает оставшиеся callbacks и будущие sources | DisposingSubscriptionInsideCallbackStopsRemainingCallbacks |
| Параллельный discovery/completion/Dispose не оставляет активных subscriptions;128sources | ConcurrentSourceDiscoveryCompletionAndSubscriptionDisposalDetachAllSources |

Применены dotnet-best-practices/code-testing-agent/finding-tests/run-tests.
IDE execute_tool вызван: findTests отсутствует. Для двух production targets
research/plan/status созданы вне Git в OS temp, workflow выполнен inline без
доступного специализированного generator agent. Roslyn find-untested-sources
выполнен один раз на bounded snapshot7diagnostic source files/1existing test file:
paired4/unpaired3. Subscription/Configuration/Source/Factory paired с DiagnosticsTests;
AppCreationContextExtensions/internal Observer/LoggingObserver statically unpaired
(extension/indirect caller не содержит declaring-type token). Это static pairing,
не execution coverage; existing public tests расширены без дублирования suite.

Assertion-quality и test-gap-analysis применены при финальном review: у7newmethods
нет assertion-free/trivial-only cases; проверены collection contents, точные counts,
identity, inactive listeners, error guards и memory ownership. Добавлен независимый
handle Dispose counter: GC сам по себе не доказывает освобождение handle внутри
неукоренённого цикла. NoInlining helper/KeepAlive исключают локальный JIT root и
раннее исчезновение владельца. Original defects killed эмпирическим red run;
оставшиеся candidate mutations оценены по source/assertions, не выполнены.
Bounded concurrency test не объявляет exhaustive race/stress доказательство.

Команды из src; все final commands exit0:

```powershell
# Baseline до изменений:8passed/0failed/0skipped.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --filter FullyQualifiedName~TrueMoon.Tests.DiagnosticsTests --logger 'trx;LogFileName=baseline.trx' --results-directory TestResults/CoreQuality/diagnostics-20261015
# До production fixes: exit1,3failed/0passed/0skipped.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --filter 'FullyQualifiedName~CompletedSourcesAreReleasedWhileSubscriptionRemainsAlive|FullyQualifiedName~RepeatedDiagnosticsSetupKeepsOnlyLastRegistration' --logger 'trx;LogFileName=red.trx' --results-directory TestResults/CoreQuality/diagnostics-20261015
# Final narrow:18passed/0failed/0skipped.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --no-build --filter FullyQualifiedName~TrueMoon.Tests.DiagnosticsTests --logger 'trx;LogFileName=diagnostics-debug.trx' --results-directory TestResults/CoreQuality/diagnostics-20261015
# Полный Core:107passed/0failed/0skipped в каждой конфигурации.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --logger 'trx;LogFileName=core-debug.trx' --results-directory TestResults/CoreQuality/diagnostics-20261015
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Release --logger 'trx;LogFileName=core-release.trx' --results-directory TestResults/CoreQuality/diagnostics-20261015
# Solution compile:0errors/1183warnings.
dotnet build TrueMoon.slnx -c Debug --no-incremental --no-restore --verbosity quiet
```

Windows x64/net10.0/SDK10.0.402, xUnit2.9.3/VSTest. Final2CoreTRX214passed,
Failed0/Skipped0; diagnostics18 в каждой конфигурации входят в107, отдельный18-case
Debug TRX не суммируется в214. Артефакты/логи в ignored
TestResults/CoreQuality/diagnostics-20261015. Source manifests старых package или
benchmark runs после Core изменений не считаются current. Другие module suites,
package consumers/IPC/units/UI/native/performance не повторены.

Diagnostics stage закрыт в указанном scope. Следующий этап: цельный headless proof
configuration/diagnostics/Cobalt, normal/cancel/startup failure/shutdown/cleanup,
затем сводка ограничений Core перед Enerit. Callback quiescence, process-specific
event routing и новый factory release API не добавлены.

## Configuration и bootstrap quality — 2026-10-15

Первый собственный Core quality шаг после COB7. Прочитаны общие/local планы,
configuration/contracts, AppBuilder и diagnostics; production fix ограничен
ConfigurationExtensions.GetOrCreate, existing ConfigurationTests расширен.
Новых API/providers/dependencies, смены DI provider и engine кода нет.

Подтверждён defect: unqualified GetOrCreate при existing null в именованной
секции записывал объект в default; старший null продолжал маскировать результат,
следующий вызов возвращал другой объект. Red run:4cases,1failed/3passed/0skipped,
exit1. Default/named variants уже проходили; исправлен именно источник записи
при unqualified lookup. Теперь null replacement идёт в найденную секцию,
missing unqualified — по-прежнему default, invalid вызывает FormatException.
JSON null replacement сохраняется на диск, повторное чтение не пишет файл.

Focused tests в existing suite, без создания нового test project или broad
coverage pipeline. Применены dotnet-best-practices, code-testing-agent,
finding-tests и run-tests; IDE findTests/execute_tool недоступен, использованы
существующие ConfigurationTests/AppLifecycleTests. Repo overlay run-tests отсутствует.
Сохранены repo xUnit/собственные DI contracts вместо generic skill MSTest/MS DI.

| Поведение | Exact test name в TrueMoon.Tests.ConfigurationTests |
| --- | --- |
| Null replacement, implicit type key, priority и identity;4variants | GetOrCreateReplacesWinningNullAndRetainsIdentity |
| Обычный unqualified Set пишет default | UnqualifiedSetKeepsDefaultTargetWhenNamedSectionMasksTheKey |
| Invalid blocking value не заменяется и не пропускается;2variants | GetOrCreateRejectsInvalidWinningValueWithoutChangingProviders |
| JSON persistence, repeated read без записи, нижний provider не изменён | GetOrCreatePersistsWinningJsonNullWithoutWritingDefault |
| Configuration actions раньше registrations/factory; тот же IConfiguration в App/DI;2providers | BootstrapConfiguresValuesBeforeRegistrationsAndServiceCreation |
| Configuration validation error identity; registrations/factory не вызваны;2providers | BootstrapConfigurationFailurePreservesCauseAndSkipsRegistrations |
| Ни один provider не обновляется при failure, recovery и старые handles;sync/async | FailedRefreshPreservesAllProvidersAndAllowsRecovery |
| Serialization failure сохраняет file/snapshot, retry пишет | JsonSerializationFailurePreservesFileAndSnapshotAndAllowsRetry |
| Sharing violation сохраняет file/snapshot, retry после release handle | JsonWriteFailurePreservesSnapshotAndAllowsRetry |

Добавлено16cases, ConfigurationTests11→27, Core81→97. Review assertions проверил
identity/нижний provider/disk state/полный порядок callbacks/original exception
и recovery; production change имеет failing-before/passing-after свидетельство.
Concurrency гарантия GetOrCreate не добавлена; file errors не подавляются.

Команды из src (stdout/stderr перенаправлялись в ignored TestResults):

```powershell
# До production fix: exit1, failed1/passed3/skipped0.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --filter FullyQualifiedName~GetOrCreateReplacesWinningNullAndRetainsIdentity --logger 'trx;LogFileName=red.trx' --results-directory TestResults/CoreQuality/configuration-20261015
# Финальный узкий run: exit0, passed27/failed0/skipped0.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --no-build --filter FullyQualifiedName~TrueMoon.Tests.ConfigurationTests --logger 'trx;LogFileName=configuration-debug.trx' --results-directory TestResults/CoreQuality/configuration-20261015
# Финальные полные Core runs: exit0, passed97/failed0/skipped0 каждый.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --logger 'trx;LogFileName=core-debug.trx' --results-directory TestResults/CoreQuality/configuration-20261015
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Release --logger 'trx;LogFileName=core-release.trx' --results-directory TestResults/CoreQuality/configuration-20261015
# Full solution compile: exit0, errors0/warnings1184.
dotnet build TrueMoon.slnx -c Debug --no-incremental --no-restore --verbosity quiet
```

Windows x64/net10.0/SDK10.0.402/xUnit2.9.3/VSTest. Два final Core TRX194passed,
Failed0/Skipped0; узкий27-case TRX отдельно. Full build выполнен до добавления
последнего JSON GetOrCreate test; финальный test source затем собран обоими Core
test runs. Production code после full build не менялся. Logs:
TestResults/core-quality-{red,configuration,debug,release,build}.log;
TRX в TestResults/CoreQuality/configuration-20261015. Red и initial24-case
configuration run предшествуют final27-case; intermediate96-case runs заменены
final97-case reports. Restore/build не подменяют test discovery.

Package/IPC/units/UI/native/performance и другие module suites не повторялись.
Общий build с warnings не доказывает их runtime готовность. Disk-full/permission
injection, process-shared JSON/atomic file replacement и concurrent GetOrCreate
не проверены. Собственный diagnostics quality и цельный новый headless proof
остаются следующими частями Core; текущие8 diagnostics tests — regression.

## Regression при Cobalt COB7 — 2026-10-15

Core/Contracts production/test без изменений; Core81/81 Release, Failed0/Skipped0.
Full nonincremental Release solution build exit0/errors0/warnings1185.
TrueMoon App graph проверен на Cobalt/MS в normal lifetime/external cancellation/
startup rollback/error recovery/cleanup paths. Exact source package consumer:
12processes/120scenarios Debug/Release build/publish, включая MS analyzer opt-out;
standard package64+3 иlibrary/app4/2constructor controls также прошли.
[Результаты/решение](../cobalt/COMPARISON.md), [команды](../cobalt/HISTORY.md#cob7-provider-comparison--2026-10-15).
Debug unit suites/IPC/units/UI/native не повторены. Migration отложена, существующий
Cobalt default/DI adapter сохранены. Далее собственный Core quality review; этот
comparison не закрывает configuration/diagnostics/Core utility criteria.

## Regression при Cobalt COB5 — 2026-10-15

Core/Contracts production/test code без изменений. Core81/81 Release, Failed0/
Skipped0; Debug suites не повторены. Fixed headless App graph использует настоящий
App.Build/Start/Stop/Dispose, workload guard проверяет lifecycle1/1 и cleanup.
Startup/resolution/allocations измерены в Cobalt COB5, без изменения bootstrap.
Full nonincremental Release solution build exit0/errors0/warnings1185.
[Команды/3TRX/351passed и baseline scope](../cobalt/HISTORY.md#cob5-app-baseline--2026-10-15).
Package consumer/IPC/units/UI/native не повторены; прежние COB6d package/Debug
результаты не являются текущим run. Core quality roadmap сохраняется отдельно.

## Regression при Cobalt COB6d — 2026-10-15

Core production/test и Contracts code без изменений. Core81/81 в каждой
Debug/Release, Failed0/Skipped0. Fresh direct/headless/provider package64/64 +3actual
README examples, Debug/Release build/publish; opt-out expected CS0246.
Full nonincremental Debug solution build exit0/errors0/warnings1185.
[Команды,6TRX/702passed и module library/app consumer](../cobalt/HISTORY.md#cob6d-registration-workflow--2026-10-15).
Собственный Core quality scope не считается завершённым этой регрессией; other
module suites, UI/native/performance не повторены.

## Regression при Cobalt COB6c — 2026-10-15

Core production/test и Contracts code без изменений. Core81/81 в каждой
Debug/Release, Failed0/Skipped0. Fresh direct/headless/provider package64/64 +3actual
README examples, Debug/Release build/publish; opt-out expected CS0246.
Full nonincremental Debug solution build exit0/errors0/warnings1185.
[Команды,6TRX/644passed и independent registration consumer](../cobalt/HISTORY.md#cob6c-registration-contracts--2026-10-15).
Собственный Core quality scope не считается завершённым этой регрессией; other
module suites, UI/native/performance не повторены.

## Regression при Cobalt COB6b — 2026-10-14

Core production/test и Contracts не менялись. Core81/81 в каждой Debug/Release,
Failed0/Skipped0. Creation/rollback/failure identity regressions прошли при новом
Cobalt runtime guard/metadata. Fresh headless/direct/provider package64/64 +3exact
README examples, Debug/Release build/publish; opt-out expected CS0246.
Full nonincremental Debug solution build exit0/errors0/warnings1187.
[Команды,6TRX/454passed и independent error consumer](../cobalt/HISTORY.md#cob6b-runtime-errors-и-recovery--2026-10-14).
Собственный Core quality scope этой проверкой не завершается; UI/native/performance
и остальные module suites не повторены.

## Regression при Cobalt COB6a — 2026-10-14

Core production/test code без изменений. Core81/81 в каждой Debug/Release,
Failed0/Skipped0. Fresh bundled package direct/headless/provider64/64 +3delivered
README examples, Debug/Release build/publish; opt-out expected CS0246.
Full nonincremental Debug solution build exit0/errors0/warnings1191.
Собственная Core quality программа не завершена этой регрессией; UI/native/
performance и прочие module suites не повторены.
[Команды,6TRX/360passed и package scope](../cobalt/HISTORY.md#cob6a-конструкторы-и-diagnostics--2026-10-14).

## Package regression с bundled Cobalt — 2026-10-14

TrueMoon package README/consumer references обновлены под bundled analyzer.
Fresh64headless cases +3точных delivered examples прошли Debug/Release build/publish,
negative generation control expected CS0246. Core81/81 отдельно Debug/Release,
Failed0/Skipped0; source Core logic/tests без изменений. [Команды/границы](../cobalt/HISTORY.md#единый-пакет-и-truemoon-scope--2026-10-14).

## Regression при начале качества Cobalt — 2026-10-14

Core production/test code не менялся. С изменённым Cobalt generator выполнен
unfiltered TrueMoon.Tests Debug/Release:81/81 в каждой configuration,
Failed0/Skipped0, commands exit0, Windows x64/net10.0/SDK10.0.402.
[Команды/артефакты/ограничения](../cobalt/HISTORY.md#cobalt-quality-и-try-helpers--2026-10-14).
Core package/headless consumers и другие module suites этим regression не повторены.

## Ревью ядра и CORE0 baseline — 2026-10-10

Запрос: посмотреть ядро TrueMoon и предложить улучшения. Прочитаны общие и локальные планы, bootstrap/lifecycle, configuration, diagnostics, Contracts и Core utilities. Cobalt runtime/generator и Microsoft DI adapter прочитаны в части app integration. Production код и тесты не изменялись; предложения ниже не являются принятыми изменениями PLAN.

### Фактически выполненные проверки

Windows, SDK **10.0.401**, Debug/net10.0, xUnit **2.9.3**, VSTest. `global.json`: 10.0.100 с `rollForward: latestMinor`. Overlay `src/.agents/skill-overlays/dotnet-test/run-tests.md` отсутствует.

```powershell
dotnet --version
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration Debug --framework net10.0 --logger 'trx;LogFileName=core-baseline.trx' --results-directory TestResults/CoreReview-20261010 --verbosity quiet
dotnet build TrueMoon.Core/TrueMoon.Core.csproj --configuration Debug --framework net10.0 --verbosity quiet
```

- TrueMoon.Tests: exit **1**, discovered **4**, **Passed: 3, Failed: 1, Skipped: 0**. Restore/build позволили выполнить тесты. Исторический AppCreate failure воспроизведён на текущем checkout.
- TrueMoon.Core: build exit **0**, errors **0**. Сборка incremental; runtime проверки scheduler/buffers не выполнялись. Этот проект не входит в зависимости TrueMoon.Tests.
- В логах есть NU1506 (повторные Roslyn PackageVersion), NU1507 (несколько package sources без mapping), в тестовом build — CS8602 в AppTests. Package настройки не исправлялись.
- Артефакты ignored: `TestResults/CoreReview-20261010/core-baseline.trx`, `TestResults/CoreReview-20261010/core-build.log`, `TestResults-CoreReview-20261010.log`.
- Cobalt.Tests/Cobalt.Generator.Tests, отдельный adapter scenario, Enerit/Mithril/UI suites, общий solution, Release/package consumer не запускались. Их исторические passing результаты сюда не переносятся.

### Воспроизведённый bootstrap/DI отказ

`TrueMoon.Tests.AppTests.AppCreate` падает с `ServiceResolvingException<IAppLifetime>: Could not resolve service for type: TrueMoon.IAppLifetime`.

Стек: `DefaultApp.StartAsync` → `ResolveAll<IStartable>` → `CobaltServiceResolverBase.ResolveEnumerable` → generated `LifeTimeExecutorResolver.Resolve` → `Resolve<IAppLifetime>`. В AppCreate зарегистрированы CommonStartableService и CommonService1; LifeTimeExecutor зарегистрирован в других сценариях того же test assembly.

По исходникам [CobaltServiceResolverBuilder](../../TrueMoon.Cobalt/CobaltServiceResolverBuilder.cs) выбирает все generated factories по одному ServiceType, не фильтруя выбранную ImplementationType/lifetime runtime handle. [ServiceResolvers](../../TrueMoon.Cobalt/ServiceResolvers.cs) хранит factories глобально, generator регистрирует их через ModuleInitializer. Поэтому runtime graph получает чужую implementation из той же сборки. Это не доказательство зависимости от порядка тестов: generated registrations общие уже при загрузке assembly.

Дополнительно по коду [generator](../../TrueMoon.Cobalt.Generator/CobaltGenerator.cs): Composite aliases получают FactoryCode, но GenerateTypeResolver не использует его при создании singleton. Alias resolver может возвращать незаполненное `_instance`. Требуется проверка единого экземпляра IAppLifetime/IAppLifetimeHandler. Владение resolver/generator fixes — COB1/COB3; интеграционный сценарий — CORE3.

### Приоритеты улучшения

**P0 — наблюдаемый lifecycle и cleanup (CORE1).** [App.RunAsync](../../TrueMoon/App.cs) ловит Exception и только отправляет diagnostic event; caller может получить successful Task после startup failure. [AppExtensions.RunAsync](../../TrueMoon/AppExtensions.cs) выполняет тот же сценарий без такого catch. В обоих путях отмена/ошибка StartAsync или WaitAsync обходит StopAsync/Stopping/Stopped; await using запускает disposal, но это не эквивалент graceful stop. StopAsync получает тот же run token, который при внешней отмене уже отменён. DefaultApp.StopAsync также подавляет ошибки отдельных сервисов.

Предложение: один runner для обоих entry points; явно определить cancellation outcome, startup rollback и shutdown errors; использовать независимый ограниченный shutdown token; сохранять первичную причину при cleanup failure. Учитывать реально запущенные сервисы, останавливать их в обратном порядке; определить repeat/concurrent Start/Stop/Dispose. Это предложения к contract review, текущим PLAN эти semantics ещё не утверждены.

**P0 — изоляция graph и lifetime aliases (CORE3 ↔ COB1/COB3).** Factory registry должен давать способ создания выбранной runtime registration, включая implementation и lifetime. Generated resolver не должен автоматически включать сервис во все app graphs. Composite должен разрешать один instance через несколько interfaces. Cobalt и Microsoft DI нужно проверить на одинаковом небольшом app scenario.

**P1 — scheduler shutdown (CORE4).** [TmTaskScheduler](../../TrueMoon.Core/Threading/TmTaskScheduler.cs) в Dispose завершает writer, но ThreadLoop выходит только по внешнему cancellation token. Enerit consumers создают scheduler без такого token: idle worker продолжает цикл после dispose. QueueTask после завершения writer бесконечно повторяет TryWrite. TryExecuteTaskInline допускает исполнение на вызывающем потоке, поэтому отдельный STA worker не гарантирует affinity. Предложение: ожидание очереди с учётом completion, явный drain/cancel contract, отказ от новых задач после stop и управляемое завершение workers. Inline/affinity определить по требованиям consumers; не превращать общий scheduler в UI dispatcher без отдельного решения.

**P1 — владение буферами (CORE4).** [ArrayPoolBufferWriter.Dispose](../../TrueMoon.Core/ArrayPoolBufferWriter.cs) каждый раз возвращает тот же массив; методы продолжают обращаться к нему после возврата. Предложение: идемпотентный dispose, ObjectDisposedException при последующем использовании, очистка references при возврате и копирование только written region при росте. [MemoryPoolUtils](../../TrueMoon.Core/MemoryPoolUtils.cs) возвращает backing array любого array-backed Memory, не доказывая происхождение/владение, включая slices. Для нового API предпочтителен owner/lease с однократным Dispose; существующие Enerit consumers мигрировать отдельно. [.NET ArrayPool.Return](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1.return?view=net-10.0) требует возврата rented buffer ровно один раз и прекращения использования после возврата.

**P1 — process и diagnostic subscriptions (CORE1/CORE2).** [DefaultApp](../../TrueMoon/DefaultApp.cs) подписывает анонимный Console.CancelKeyPress handler и не отписывается: static event удерживает app. Handler не устанавливает args.Cancel; для управляемого Ctrl+C shutdown это необходимо по [Console.CancelKeyPress contract](https://learn.microsoft.com/en-us/dotnet/api/system.console.cancelkeypress?view=net-10.0). Предложение: выбираемый console lifetime, хранение/освобождение delegate и явное поведение нескольких app instances.

[DiagnosticObserver](../../TrueMoon/Diagnostics/DiagnosticObserver.cs) теряет IDisposable от каждой value.Subscribe. DiagnosticSubscription освобождает только AllListeners subscription; EventsSource не освобождает собственный DiagnosticListener. Предложение: app-owned набор всех handles с cleanup при shutdown/build failure. LoggingObserver вызывает listeners без изоляции исключений: diagnostic listener способен прервать app operation. Определить error policy для callbacks.

**P1 — configuration APIs (CORE2).** [ConfigurationBuilder](../../TrueMoon/Configuration/ConfigurationBuilder.cs) игнорирует AddProvider/RemoveProvider и создаёт фиксированный набор. [JsonConfigurationSection](../../TrueMoon/Configuration/JsonConfigurationSection.cs) бросает NotImplementedException для всех операций чтения/записи. Предложение: реализовать минимальный заявленный набор providers либо явно ограничить доступный API; определить precedence и invalid-value errors до расширения.

Текущие способы доступа расходятся: ConfigurationExtensions.Get без section просматривает все секции; indexer/Exist/GetName обращаются к default; CommonConfiguration.GetSection берёт первую секцию с данным именем. Args/environment хранят строки, ConfigurableBase.Get<int> не преобразует их. Refresh обновляет список секций, но args/environment snapshot получен в constructor. Дополнительные дефекты по коду: Split('=') теряет хвост `token=a=b`; PathResolver передаёт в GetFullPath весь `-root=...`, а GetInfo возвращает processName вместо вычисленного productName; GetOrCreate с default key читает typeof(T).Name, но при создании передаёт null в Set. Отдельные поведенческие проверки этих случаев не запускались.

**P2 — API и зависимости после исправления поведения.** Сохранить разделение Contracts/bootstrap/utilities и отсутствие UI зависимости в Core. Выбор resolver уже есть через App.Builder/UseDI; перед перемещением Cobalt default в отдельный composition layer проверить package consumers. Для обычного hosting можно отдельно оценить adapter к [.NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host), который объединяет lifecycle, DI, configuration и logging. Это возможная интеграция, не решение заменить TrueMoon или добавить новую зависимость сейчас. Typed configurator вместо reflection Configure и XML docs полезны после фиксации contracts. Startup/allocations оптимизировать после измерений.

### Обзор четырёх существующих тестов

Использованы run-tests и test-anti-patterns с .NET extension; [AppTests](../../TrueMoon.Tests/AppTests.cs) не переписывались. Class-level fixtures/resources отсутствуют.

| Тест | Oracle и текущий результат | Оценка |
| --- | --- | --- |
| AppCreate | Тип CommonStartableService и IsStarted; Failed до assertions на resolution | Полезный smoke с проверяемым состоянием; отсутствие Stop assertion — отдельный пробел |
| Create | App not null, Name not empty; Passed | Корректный ограниченный smoke; не проверяет precedence/конкретное имя |
| RunAsync | await вызова и Assert.True(true); Passed | T1: Always-true assertion, не проверяет lifecycle outcome |
| RunAsyncWithConfigurator | await вызова и Assert.True(true); Passed | Тот же T1, отдельный reflection entry point |

Findings: **1 Critical (T1, два теста)**, **1 High (T2, общий helper двух тестов)**; Medium/Low findings не выделялись.

- **T1:** заменить константный assert наблюдаемым результатом runner. Предлагаемый oracle после принятия CORE1: trace строго `Start → Stopping → Stop → Stopped → Dispose`, каждое событие один раз; при failure сохранить исходный exception instance, остановить только реально запущенные сервисы, подтвердить disposal. Это будущие критерии, а не результаты текущего запуска.
- **T2:** LifeTimeExecutor.StartAsync запускает неотслеживаемый Task.Run с Task.Delay(500) и lifetime.Cancel. Нельзя дождаться фоновой операции и проверить её ошибку. Заменить fixed-delay coordination управляемым сигналом старта/отмены, хранить и await завершение helper task; проверять ровно один cancellation request. Длительность suite около100ms не доказывает, что scheduled task успела вызвать Cancel.
- Положительные наблюдения: app operations awaited; using/await using присутствуют; AppCreate проверяет тип и observable IsStarted; Create действительно проверяет создание/имя.
- Смежные непроверенные сценарии: startup rollback, external cancellation, stop failure, repeat/concurrent lifecycle, disposal/subscription ownership, оба DI providers. Coverage не собирался; отсутствие dedicated тестов не выдаётся за измеренный процент покрытия.

### Следующая конкретная работа

Сначала устранить загрязнение runtime graph и Composite alias resolution в COB1/COB3, сохранив AppCreate как regression proof. В CORE1 определить наблюдаемый runner/error/cancellation/rollback contract и заменить T1/T2 проверками принятого поведения. Затем закрыть scheduler/pool ownership по CORE4 и providers/diagnostic subscriptions по CORE2. Архитектурное расширение до этих шагов увеличит поверхность неподтверждённого поведения.

## Исправление проблем ядра — 2026-10-10

Запрос: «давай починим все проблемы» из предыдущего ревью. Область: lifecycle/bootstrap, Cobalt/generator/Microsoft DI adapter, configuration/diagnostics, scheduler/pools и необходимые Enerit consumers. Исходники поверх HEAD `9d7090a`, изменения не закоммичены. Существующие пользовательские UI изменения сохранены. Optional Generic Host adapter, новый typed-configurator API и переезд архитектуры не вводились.

### Реализация

- App и IApp.RunAsync используют один AppRunner. Ошибки больше не маскируются; startup/cleanup exceptions сохраняются, rollback/normal shutdown выполняются в reverse order. Shutdown token отдельный, timeout30s кооперативный. DefaultApp сериализует операции, повторные calls идемпотентны, restart запрещён; owned provider освобождается один раз, concurrent и recursive disposal не ждут сами себя.
- Console handler устанавливается только на работающем lifecycle, ставит Cancel=true и снимается после stop/failure/dispose. DefaultAppLifetime выполняет snapshots callbacks вне lock, завершает wait асинхронно, освобождает owned CTS и сохраняет borrowed. Последний review обнаружил Cancel/Dispose race: CTS теперь освобождается после активных callbacks; external borrowed source cancellation освобождает waiters.
- Configuration providers действительно add/remove; priority, reads и conversion согласованы. JSON sections читаются/сохраняются, failed refresh сохраняет snapshot. Исправлены args equals-tail/executable exclusion, root path/product metadata и default GetOrCreate key. Diagnostic subscriptions/factory созданы лениво и принадлежат resolver; individual handles и sources освобождаются, observers не ломают app.
- Scheduler больше не spin-wait, закрывает приём/drain и завершает workers. Writer защищает Dispose/use-after и copy только written region. Memory rentals имеют отдельного manager-owner, проверяют whole-region identity, отвергают foreign/sliced/double/stale return; добавлен IMemoryOwner API. Array extraction compatibility и обязательное прекращение работы с ранее полученными spans/pins описаны в PLAN.
- Cobalt exact registration metadata, aliases/generics, singleton/thread-safety и owned/borrowed disposal исправлены; подробности принадлежат [его HISTORY](../cobalt/HISTORY.md). Microsoft DI wrapper владеет provider, но стандартный многократный disposal disposable aliases сохраняется.
- Enerit переведён на tracked rental, собственный scheduler/connection cleanup и async handler drain. Partial header/EOF и pending-response failures исправлены без изменения framing; [его20 новых regression cases](../enerit/HISTORY.md#совместимость-pools-и-завершение-pipes--2026-10-10).
- В Directory.Packages.props удалены lower-version дубликаты Roslyn CSharp/Analyzers: effective5.9.0 сохранена. Repository NuGet.Config задаёт один публичный source и mapping, не меняя global user config. После исправления NU1506/NU1507 в полном build отсутствуют. Проверка выполнена с .NET best practices, run-tests, code-testing-agent, find-untested-sources, assertion-quality/test-gap-analysis и msbuild-antipatterns skills.

### Итоговая валидация

Windows, Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Сначала direct Core run78/78 и Cobalt37/37+generator6/6; затем окончательные команды после всех правок:

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --no-build --no-restore --logger trx --results-directory TestResults/CoreFixes-Final-20261010 --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --no-build --no-restore --list-tests --verbosity quiet
```

Все три команды exit0. Build: **0errors/1271warnings**, не warning-free. Tests: **643passed/0failed/0skipped**. TRX counters сверены по проектам:

| Проект | Discovered/Total | Passed | Failed | Skipped |
| --- | --- | --- | --- | --- |
| TrueMoon.Tests | 81 | 81 | 0 | 0 |
| TrueMoon.Cobalt.Tests | 37 | 37 | 0 | 0 |
| TrueMoon.Cobalt.Generator.Tests | 6 | 6 | 0 | 0 |
| TrueMoon.Enerit.Tests | 30 | 30 | 0 | 0 |
| TrueMoon.Enerit.Generator.Tests | 9 | 9 | 0 | 0 |
| TrueMoon.Alloy.Tests | 480 | 480 | 0 | 0 |
| TrueMoon.Mithril.Tests | 0 | 0 | 0 | 0 |

Mithril zero discovery — отсутствие executable тестов, а не successful lifecycle suite. Alloy480 проверяет unit scenarios на уже существующих UI изменениях; physical/native/soak результаты этим run не меняются. Core81 распределены: AppTests5, AppLifecycle23, AppLifetime12, Configuration11, Diagnostics8, CoreUtility22.

Ignored logs: `TestResults-CoreFixes-solution-build-final-20261010.log`, `TestResults-CoreFixes-solution-tests-final-20261010.log`, `TestResults-CoreFixes-discovery-final-20261010.log`. TRX: `TestResults/CoreFixes-Final-20261010/`, семь файлов с timestamps16:58:56–16:58:58 local. Intermediate failing compile/assertion attempts сохранены отдельно и не считаются final passing evidence: исправлены unconstrained nullable override, synchronous Throws overloads и exact JsonException assertion для derived JsonReaderException.

### Requirement → Evidence

| Requirement | Evidence |
| --- | --- |
| «давай починим все проблемы» — L1 runner/rollback/error/cancellation | `AppLifecycleTests.StartupFailure_RollsBackReverseOrderAndPreservesOriginalError`, `StartupAndCleanupFailures_PreserveEveryErrorAndContinueCleanup`, `ExternalCancellation_StopsWithIndependentTokenAndDisposes`, `InstanceRun_UsesSameShutdownOrderAsBuilderRun` — exact trace, original exception identity и dispose count на обоих providers |
| «давай починим все проблемы» — L2 repeat/subscription/lifetime ownership | `ConcurrentStartStopDispose_AreIdempotentAndRestartIsRejected`, `StoppedAndDisposedApplication_IsCollectibleAfterConsoleUnsubscription`, `AppLifetimeTests.Cancel_DoesNotRunWaitContinuationsInline`, `ConcurrentDispose_DefersSourceDisposalUntilCancellationCallbacksFinish`, `BorrowedSourceCancellation_ReleasesLifetimeWaiters` |
| «давай починим все проблемы» — D1 graph/aliases/containers | `AppTests.AppCreate`, `ResolverRegressionTests.RuntimeGraph_SelectsImplementationAndLifetime_WithoutAssemblyPollution`, `Composite_AllAliasesShareOneOwnedInstance`, `TypeContainer_InitializesAllFactoriesOnce_WhenSingleAndMultipleResolutionMix`; полный Cobalt matrix в его HISTORY |
| «давай починим все проблемы» — C1 providers/JSON/paths/conversion | `ConfigurationTests.BuilderAddsProvidersByPriorityAndRemovesByIdentityOrType`, `NamedAndUnqualifiedReadsUseTheSameHighestExistingKey`, `ValuesConvertUsingInvariantCultureAndPreserveMissingNullAndInvalidDistinctions`, `JsonInvalidFilesSurfaceErrorsAndFailedRefreshKeepsLastValidSnapshot`, `PathResolverParsesExactRootArgumentAndUsesProductMetadata` |
| «давай починим все проблемы» — G1 diagnostics | `DiagnosticsTests.DisposingSubscriptionDetachesExistingAndFutureSources`, `ListenerExceptionsDoNotStopDeliveryAndSubscriptionsRespectFilters`, `AppCreationFailureDisposesResolverOwnedSubscriptions`, `AppDiagnosticsAreObservedAndDisposedEvenWhenAListenerThrows` |
| «давай починим все проблемы» — U1 scheduler | `CoreUtilityTests.SchedulerDisposeDrainsAcceptedWorkAndTerminatesWorkers`, `SchedulerCancellationDrainsQueueAndRejectsNewTasks`, `SchedulerExposesPendingTasksAndRefusesForeignInlineExecution`, `SchedulerAllowsWorkerInlineExecutionAndSelfDisposalWithoutDeadlock` |
| «давай починим все проблемы» — U2 writer/rental/consumer ownership | `BufferWriterGrowsPreservingOnlyWrittenRegionAndReturnsOldBuffer`, `BufferWriterDisposeReturnsOnceClearsReferencesAndGuardsEveryMember`, `MemoryRentalRejectsForeignAndSlicedMemoryWithoutReturningWholeRental`, `MemoryOwnerDisposalAndReturnShareOwnershipAndStaleRentalCannotReturnNewLease`, `MemoryConcurrentReturnsHaveExactlyOneWinner`; Enerit `UtilityLifetimeTests.ConnectionDisposeWaitsAsyncHandlersAndReturnsTheirRequestMemory` |

### Assertion и static gap review

Новые/переписанные тесты перечитаны против исходников: exact lifecycle order и single disposal, отсутствие rollback незапущенных сервисов, original error identity/aggregate contents, live independent shutdown token, отсутствие retained console roots; configuration exact winners/shadowing/invalid/missing, реальная persisted JSON запись и snapshot retention; subscription delivery после individual cleanup и observer failure; scheduler accepted-work drain/foreign inline/worker identity; writer sentinel отличает copy-written от copy-capacity; rental stale lease не может вернуть новую rental. Старые Assert.True(true) удалены, delayed fire-and-forget helper заменён observable synchronous lifetime cancellation.

В bounded новых regression files нет assertion-free/always-true/self-field tests; identity assertions сравнивают независимо разрешённые объекты/известные исключения. Assertions охватывают equality, state/side effects, collections/deep sequence, negative, type/identity и errors. TCS/manual barriers вместо arbitrary sleep; timeout guards ограничивают зависший тест. Static pseudo-mutations remove-cleanup/error propagation/priority/ownership/queue close/EOF checks имеют наблюдаемый oracle. Mutations не выполнялись, empirical mutation score и line/branch coverage не заявляются.

Ограничения: cooperative user Task shutdown, standard Microsoft DI alias/error cleanup, disk/permission fault injection, multi-process JSON writes, pointer-level pinning/active-pin return behavior, independent package consumers и Release. Полный IPC/units/native UI matrix принадлежит другим этапам; исправление известных Core дефектов не закрывает G3/G4/G6.

Финальная документальная сверка:13 изменённых planning/status/history документов,113 local links, missing files/anchors0. `git -c core.safecrlf=false diff --check` exit0; удалена лишняя пустая строка JSON section EOF без semantic changes. Scratch research/plan/status/pairing files отсутствуют в working-tree change list. Root assertion inventory:65 named methods/320 static Assert call sites/0 assertion-free в6 Core test files; theory/loop executions не входят в эту метрику.

## CORE5 / COB4 headless package consumer — 2026-10-11

Реализован [воспроизводимый consumer](../../ManualTests/CorePackageConsumer/README.md)
с собственными исходниками и только PackageReference, вне solution. Runtime C# и
lifecycle/provider contracts не менялись. Пять package projects получили конкретные
Description и самостоятельные PACKAGE.md: TrueMoon, Contracts, Cobalt, generator,
Microsoft DI adapter. Общая версия и net10.0 сохранены, публикации пакетов не было.

Generator доставляется **явным** TrueMoon.Cobalt.Generator reference с PrivateAssets=all;
runtime nuspec не содержит его автоматической transitive зависимости. Это принятое
правило поставки, не исправление analyzer runtime. Actual compiler analyzer path/SHA256,
consumer-generated ModuleInitializer и resolution проверены на NuGet, отдельно от
ProjectReference unit tests. Microsoft DI consumer дополнительно собирается без generator.

### Итоговый запуск

Windows x64 build26300, SDK10.0.401/runtime.NET10.0.12, net10.0, framework-dependent
Debug/Release build/publish без RID. Свежая local version:
`0.2.2.7-alpha.headless.20261011142810069`. Generator DLL SHA256:
`AB7D23337C3866C6877264F92D825A961DA789FB450D1F3077F42FF8867C5288`.
Все пять TrueMoon packages получены из нового local feed в isolated cache; source
mapping и .nupkg.metadata сверены. Consumer assets только package, без UI/Skia/Silk,
IPC или units. Runtime output не содержит generator/Roslyn DLL.

```powershell
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/CorePackages/final-consumer

dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --no-restore --logger "trx;LogFileName=TrueMoon.Tests.trx" --results-directory TestResults/CorePackages/tests --verbosity quiet
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --no-restore --logger "trx;LogFileName=TrueMoon.Cobalt.Tests.trx" --results-directory TestResults/CorePackages/tests --verbosity quiet
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --no-restore --logger "trx;LogFileName=TrueMoon.Cobalt.Generator.Tests.trx" --results-directory TestResults/CorePackages/tests --verbosity quiet
```

Повторять runner в **новом** каталоге; final-consumer уже содержит evidence и не
перезаписывается. Три component test commands завершились exit0, xUnit2.9.3/VSTest:
Core81/81, Cobalt37/37, generator6/6, Failed0/Skipped0; TRX totals совпадают.

| Проверка | Фактический результат |
| --- | --- |
| App + explicit generator, оба providers | 40/40 processes: 5scenarios ×2providers ×Debug/Release ×build/publish |
| Direct Cobalt без App/adapter | 4/4 processes, Composite/open generic/transient/borrowed cleanup |
| Microsoft DI без consumer generator | 20/20 processes, те же5scenarios ×Debug/Release ×build/publish |
| Generation | По8headless/6direct generated sources на configuration; 4 actual compiler path/hash checks; marker в consumer assembly и автоматически опубликованный graph |
| Consumer compilation/delivery | 6builds/6publishes exit0/0warnings/0errors |
| Delivered README | 3точных C# snippets извлечены из packaged PACKAGE.md, 3Release builds +3processes exit0/0warnings/0errors; примеры вручную не адаптировались |
| Missing generator control | Fresh isolated consumer без generator, compile-time marker сохранён; expected build exit1/CS0246 RHeadlessConsumer. Это отрицательный контроль, не passing build |
| Runner | exit0, summary.Completed=true, Cases64/ReadmeExamples3, Failure=null |

Сценарии: normal lifetime.Cancel; external cancellation с исходным token;
startup failure с rollback только worker; stop failure с продолжением worker cleanup;
build failure с resolver-owned source/subscription/factory cleanup. В normal/cancel/
stop-failure lifecycle order worker.Start→gate.Start→Stopping→gate.Stop→worker.Stop→Stopped;
startup failure worker.Start→gate.Start→worker.Stop→Stopping→Stopped, build failure
не запускает сервисы. Исходные exception identities сохранены (build может иметь wrapper).
Отдельный shutdown token остаётся cancelable и не canceled после отмены run token.

Composite Worker/aliases и lifetime aliases shared; open Repository<int>/closed
Repository<string> singleton возвращают Int32:17/String:17 из приоритетного provider,
app name write/read совпадают. Runtime factory singleton shared; две Ticket transient
instances различны и освобождены. Factory1/Gate1/Ticket2/WorkerResource1 releases,
borrowed instance0. Cobalt Worker.Dispose calls1, Microsoft DI calls3 в normal graph;
Worker идемпотентен и собственный ресурс освобождает один раз. Это сохранённое
provider difference, а не обещание reference-unique Microsoft DI cleanup.

Observer failure изолирован; framework и consumer diagnostics наблюдались.
После shutdown/build failure fresh DiagnosticListener не enabled, events не
доставляются, retained owned source отклоняет операции как disposed. Нет UI,
clipboard, system settings или чужих процессов; runner ограничивает только
свои children timeout и завершает только свои зависшие процессы.

Pack exit0 во всех5проектах, **535 warning entries** в pack logs (31/147/177/75/105),
не zero-warning source build и не535уникальных проблем. Shared XML/RS1041 и прочие
warnings не исправлялись. Полная solution сборка/другие suites не запускались.

### Промежуточные попытки, артефакты и ограничения

Первый runner `first-consumer`: все64runtime processes прошли, omitted-generator
build верно отказал CS0246, но verifier ожидал только CS0234 и завершился exit1.
Classifier исправлен на обе формы отсутствующего namespace; весь итоговый runner
повторён с новой версией/feed/cache. First summary.Completed=false сохранён и не
считается итоговым passing run. Позднее добавлена проверка примеров из actual package
README; она входит в final-consumer. Одна локальная reporting command имела PowerShell
foreach/pipeline parse error; исправленная сводка сверила все actual reports/TRX.

Ignored TestResults/CorePackages содержит оба runs, feed/package SHA256, isolated
cache, source copies/SHA256, compiler analyzer list/generated source hashes,
build/publish output, process reports/logs, negative control и summary; tests/TRX
отдельно. После executable proof менялась только навигационная документация/статусы.

Не проверялись другие OS/SDK/compiler hosts, физический Ctrl+C/kill/parent exit,
RID/self-contained/trimming/single-file/NativeAOT, owned disposal failure matrix
в отдельном package consumer, disk/JSON fault injection и performance. Component
coverage этих контрактов не выдаётся за более широкий package run. UI K7/physical
DPI/real loss/VRAM, Enerit/Mithril own-process и общий выпуск остаются открыты.
CORE5/COB4 выполнен в указанном scope; локально далее COB5 baseline фиксированного
graph, общий функциональный приоритет — MIT0 bounded own-child, затем ENE1/ENE2.

Final static verification: 31 changed/new text files без NUL/conflict markers,
source final newline, XML/PowerShell syntax valid. 34documents/374local links/85heading
links без ошибок; git diff --check exit0. Все123 recorded source fingerprints и
5package SHA256 совпадают; все64 child reports Completed=true. TestResults ignored.

## Diagnostics test isolation при MIT4 — 2026-10-14

Во время [MIT4/ENE4 regression](../mithril/HISTORY.md#mit4ene4-app-и-generated-pipes--2026-10-14)
TrueMoon.Tests Debug сначала дал80passed/1failed/0skipped.
AppCreationFailureDisposesResolverOwnedSubscriptions получил IAppLifetime.Cancel
от параллельного теста через default TrueMoon filters. Исправлена только проверка:
ConcurrentBag собирает unique eventName собственного DiagnosticListener probe,
сохраняются original exception, IsEnabled=false и Assert.Empty для post-failure
события. Production Core без изменений. Final Debug81/81 и Release81/81,
Failed0/Skipped0, Windows x64/net10.0/SDK10.0.402. Команды/TRX в общем integration
журнале. Packages/headless64+3/UI этой regression не повторены.

## Package regression MIT5/ENE5 — 2026-10-14

[Общий package журнал/команды](../mithril/HISTORY.md#mit5ene5-independent-package-consumer--2026-10-14).
Windows x64/net10.0/SDK10.0.402: independent App/IPC/units consumer108/108,
Cobalt/Microsoft DI включая DI без Cobalt analyzer. Actual packaged dependencies/
compiler path/SHA и no-runtime-analyzer delivery подтверждены. Related source
regressions Core81/81, Cobalt37/37/generator6/6 в Debug/Release, Failed0/Skipped0.
Core получил только package Description; Cobalt/DI adapter C# runtime без изменений.
Headless64+3/COB5 performance baseline не повторены. Full solution Debug nonincremental
exit0/errors0/warnings1227; другие UI/native checks не запускались.

<a id="status-before-planning-revision-2026-11-05"></a>
## Архив STATUS перед ревизией — 2026-11-05

Полный снимок до ревизии очереди после db5ea97. Даты и scope внутри принадлежат прежним работам; это не новые запуски. SHA256 исходного текста: `355E2C0E7EE71DD82ED7A4402E5FC833C2811CD31129611E3B564B2A4EAB0CC4`.

<details>
<summary>Прежний STATUS целиком</summary>

# Core: состояние и точка продолжения

Последняя работа — **2026-10-30, bounded JSONL archive/configuration views для ED8a**.
App предоставляет ещё два borrowed aliases без нового owner: bounded async чтение
recognized history и frozen path-free scalar configuration. Чтение не flush-ит
sinks, consumer отменяет/drain свою задачу. Core398/398 отдельно Debug/Release,
failed/skipped0; narrow56/56, Cobalt/MS DI и blocked-sink/current-run route проверены.
[Контракт и evidence](HISTORY.md#архив-jsonl-и-конфигурация-для-ed8a--2026-10-30).
Published Release/Cobalt Editor consumer проверен отдельно с normal/crashed CPU
preview и rotating/malformed history. Full solution/NuGet/hardware не повторялись.
Далее ED8a.2 child health/device/acceptance у engine; полный CORE6 и App-private
routing остаются отдельными задачами.

Предыдущая работа — **2026-10-29, borrowed health и bounded output windows для ED6a.2**.
`UseDiagnostics(options)` даёт nondisposable `IDiagnosticHealthReader`; cached
ring/sink/emergency/retention counters читаются без IO/flush/wait. `Read(maxChars)`/
snapshot.Limit отделяют display omission от capture/file loss, сохраняют старый API.
Core387/387 Debug/Release, failed/skipped0; blocked sink/no-drain, sink failure,
Cobalt/MS DI ownership и UTF16/window boundaries проверены. Aether1556/Editor823/
Hosting117 Debug/Release и один native SourceDebug/Cobalt consumer проверены отдельно.
[История и evidence](HISTORY.md#borrowed-health-и-output-windows--2026-10-29).
Core/App-private routing, общий disk quota и physical/release gates этим не закрыты.

Предыдущая работа — **2026-10-29, file-drain вне shared ThreadPool**.
ProcessOutputCapture использует dedicated file worker и прямой sync wake-up,
сохраняет queue8/UTF8/tail/file budgets/borrowing/error accounting. No-file mode
без worker; один background worker на active file capture — цена решения.
Controlled RED→GREEN: EOF/24bytes уже завершены, shared worker удерживается,
FileCompletion и Completion завершаются за прежние2s. Pool settings меняет только
Core-only child fixture. Core374/374 Debug/Release, Aether1550/1550 Debug/Release,
4000file controls, fresh headless8/8 и native24/24 прошли.
[History/точный тест/границы](HISTORY.md#file-drain-без-shared-threadpool--2026-10-29),
[audit/current TRX](../../TestResults/ARCH2e4-FileDrain-Native-20261029-1/audit.json).
Исторический intermittent episode не имеет failure heap; его точная причина не
объявляется установленной. Исправлена доказанная pool dependency; arbitrary FS
stall/global real-time guarantees не добавлены. App/DI contracts и full solution
scope не менялись. Следующий engine consumer — ED6a/ED8a console/export.

Историческое планирование — **2026-10-27, ARCH2 пакетный Editor/host**.
Текущих App/IFactory contracts достаточно для начала; новые Core APIs не назначены.
Порядок работ изменён: [ARCH2](../engine/PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27)
предшествует console/export. Core code/API не менялись; build/tests не запускались.

Предыдущая реализация и проверка — **2026-10-27, bounded history reader/importer для child logs**.
UseDiagnostics(pipeline) предоставляет borrowed nondisposable reader/importer views;
импорт назначает host identity/sequence, сохраняет child Origin/PID/time/trace,
применяет прежние filters/budgets и не re-emits global listeners.
Legacy overload остаётся без delivery views.
[API](DIAGNOSTICS.md#чтение-и-импорт-bounded-history),
[подробные проверки](HISTORY.md#чтение-и-импорт-child-diagnostics--2026-10-27).

Новые59cases; Core373/373 Debug/Release, failed/skipped0. Full solution4285/4285
в каждой конфигурации; builds0errors, Debug nonincremental1546warnings,
Release incremental1316warnings, SDK10.0.403.
[Evidence](../../TestResults/DiagnosticsDelivery-20261027-1/verification.json).
Ошибка изоляции нового App-теста в общем запуске исправлена в тесте; global routing
не менялся. Исторические CPU FrameReceiver/Mithril races остаются открытыми.
Aether отдельно проверил3actual Preview children и control/log isolation;
published/GPU/package/performance acceptance не повторены.

CORE6 остаётся частичным: после ARCH2 — Editor console/export и domain/frame metrics.
Bootstrap-before-pipeline, App-private routing, generator и общий quota report/
artifact roots отдельны. [Child transport](../engine/DIAGNOSTICS_DELIVERY.md)
принадлежит Aether; предыдущие [operations/telemetry](HISTORY.md#операции-и-трассы-диагностики--2026-10-26),
[explicit parent/kind](HISTORY.md#explicit-parent-и-kind-для-ipc--2026-10-26)
и JSONL/emergency budgets сохранены.

Связанная проверка — **2026-10-23, ENE6a/MIT6a integration**: Core117/117
Debug/Release в текущем полном solution2361/configuration; new App/IPC lifecycle
scenarios и fresh package132 прошли на Cobalt/DI. Core runtime/API не менялись;
собственная headless delivery/performance матрица повторно не запускалась.
[Команды и scope](../enerit/HISTORY.md#ene6a-finite-admission-и-наблюдаемая-остановка--2026-10-23).

Последняя работа — **2026-10-23, сверка предпосылок Aether по планам и исходникам**.
Новых Core blockers для локального ARCH1b не выявлено; текущие App/factory/lifecycle
contracts достаточны в прежнем bounded scope. Неограниченный scheduler сам по себе
не требует переписывания Core: admission для IPC предложен на границе Enerit.
[Общая сверка и зависимости](../PLAN.md#сверка-основы-перед-aether--2026-10-23).
Код не менялся, build/tests/consumers не запускались; прежние ограничения сохраняются.

Текущая реализация — **2026-10-22, общие фабрики/lifecycle adapters**:
IFactory<T,TArguments>, Factory adapters и non-owning AppServiceLifecycle добавлены
в Contracts без provider/module dependencies. Legacy IFactory<T> сохранён.
Core117/117 Debug/Release (10новых cases), включая обе DI реализации и exactly-once
product ownership. Подробности в [Aether DI evidence](../engine/HISTORY.md#единые-фабрики-и-di--2026-10-22).
Прежняя Core delivery matrix не повторялась целиком; свежие bounded package checks
этого изменения перечислены там же.

Документационное решение — **2026-10-22**: правила ИИ-агентов закрепляют сборку
систем/сервисов через Cobalt и внедрённые IFactory, constructor injection и явное
владение результатами фабрик. App остаётся точкой composition/lifecycle, общие
contracts независимы от provider. [Границы решения](../cobalt/PLAN.md#организация-систем-и-сервисов-через-di-и-ifactory--2026-10-22).
Код/API не менялись; аудит consumers и build/tests не запускались. Следующие
адресные изменения следуют правилу, прежние readiness и порядок работ сохраняются.

Исправление структуры — **2026-10-21**: AppCreationException находится в корне
Contracts/namespace TrueMoon; UnitsConfigurationExtensions перенесён в Mithril.
Роль общей сборки contracts/base models/interfaces сохранена, новых dependencies
нет. Shared consumer RootNamespace заданы явно. Core107/107 Debug/Release;
свежие package consumers80cases и3README examples прошли. Public namespace/assembly
изменения требуют обновления using и пересборки внешних consumers.
[Миграция и точные команды](../HISTORY.md#исправление-структуры-проектов--2026-10-21).

App entry-point audit — **2026-10-19**: bootstrap/configuration и UseDiagnostics
подключены к App; Contracts/Core utilities не требуют отдельного UseX.
Focused Debug AppTests/AppLifecycleTests/DiagnosticsTests:46passed/failed0/skipped0.
Runtime code не менялся; прочие Core suites/Release/consumers не повторялись.
[Общая таблица API и команды](../HISTORY.md#аудит-точек-подключения-модулей-к-app--2026-10-19).

Integration regression — **2026-10-19, UseAether**: Core production API не менялся.
Полный Debug повторил Core107/107; новые Aether integration tests и независимые
package consumers проверили App startup/stop/rollback и sync/async disposal на
Cobalt и Microsoft DI. Это engine integration scope; прежние Core Release/source/
package матрицы ниже не повторялись.
[Подробные команды/evidence](../engine/HISTORY.md#app-integration-реализация--2026-10-19).

Package metadata check — **2026-10-19**: TrueMoon/Contracts/Core наследуют общие
project/repository/license/icon metadata; Core получил собственный PACKAGE.md.
Release pack и nuspec/payload checks прошли в общей matrix; runtime code и own
unit/native consumers не менялись/не запускались в этом metadata шаге.
[Общие команды/evidence](../HISTORY.md#nuget-package-metadata--2026-10-19).

Документационная сверка2026-10-15: [общая quality matrix](../QUALITY.md)
определяет следующий scope — UI package/Hosting после текущих Core/Cobalt changes.
Новых code/build/tests/package запусков в этой сверке нет.

Последняя работа — **2026-10-15, сквозной headless quality proof**.
Одни consumer sources проверены через ProjectReference и независимые свежие
PackageReference: configuration/JSON, diagnostics, App lifecycle/Cobalt и реальный
эксклюзивный FileStream, включая cancel/start/stop/build/config/cleanup failures.
**Согласованный bounded Core quality проход завершён** на указанной конфигурации;
новых public APIs/runtime fixes этим этапом не потребовалось.
[Контракт](PLAN.md#проход-качества-до-engineeditor--2026-10-15),
[runner](../../ManualTests/CoreHeadlessProbe/README.md),
[команды, matrix и границы](HISTORY.md#сквозной-headless-proof--2026-10-15).

**Текущие проверки:** Windows x64/net10.0, SDK10.0.402, xUnit2.9.3/VSTest.
TrueMoon.Tests: **107 passed / 0 failed / 0 skipped в каждой Debug/Release**;
два final TRX,214passed суммарно. Source52processes, package80 +3README, оба
summary Completed=true;130/127source fingerprints совпали, opt-out expected CS0246.
Full nonincremental Debug solution build exit0/errors0/warnings1183.
Microsoft DI:6compatibility scenarios; Cobalt:7, включая startup+Dispose failure
с сохранением обеих причин. Матрица Debug/Release × build/publish, без RID.

Предыдущие этапы2026-10-15: [configuration/bootstrap](HISTORY.md#configuration-и-bootstrap-quality--2026-10-15)
и [diagnostics](HISTORY.md#diagnostics-quality--2026-10-15). Их исправления сохраняют
текущую regression107cases; новые unit tests в headless этапе не добавлялись.

## Реализация и проверки

| Область | Реализация | Проверки2026-10-15 |
| --- | --- | --- |
| Bootstrap/lifecycle / CORE1 | Runner, reverse rollback/stop, cooperative shutdown token, повторные операции, owned disposal | AppLifecycleTests23 + AppTests5; regression Debug/Release |
| Lifetime | Snapshot callbacks вне lock, asynchronous wait continuations, owned/borrowed CTS | AppLifetimeTests12; regression Debug/Release |
| Configuration / CORE2 | Приоритеты, conversion, null/invalid, args/env/JSON refresh; исправлен GetOrCreate в предыдущем этапе | ConfigurationTests27; regression Debug/Release |
| Bootstrap/configuration | Actions до registrations/factories, единый IConfiguration у callbacks/App/DI; validation failure сохраняет cause и не начинает registrations | Две theory по Cobalt/Microsoft DI,4cases; входят в27 configuration cases |
| Diagnostics / CORE2 | Completed source retirement, late handle cleanup, повторный setup без старых registrations, независимое освобождение Apps | DiagnosticsTests18; regression Debug/Release; текущий consumer подтверждает повторный setup/events/cleanup |
| Utilities / CORE4 | Scheduler drain/affinity, writer guards, validated rental ownership | CoreUtilityTests22; regression Debug/Release |
| Headless/App proof | JSON persistence/failed refresh recovery, lifecycle/errors/token identity, реальный file ownership и owned/transient/borrowed cleanup | Source52cases: Cobalt28/Microsoft24, Debug/Release build/publish |
| Package / CORE5 + COB4 | Fresh feed/cache/version, bundled direct/transitive analyzer, opt-out и exact README examples | Package80cases +3README;4nupkgs, analyzer path/SHA/generated initializer и expected CS0246. Library→app/legacy controls не повторены |

JSON failure checks различают serialization error до записи и sharing violation
при открытом read-only handle; после ошибки старое значение/файл доступны,
после освобождения handle запись можно повторить. Failed sync/async refresh не
публикует частично обновлённые providers; recovery и старые section handles проверены.
Обычный Set без имени секции сохраняет default target; invalid GetOrCreate
не изменяет providers. Это текущая regression предыдущего configuration этапа.

Несколько Apps наблюдают global DiagnosticListener по своим prefix filters;
закрытие одной не отключает другую. Same-name источники независимы по identity.
Новые filters действуют при discovery; rebinding прежних sources не добавлен.

Предыдущие результаты Core81, Cobalt comparison/package proof и ранние исправления:
[HISTORY](HISTORY.md), [Cobalt comparison](../cobalt/COMPARISON.md).
Собственные Cobalt/Enerit/Mithril/generator suites, IPC/units, UI/native и benchmarks
текущим проходом не запускались. Independent headless package proof повторён;
старый benchmark graph v1 и его consumer не изменялись/не измерялись. Solution build
не считается passing tests соседних модулей.

## Ограничения

- Shutdown timeout30s кооперативный; user Task, игнорирующий cancellation, может удерживать завершение. Scheduler consumers должны дождаться своих async continuations до закрытия scheduler.
- Microsoft DI сохраняет собственные disposal semantics для aliases и cleanup errors; текущий default — Cobalt, массовая migration отложена.
- GetOrCreate не обещает atomic create при concurrent calls. Null replacement в JSON является синхронной записью и может выбросить файловую ошибку.
- Sharing violation проверена; disk-full/permission injection, crash-safe/atomic file replacement и межпроцессные JSON writes не проверены. Refresh доступен на CommonConfiguration; нового reload API нет.
- Memory из pool может быть manager-backed; partial/foreign/double/stale Return запрещён, старые spans/pins после Return использовать нельзя.
- Linux/macOS, другие SDK/compiler hosts, RID/self-contained/trimming/single-file/NativeAOT и engine workloads не подтверждены.
- Dispose подписки не обещает дождаться callback, уже выполняющегося на другом потоке. Bounded concurrency128sources не является exhaustive race/stress проверкой. Factory удерживает собственные sources до своего Dispose; нового release API нет.
- Собственный headless solution build дал1183warnings; более поздний общий build указан в [общем статусе](../STATUS.md). XML/nullability/analyzer warnings и общий выпуск не закрыты.

## Следующий конкретный шаг

Сначала общий ARCH2 package/host transition; concrete App composition остаётся
в executable, module registrations используют существующие общие contracts.
После ARCH2 для diagnostics направления — Editor console/export через Core reader,
затем domain/frame metrics поверх CORE6d и готовой child delivery lane.
App-private routing и
bootstrap capture требуют самостоятельного контракта; общий quota report roots
не выводится из JSONL retention.

Актуальный прикладной маршрут — [engine STATUS](../engine/STATUS.md#следующий-конкретный-шаг).
ARCH1b.1 и ED1a выполнены в своих границах. Прежний UI package/Hosting quality проход завершён в своём
scope, не остаётся текущим prerequisite. Новые общие Core APIs/utilities вводить
только по потребителю; IPC admission/limits и process cleanup развиваются у
Enerit/Mithril. Общий release/platform scope и перечисленные ограничения открыты.

</details>

<a id="status-before-core75-2026-11-10"></a>
## Полный STATUS перед CORE7.5 — 2026-11-10

# Core: состояние и точка продолжения

**2026-11-10 — CORE7.4 Core/Mithril migration реализована и проверена.**
[Guide](CONFIGURATION_CORE_MITHRIL.md), [общий API](CONFIGURATION_API.md),
[reload](CONFIGURATION_RELOAD.md), [план](PLAN.md#core7-unified-configuration),
[evidence/limits](HISTORY.md#core74-2026-11-10).

## Реализация и проверки

| Область | Текущее состояние |
| --- | --- |
| Core consumers | Standard App:Name/Paths:Root → UseCoreConfiguration; private startup snapshots, legacy-only Name dynamic; common pre-App root parser |
| Diagnostics |33data fields/rules/nested limits → UseConfiguredDiagnostics; resources/borrowed inputs отдельно, DI activation; legacy object-bag только compatibility mirror |
| Mithril bootstrap | Immutable typed launch, original providers до overlays, authoritative env identity group; ordinary profile не подменяет unit/parent/pipe; parent monitor typed |
| Core tests |661/661 Debug и Release, failed/skipped0;42newcases |
| Mithril tests |155/155 Debug и Release, failed/skipped0;44newcases, прежний real-process lifecycle suite повторён |
| Delivery | Source Debug/published Release/fresh6packages по108checks; оба DI,2real owned child/profile, graceful stop/reap; version0.2.5.2-core74.20261110190453 |
| Solution/API | Ordinary incremental Debug0errors/790warnings;4API blocks Release0errors/0warnings; public reflection surface removed0 |

Mithril добавил TrueMoon dependency для общего parser/profile; внешние versions,
TF и общая0.2.5.2-alpha не менялись.1621из1635baseline C#/csproj hashes сохранены;
13C#+1csproj изменены,11production/3tests добавлены. Commands/SHA/restore caveat
в HISTORY. [Прежний STATUS](HISTORY.md#status-before-core74-2026-11-10).

## Ограничения

Core/diagnostics settings StartupOnly, resources не пересоздаются при reload.
Legacy-only apps и callbacks сохранены как explicit compatibility path. Custom
IConfiguration без bootstrap capability предоставляет trusted sections сам.
Aether RuntimeBootstrap/Editor/прочие module consumers ещё ждут CORE7.5.
Нет новых forced callback/source termination, OS/RID/AOT, interprocess writer,
power-loss durability или native/GPU/release guarantees. MIT6 остаётся у Mithril.

## Следующий конкретный шаг

CORE7.5: переход Aether/Editor и других модулей, включая pre-App protected runtime
bootstrap и GPU flags; сохранить captures, identity/version/size checks и ownership.
Детальный scope — Core PLAN и соответствующие module owners. Engine f.4 независим.

<a id="status-before-core76-2026-11-10"></a>
## Полный STATUS перед CORE7.6 — 2026-11-10

# Core: состояние и точка продолжения

**2026-11-10 — CORE7.5 реализован и проверен в configuration scope.**
[Модули и ключи](CONFIGURATION_MODULES.md), [матрица110новых cases](CONFIGURATION_MODULE_TESTS.md),
[команды/результаты/ограничения](HISTORY.md#core75-2026-11-10).

## Реализация и проверки

| Область | Текущее состояние |
| --- | --- |
| Module configuration | Aether/Editor/ECS/render/authoring, Enerit pipes, Nom и Hosting: common schemas/deferred bind/validated copies → existing App/DI owners; StartupOnly |
| Bootstrap/CLI | Frozen runtime parent/control/identity/public flags, child-only validation environment; общий Editor CLI parser/aliases; старые callbacks/overloads сохранены |
| Library boundary | Pure schema/binding utilities в Contracts, старый TrueMoon facade; Editor/Hosting/Nom/Enerit без новой bootstrap dependency |
| Current full suites | Core661, Mithril155, Enerit354, Nom874, Hosting171, Editor1496 каждый Debug/Release, failed/skipped0 |
| Aether scope |42newcases +5old failure replays =47/47 каждый D/R последовательно. Full attempts non-green; см. ниже |
| New cases |110/110 D/R в6новых файлах,49methods |
| Delivery | Source Debug/published Release/fresh16packages по76checks на Cobalt/MS DI; version0.2.5.2-core75.20261110212150; без window/GPU/child runtime |
| API/preservation |7public surfaces removed0;1633/1649baseline hashes сохранены,14целевых изменений,2internal files перенесены; прежние работы сохранены |

Общая версия0.2.5.2-alpha, внешние dependencies/TF не менялись.
[Предыдущий STATUS](HISTORY.md#status-before-core75-2026-11-10).

## Ограничения

Полные Aether attempts не green: initial nested restore/NU1100, один ошибочный
test-environment CPM override/NU1008, concurrent SDK .version exclusive-access
collision и отдельный diagnostics timeout. Serial47/47 D/R прошёл; он не
подменяет чистый full baseline и не закрывает старую reliability причину.
Root props не менялся; offline tests использовали ограниченную clone без obsolete
tool reference. Подробные counts и все attempts сохранены в HISTORY.

Module settings StartupOnly; resources не пересоздаются на reload.
CORE7.4 real-child delivery — предыдущая отдельная проверка; fresh CORE7.5 consumer
проверяет headless module configuration. Нет новых solution/native/GPU/hardware,
OS/RID/AOT, interprocess/durability гарантий. Engine f.4 и Mithril MIT6 отдельно.

## Следующий конкретный шаг

CORE7.6: свести документацию/ключи/совместимость, повторить итоговую поставку и
объединённый независимый App/child consumer на обоих DI. Отдельно получить чистый
последовательный Aether baseline с изоляцией nested fixtures от legacy tool restore.
