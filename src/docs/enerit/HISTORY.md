# Enerit: история работы и проверок

[Текущий статус](STATUS.md), [план](PLAN.md).

## 2026-10-10 — ENE0 baseline и inherited generated mapping

Изолированный worktree: `E:/source/my/TrueMoon-worktrees/enerit/src`, ветка `codex/enerit-next`, исходный HEAD `b15d85f095b6fb1cbe00c4439d11d7bac71e6dc8` (`origin/main`). Общие Contracts/Core/Cobalt/build/docs файлы не менялись; эта запись не подтверждает готовность других модулей.

Окружение: Windows, .NET SDK10.0.401 (`global.json`10.0.100/latestMinor), Debug/net10.0, VSTest с Microsoft.NET.Test.Sdk18.10.1, xUnit2.9.3, VS adapter4.0.1. .NET test skills применены; repository overlay отсутствует. Builds/restore выполнялись стандартным `dotnet test`, dependencies использовали свои worktree-local bin/obj. Игнорируемые артефакты: `TestResults/EneritBaseline/` внутри этого worktree.

### Discovery и исходный baseline

```powershell
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj --configuration Debug --list-tests --verbosity minimal
dotnet test TrueMoon.Enerit.Generator.Tests/TrueMoon.Enerit.Generator.Tests.csproj --configuration Debug --list-tests --verbosity minimal
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj --configuration Debug --no-build --no-restore --blame-hang --blame-hang-timeout 60s --logger 'trx;LogFileName=runtime-baseline.trx' --results-directory TestResults/EneritBaseline --verbosity minimal
dotnet test TrueMoon.Enerit.Generator.Tests/TrueMoon.Enerit.Generator.Tests.csproj --configuration Debug --no-build --no-restore --blame-hang --blame-hang-timeout 60s --logger 'trx;LogFileName=generator-baseline.trx' --results-directory TestResults/EneritBaseline --verbosity minimal
```

Все четыре команды exit0. Runtime discovery10: ArrayBufferWriterTests.Test1–Test5 (5), SerializationExtensionsTests.SerializeTestPoco/DeserializeTestPoco (2), PipesTests.Test0–Test2 (3). Runtime Passed10, Failed0, Skipped0. Generator discovery4: SerializerTests.Serialize/Serialize2/Serialize2_null (3), ServicesGeneratorSnapshotTests.GeneratesCorrectly (1); Passed4, Failed0, Skipped0. Discovery logs: `runtime-discovery.log`, `generator-discovery.log`; run logs/TRX: `runtime-baseline.*`, `generator-baseline.*`.

MMF Test1/Test2 полностью закомментированы: discovery0; это отсутствие проверок, не xUnit skipped. Все три pipe tests в одном процессе; Test0 не содержит assertions, invocation Test1/Test2 проверяют non-null. Все await ограничены внешним VSTest hang timeout60s; timeout не срабатывал. Отдельные процессы и fault/cleanup transport semantics не подтверждены.

### Реальный generator regression и исправление

Существующий snapshot содержит устаревший `UseSignalService`; helper без metadata references возвращает RunResult без assertions/output compilation. Поэтому выбран focused ENE1 шаг для `ServicesGenerator`: компилируемый consumer с `UseInvocationService`, проверка реальных generated client/handler/initializer, diagnostics, method IDs и assembly emit. Общие API не добавлялись.

Первый regression до исправления (3 cases): Passed1, Failed2, Skipped0, exit1. Direct interface компилируется, inherited и diamond падают CS0535: generated client не реализует IBaseService.AddAsync. Evidence: `generator-regression-before.log/.trx`. После первичного включения AllInterfaces: Passed3, Failed0, Skipped0, exit0 (`generator-regression-after.*`).

Дополнительный review case двух независимых base interfaces с одинаковой сигнатурой и разными именами параметров: Passed3, Failed1, Skipped0, exit1 (`generator-duplicate-before.*`). Найдены CS0111 в client и CS0121 в handler. Исправление завершено: общий ordered member list сохраняет declared first, dedup equivalent signatures (return type, arity, static/ref kinds, parameter types/ref kinds), inherited handler dispatch cast к declaring interface. После исправления4 cases: Passed4, Failed0, Skipped0, exit0 (`generator-duplicate-after.*`). Добавлен пятый distinct-overloads case для защиты от чрезмерного dedup.

Focused commands выполнялись по форме:

```powershell
dotnet test TrueMoon.Enerit.Generator.Tests/TrueMoon.Enerit.Generator.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~ServicesGeneratorContractTests' --no-restore --blame-hang --blame-hang-timeout 60s --logger 'trx;LogFileName=generator-regression-before.trx' --results-directory TestResults/EneritBaseline --verbosity minimal
```

Для следующих runs использовались имена `generator-regression-after.trx`, `generator-duplicate-before.trx`, `generator-duplicate-after.trx`. Ни failed regression, ни intermediate passing не заменяет финальный run.

### Итоговые checks

```powershell
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj --configuration Debug --no-restore --blame-hang --blame-hang-timeout 60s --logger 'trx;LogFileName=runtime-final.trx' --results-directory TestResults/EneritBaseline --verbosity minimal
dotnet test TrueMoon.Enerit.Generator.Tests/TrueMoon.Enerit.Generator.Tests.csproj --configuration Debug --no-restore --blame-hang --blame-hang-timeout 60s --logger 'trx;LogFileName=generator-final.trx' --results-directory TestResults/EneritBaseline --verbosity minimal
```

Runtime exit0: **Passed10, Failed0, Skipped0**. Generator final exit0: **Passed9, Failed0, Skipped0**, включая5 новых consumer cases. Evidence: `runtime-final.log/.trx`, `generator-final.log/.trx`. При устранении новых xUnit analyzer warnings был краткий test-source build failure CS1026; исправлен и итоговый generator run после всех правок exit0. Этот неуспешный build не считался test failure или passing run.

| Requirement | Evidence |
| --- | --- |
| «ENE0 runtime+generator baseline» | discovery10/4 и baseline TRX10/4; обе команды exit0 |
| «один ограниченный проверяемый шаг ENE1 по существующему serialization/generated mapping контракту» | ServicesGeneratorContractTests.GeneratedClientAndHandler_ImplementTheCompleteServiceInterface: direct/inherited/diamond/duplicate-signatures/overloads; diagnostics0errors, emit success, expected client/handler IDs |
| «проверь diamond dedup и одинаковые codes client/handler; сохрани declared member IDs» | Тот же theory: diamond case, expected declared EchoAsync:0 и inherited AddAsync:1; direct сохраняет AddAsync:0/EchoAsync:1 |
| «одинаковых inherited signatures из двух независимых base interfaces» | duplicate-signatures case: ровно один generated AddAsync и handler case, compilation/emit успешны |
| «Обновите статусы модулей» | STATUS.md содержит фактические checks, limitations и следующий шаг; этот HISTORY хранит команды/evidence |

Предел256 byte method codes, межверсионная стабильность inherited order, payload compatibility, package delivery и separate-process/cancellation/disconnect/error/cleanup остаются непроверенными. Существующие shared build warnings не устранялись. Native UI checks, solution/Release build и процессы другого worktree не запускались и не останавливались.

## Интеграция в main — 2026-10-10

Коммит агента `6e554e309032671c1858ac8079d6b8465ebc6aff` объединён через merge `0a65e2e`; затем объединён UI `cbae354` через `d56f217a0e918623c8dd4eb2036c813d85c2a5b8`. Проверяемые исходники: main `d56f217`, cwd `E:/source/my/TrueMoon/src`. Windows, Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Новых изменений runtime/generator при интеграции не потребовалось; общие contracts/build/package настройки сохранены.

```powershell
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj --configuration Debug --blame-hang --blame-hang-timeout 60s --logger 'trx;LogFileName=enerit-runtime.trx' --results-directory TestResults/AgentMerge --verbosity minimal
dotnet test TrueMoon.Enerit.Generator.Tests/TrueMoon.Enerit.Generator.Tests.csproj --configuration Debug --blame-hang --blame-hang-timeout 60s --logger 'trx;LogFileName=enerit-generator.trx' --results-directory TestResults/AgentMerge --verbosity minimal
```

Обе команды exit0. Runtime **10passed/0failed/0skipped**, generator **9passed/0failed/0skipped**. Новые build/restore/test runs выполнены после обоих merge, а не скопированы из агентного evidence. TRX counters отдельно прочитаны и совпали с console summaries. Логи/TRX — ignored `TestResults/AgentMerge/enerit-runtime.*` и `enerit-generator.*` в основном checkout. Discovery отдельно не повторялась. Own-process/MMF/fault transport matrix и Enerit Release/package consumer не запускались; ограничения ENE1/ENE2 сохраняются. UI проверки имеют самостоятельную запись и не считаются transport evidence Enerit.

## Совместимость pools и завершение pipes — 2026-10-10

Исправление Core scheduler/memory ownership по запросу «давай починим все проблемы» потребовало обновить Enerit consumers. MemoryPoolUtils rental используется ReadBytes; отрицательная длина/truncated input отвергается до rental. Connection Dispose виртуален, stream опубликован до cancellable connect; client/server закрывают собственные schedulers, pending response waits и connections. Server не ждёт disposal connections под registry lock. Async handler tasks tracked/unwrapped и завершены до закрытия execution scheduler; request memory возвращается в finally. Ошибки header/payload не оставляют response TCS без завершения.

Дополнительные реальные дефекты, найденные при compatibility review: empty success не завершал response, truncated payload мог удалить TCS и оставить caller до50s timeout; registration-after-dispose, failed send/cancellation/disconnect не убирали pending responses. Исправлено синхронизированное принятие новых requests, fault/cancel completion и self-disposing cancellation registrations. Чтение header/payload использует exact read и observable EOF вместо partial header/бесконечного zero-byte loop. Framing не изменён.

### Проверки

Root после финальных edits выполнил full solution build/test/discovery на Windows, Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest: **Enerit runtime30passed/0failed/0skipped**, включая20 новых UtilityLifetimeTests, **generator9passed/0failed/0skipped**. Это новый run; main10/9 из предыдущей записи не перенесён как evidence. Build exit0/errors0; общие1271warnings относятся solution.

[Точные команды и counters](../core/HISTORY.md#исправление-проблем-ядра--2026-10-10). Runtime TRX `TestResults/CoreFixes-Final-20261010/MEGA6_PH-DEV_2026-10-10_16_58_57_net10.0[3].trx`, generator timestamp16:58:58 без суффикса. No-build discovery соответствует30/9.

| Requirement | Evidence в UtilityLifetimeTests |
| --- | --- |
| Tracked whole rental/deserialization validation | `DeserializedBytesHaveTrackedWholeRentalOwnership`, `DeserializedBytesRejectTruncatedAndNegativeLengthBeforeRenting` |
| Owned scheduler/connection shutdown | `ClientDisposeWhileConnectingTerminatesBothOwnedSchedulers`, `ServerConnectionDisposeInterruptsOutstandingConnectAndTerminatesWorkers`, `ServerDisposeTerminatesOwnedSchedulerWithoutClient` |
| Async handler drain и request return | `ConnectionDisposeWaitsAsyncHandlersAndReturnsTheirRequestMemory` |
| Full fragmented headers и EOF | `PipeHeadersReadEntireFragmentedFrame`, `PipeHeadersRejectPrematureEof`, `PipePayloadReadRejectsPrematureEof` |
| Dispose/disconnect/cancel pending cleanup | `ClientDisposeFaultsPendingResponseAndTerminatesWorkers`, `ClientDisconnectFaultsPendingResponseWithoutWaitingForTimeout`, `ClientCancellationRemovesPendingResponseRegistration` |
| Empty/truncated/invalid response | `ClientCompletesEmptyResponseAndDisposalRejectsFurtherInvocations`, `ClientReportsTruncatedResponseInsteadOfLeavingRequestPending`, `ClientReportsInvalidResponseHeader` |

Assertion/static gap review: concrete error types, pending map emptiness, scheduler Completion, post-return memory guard и independent expected byte content. Raw pipe confirms sent request before cancellation/disconnect; controlled fragmented streams дают детерминированные header/EOF cases. Await/barriers вместо fixed sleeps; forced timeout лишь guard теста. Mutants/coverage не запускались.

Ограничения: tests in-process, handler cancellation кооперативна; own-process, memory-mapped faults/cleanup, payload-size policy, complete serialization и package/Release остаются ENE1–ENE3/ENE5. Старые weak pipe и generator smoke tests не переписаны этим ограниченным consumer fix.

## ENE1 ENE2 pipes baseline — 2026-10-11

Принятый [bounded контракт](PLAN.md#ene1-ene2-контракт--2026-10-11) проверен на
Windows x64 build26300/net10.0, SDK10.0.401/runtime10.0.12, xUnit2.9.3/VSTest.
Tracked HEAD e52dd4e; этот run включает незакоммиченные Core/Cobalt/Mithril изменения
предыдущих задач, но не переносит их passing results в Enerit.

### Изменения

- Nullable reference strings больше не генерируют Value/double marker; collection
  element codecs обходятся рекурсивно без bogus List/Dictionary DTO; Memory result
  определяется по symbol; dictionary locals scoped именем поля/параметра.
- TME001 error diagnostics для unsupported payload/service shapes и method count
  >256. DTO ограничены mutable acyclic classes с public parameterless constructor.
  Unknown method/trailing request bytes отвергаются до service invocation, включая
  parameterless methods. Действительные inherited mappings сохранены.
- Frame length до1MiB проверяется перед inbound rental/dispatch; collection count
  до65 536, states/UTF-16/remaining lengths проверяются до decode/allocation.
  WriteFrame сериализует writers, preserving correlation на concurrent calls.
- Server отправляет empty success и remote error даже при пустом writer. Oversized
  result/error преобразуются в bounded error. Void/Task ждут acknowledgement.
- Local cancellation удаляет pending request; late response полностью drained и
  rental returned. Result/cancellation race не теряет выигравший response buffer.
  Disconnect сохраняет исходный IOException subtype; только reader сбрасывает
  соединение и инициирует reconnect. Public endpoint-name overloads сохраняют default
  factories naming и позволяют независимые уникальные endpoints.
- Добавлены SerializationContractTests, PipesProtocolTests, PipesProcessTests,
  PayloadContractTests и executable EneritPipesProbe. Старый mapping test пропускает
  новый default switch section при сравнении method codes, сохраняя consumer emit.
  UtilityLifetimeTests теперь ожидает IOException subtype, invalid byte lengths —
  InvalidDataException. MithrilTest generated interface исключает пять старых
  неподдержанных object/tuple/custom-struct экспериментов; Sender их не вызывал,
  обычные implementation methods сохранены.

### Фактические проверки

Из src, каждый из четырёх test commands exit0; final TRX counters проверены.

```powershell
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=enerit-debug.trx' --results-directory TestResults/EneritPipes/tests --verbosity quiet
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=enerit-release.trx' --results-directory TestResults/EneritPipes/tests --verbosity quiet
dotnet test TrueMoon.Enerit.Generator.Tests/TrueMoon.Enerit.Generator.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=generator-debug.trx' --results-directory TestResults/EneritPipes/tests --verbosity quiet
dotnet test TrueMoon.Enerit.Generator.Tests/TrueMoon.Enerit.Generator.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=generator-release.trx' --results-directory TestResults/EneritPipes/tests --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-restore --no-incremental --verbosity quiet
./ManualTests/EneritPipesProbe/RunPipesSmoke.ps1
```

| Проверка | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| Enerit Debug | 71 | 0 | 0 |
| Enerit Release | 71 | 0 | 0 |
| Generator Debug | 34 | 0 | 0 |
| Generator Release | 34 | 0 | 0 |

Runtime сохранил прежние30 cases плюс41 case; generator — прежние9 плюс25.
Новые19 test methods расширяются до66 cases; process fixture использует реальные
сервисы с transforms, не replay/echo-only fake transport. Hosted process six-case
matrix входит в каждую runtime suite и проверяет dotnet entry assembly launch.

Full non-incremental Debug solution build exit0/errors0/warnings1242. Warning debt
(XML/nullability/analyzers) не устранён этим этапом; build не является test pass
остальных модулей. UI/native/manual MithrilTest не запускались.

Manual runner: **12/12**, Completed=true,
UTC2026-10-11T15:42:42.5851680Z, build/publish и все child runs exit0.
Debug apphost и framework-dependent published Release без RID, шесть сценариев:
payload,32concurrent, cancellation/late response, disconnect, client-dispose,
server-rebind. Все12child exit0/owned handle released, pending0/client workers
completed. Cancellation сохраняет Connections=1/LateResponses=1; same-client rebind
имеет Connections=2 Debug/3 Release: reconnect во время teardown может попасть
в ещё закрывающийся listener, но следующий typed call успешно завершён в deadline.
Сценарии/ownership/точные команды runner — [README](../../ManualTests/EneritPipesProbe/README.md).
Дополнительная сверка:38 документов,402local links/98heading links,0problems;
source SHA256 без расхождений, git diff --check exit0. Assertions/gap review выполнен
по test skills; unsupported/unverified границы оставлены явно открытыми.
Артефакты ignored `TestResults/EneritPipes`: four TRX, build/test logs,
manual/summary.json со source SHA256 и14 командами, process stdout/stderr logs.

### Промежуточные failures и исправления

Первый probe build дал22 errors: nullable string.Value, bogus collection codecs,
dictionary local collision и Memory result extension. Исправлены symbol/type
handling и collection generation; итоговые generated consumer emission и real
process typed values прошли.

Первый runtime run:65passed/2failed — stale payload checkpoint expectation28 вместо
26 и exact IOException expectation вместо EndOfStreamException subtype. После
коррекции contract expectations итог71/71 в обеих конфигурациях.
Первый generator run:24passed/6failed — новый default dispatch section мешал
старому code-mapping test, trailing guard использовал offset без declaration для
parameterless methods. Filter case labels и offset declaration исправлены,
полный emit256-method consumer прошёл; итог34/34.

Первый полный build с TME001 обнаружил два diagnostics в старом MithrilTest:
unsupported record/object payload. Его неиспользуемые unsupported IPC methods
исключены из interface; final solution build errors0. Первый manual run после
same-client reconnect прошёл11/12: необоснованное ожидание ровно одного reconnect
не выдержало legitimate teardown interleaving. Oracle требует фактический
successful next typed call в20s и Connections>=2; итоговая12/12 прошла без изменения
transport для сокрытия ошибки. Пропущенных/отключённых новых tests нет.

### Границы результата

Same-build Windows pipes baseline. Cancellation локальная, failed invocation не
replayed. Native scalar ABI/Roslyn mapping не versioned; void ack/nullable-string
layout несовместимы со старыми peers. Response trailing bytes, partial nested DTO
rental cleanup и backpressure остаются открыты. Cooperative handler shutdown
проверен, игнорирующий cancellation handler может удерживать sync Dispose.
MMF, App/AddUnit/controller/parent-exit/restart, NuGet delivery и performance
нуждаются в собственных proofs. Дальнейший общий шаг — Mithril MIT1/MIT2, затем
MIT3/ENE4 composition.

## ENE4 App registration и lifecycle — 2026-10-14

[Основной integration журнал и точные команды](../mithril/HISTORY.md#mit4ene4-app-и-generated-pipes--2026-10-14)
ведутся один раз у Mithril. Enerit получил typed generated registration factories,
App-local metadata construction, explicit internal pipes factories, endpoint selector
и owned transport Start/Stop wrapper. Existing local service сохраняется, Listen
всё равно запускает server; повторная registration idempotent. Previously resolved
client участвует в startup rollback, что подтверждено до App.Dispose.

Windows x64/net10.0/SDK10.0.402: runtime102/102 и generator34/34 отдельно Debug/Release,
Failed0/Skipped0; App/manual36/36 Cobalt/DI и isolated pipes12/12 Debug/published
Release, Completed=true. Mithril89, Core81, Cobalt37/generator6 также проверены
отдельно в каждой configuration. Non-incremental Debug solution build exit0,
errors0/warnings1226. Финальные process source hashes совпадают, mismatches0.

Wire format этим шагом не изменён. New generated factories требуют runtime
соответствующих overloads; legacy no-factory metadata fallback сохранён.
Same-build/cooperative scope; killed parent client cleanup не наблюдаем. MMF,
independent packages/performance, response trailing-byte validation, partial nested
ReadBytes cleanup/backpressure/schema negotiation остаются открыты. Следующий
локальный шаг — response validation и partial nested-buffer cleanup.

## Strict response и decode ownership — 2026-10-14

Требование пользователя: «давай приступим к следующему шагу» — после MIT4/ENE4
выбран response validation и partial nested-buffer cleanup перед package consumer.
[Единый локальный контракт](PLAN.md#ene1ene2-strict-response-и-decode-ownership--2026-10-14).
Windows x64/build26300, net10.0, SDK10.0.402, xUnit2.9.3/VSTest.

### Реализация

PayloadDecodeScope явно владеет ReadBytes rentals одного decode, включая значения,
которые ещё не присвоены DTO/collection. Failure Dispose возвращает whole rentals;
Commit передаёт ownership caller или service. Список создаётся только при byte read.
Nested internal DTO reader получает тот же scope, public prefix reader создаёт
свой. Reflection graph walk/global ambient state/resource counters не добавлялись.

Generated typed response callback имеет один return после decode, exact-length
validation и Commit. Null/empty/scalar/bytes/DTO/arrays/lists/dictionaries проходят
один contract. Handler проверяет ВСЕ arguments и length до Commit/service entry:
при malformed later parameter прежние rentals возвращены, service не вызван.
После entry service сам отвечает за rentals даже при собственной ошибке.

Pipes void/Task ack теперь требует empty payload. Error status1 требует strict
UTF-8; error text декодируется до TryRemove matched pending, поэтому malformed
message fault также первоначального caller, а не оставляет его ждать timeout.
Успешный frame с decode failure возвращается в pool; следующий frame работает.
Read<bool> принимает0/1; duplicate/null dictionary keys дают InvalidDataException.
Generated local scope name выбирается с учётом parameter name; collision case
скомпилирован и исполнен. Correct wire format и method IDs не менялись.

### Работа и фактически выполненные проверки

- Baseline до реализации: runtime102/102, generator34/34 Debug, exit0. При первом
  переносе common return в generator validation ошибочно попала также в request
  writer block;17existing emit tests failed с CS0103. Исправлен только writer block;
  финальные compilations/runs чистые, assertions не ослаблены.
- Runtime added24cases:16scope/length/bool +8raw-pipe responses. Generator added80cases:
  compiled loaded callbacks/handlers, null/empty/trailing/truncated payloads,
  nested DTO/collections/DTO dictionary keys, duplicate/null keys, prefix offset,
  throwing setter/second constructor, no invalid dispatch и successful ownership.
  Existing supported emission theory дополнена int dictionary key/Memory value.
- Review нашёл отдельно непокрытый nested DTO dictionary key и constructor failure
  после первого argument/child; добавлены наблюдаемые rental/exception assertions.
  Не выполнялись coverage/empirical mutations; static QA в Git metadata scratch.

Из src, test projects запускались отдельно с проверкой exit code, оба configurations.
Generator final runs повторены после добавления review cases; runtime/Mithril и
manual final sources не менялись после их успешного запуска.

```powershell
$projects = @(
    @('TrueMoon.Enerit.Tests', 'runtime'),
    @('TrueMoon.Enerit.Generator.Tests', 'generator'),
    @('TrueMoon.Mithril.Tests', 'mithril')
)
foreach ($project in $projects) {
    foreach ($configuration in @('Debug', 'Release')) {
        $name = $project[1] + '-' + $configuration.ToLowerInvariant()
        dotnet test (Join-Path $project[0] ($project[0] + '.csproj')) --configuration $configuration --no-restore --logger "trx;LogFileName=$name.trx" --results-directory TestResults/EneritDecode/tests --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Failed $name" }
    }
}
pwsh -NoProfile -File ManualTests/AppUnitsPipesProbe/RunAppUnitsSmoke.ps1 -ResultsDirectory TestResults/EneritDecode/app-manual
pwsh -NoProfile -File ManualTests/EneritPipesProbe/RunPipesSmoke.ps1 -ResultsDirectory TestResults/EneritDecode/isolated
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
```

| Проверка | Финальный результат |
| --- | --- |
| Runtime | Debug126/126 +Release126/126, Failed0/Skipped0 |
| Generator | Debug114/114 +Release114/114, Failed0/Skipped0; compiler/emit и actual generated method execution |
| Mithril | Debug89/89 +Release89/89, Failed0/Skipped0; runtime Mithril без изменений |
| App composition | 36/36 Cobalt/DI ×Debug/published Release, Completed=true |
| Isolated pipes | 12/12 Debug/published Release, Completed=true |
| Solution | Final non-incremental Debug exit0/errors0/warnings1227 |

Итого current6final TRX/658passed/0failed/0notExecuted. Ignored logs/TRX/summary:
TestResults/EneritDecode/runtime-*.log, generator-*.log, mithril-*.log, tests/*.trx,
app-manual/summary.json, isolated/summary.json и solution-debug.log. Baseline и
промежуточные runs имеют отдельные filenames; они не заменяют final counters.
Process source fingerprints сверены с current source, fresh case directories/GUID
endpoints исключают stale readiness records. Source/test static Roslyn pairing
запущен один раз; IDE findTests недоступен. Scratch research/plan/status находятся
в .git/testagent/enerit-decode и не являются deliverables/stageable files.

### Requirement / evidence

| Requirement | Exact test / observable outcome |
| --- | --- |
| «давай приступим к следующему шагу» | GeneratedResponses_RejectTrailingBytesAndRollBackNestedRentals и GeneratedResponses_LaterTruncationReturnsPreviouslyDecodedBytes: InvalidDataException, captured nested whole rentals inaccessible/returned; DTO dictionary keys включены |
| «давай приступим к следующему шагу» | GeneratedResponses_ConsumeExactlyAndTransferWholeRentalsToCaller и Commit_PreservesCallerOwnershipAndRejectsFurtherReads: exact literal values, live successful rentals, caller TryReturn=true once |
| «давай приступим к следующему шагу» | GeneratedHandler_InvalidLaterArgumentDoesNotDispatchAndReturnsEarlierRentals / GeneratedHandler_ValidatedArgumentsTransferOwnershipBeforeServiceEntry: Calls0 vs1, original service exception и input ownership |
| «давай приступим к следующему шагу» | LaterDtoConstructorFailureReturnsEarlierRentalsAndPreservesOriginalError / GeneratedDto_ThrowingSetterReturnsRentalAndPreservesOriginalError: response/request scopes, exact original error identity, earlier rentals returned |
| «давай приступим к следующему шагу» | Client_NonemptyAcknowledgementIsRejectedAndNextCallSucceeds / Client_InvalidUtf8ErrorFaultsAllPendingWithoutLosingMatchedRequest / Client_DecodeFailureReturnsOuterResponseAndPreservesNextFrame: bounded caller errors, outer rental returned, next valid response |

### Границы и совместимость

Новая generation требует runtime с PayloadDecodeScope/EnsureFullyConsumed. Старый
generated code не приобретает strict validation автоматически. Valid same-build
wire unchanged; malformed data теперь отклоняется явно. DTO public Deserialize —
prefix API, не whole-frame reader. Partial offset и user-code side effects не
откатываются; successful input/output rentals принадлежат service/caller, в том
числе после service throw. Manual unscoped ReadBytes/opaque callbacks имеют собственный
ownership/validation contract. Scope synchronous и не thread-safe.

Core/Cobalt suites, UI/native, independent NuGet/analyzer delivery, other OS,
performance, MMF/backpressure/schema negotiation и uncooperative shutdown этим
шагом не проверены. Parent-exit matrix наблюдает child/server cleanup; client
workers killed parent не наблюдаем. Passing bounded proof не zero-leak/stress claim.
Далее ENE5/MIT5 independent package composition. Commit/push не выполнялись.

Документация:45Markdown documents,493local links,129heading links,0problems. Checker/links.json — ignored TestResults/EneritDecode. Default git diff --check passed; new untracked source/doc files проверены отдельно. QA research/plan/status отсутствуют в stageable git status.

## ENE5 package portion — 2026-10-14

[Единый журнал/команды](../mithril/HISTORY.md#mit5ene5-independent-package-consumer--2026-10-14).
Enerit/runtime generator packaged descriptions/README добавлены; C# logic без изменений.
9local packages,108/108independent consumer cases,4compiler configurations с actual
analyzer path/SHA, expectedCS0234 без Enerit analyzer. Runtime126/126 и generator114/114
Debug/Release, Failed0/Skipped0; related Mithril89/Core81/Cobalt37/generator6 также
прошли отдельно. Final solution Debug non-incremental0errors/1227warnings.
Same-build Windows x64/net10.0/SDK10.0.402; published/build consumer запускает own child.
Local packages не опубликованы. Package portion выполнена; ENE5 performance baseline,
MMF/backpressure/schema/uncooperative shutdown/other OS и MIT5 diagnostics открыты.
