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
