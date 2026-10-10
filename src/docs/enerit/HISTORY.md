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
