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
