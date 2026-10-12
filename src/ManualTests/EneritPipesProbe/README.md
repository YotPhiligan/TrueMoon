# Bounded Enerit pipes probe

Проверяет два собственных процесса через реальные generated client/handler и
`PipesInvocationClient`/`PipesInvocationServer`. Child запускается
`ChildProcessUnitHandle` с `RestartPolicy.Never`; production проекты Enerit и
Mithril не получают зависимость друг на друга. Generator подключён явным analyzer
ProjectReference, без App/DI registration и NuGet delivery claim.

Из `src` на Windows x64 с SDK из `global.json`:

```powershell
./ManualTests/EneritPipesProbe/RunPipesSmoke.ps1
```

Runner строит Debug apphost и framework-dependent published Release без RID;
исполняет шесть сценариев в каждой конфигурации. Logs и `summary.json` сохраняются
в ignored `TestResults/EneritPipes/manual`. Completed=true записывается после всех
12 успешных запусков; summary содержит команды, exit codes, SDK/OS, source SHA256,
parent/child PID и уникальный pipe endpoint. Build deadline60s, scenario40s,
внутренний invocation deadline20s, shutdown5s. Timeout cleanup касается только
Process, созданного runner, и его descendants; name-based process cleanup нет.

| Scenario | Наблюдаемый результат |
| --- | --- |
| payload | Null/empty/Unicode, nullable scalar, DTO scalar/list/dictionary/bytes transforms, memory до frame limit; oversized/malformed/unknown method и remote errors; void/Task acknowledgement |
| concurrent | 32 параллельных typed DTO transforms с разными payloads и порядком выполнения; проверены все поля результата |
| cancellation | После подтверждённого входа handler локальная отмена; late response действительно drained, следующий вызов успешен, одно соединение сохраняется |
| disconnect | Exit собственного child завершает pending invocation IOException до timeout |
| client-dispose | Dispose во время handler завершает pending ObjectDisposedException и worker tasks |
| server-rebind | Cooperative server shutdown прерывает pending; тот же клиент автоматически reconnect и выполняет typed call после повторного открытия того же endpoint |

Каждый сценарий проверяет pending map=0, обе client scheduler Completion, отказ
после Dispose, наблюдаемый child exit0, released owned Process и повторный Stop.
ReadBytes rentals возвращаются явно в handler/caller, включая DTO.Bytes.
Получатель должен вернуть их после использования; transport возвращает только
свои frame buffers. Checks в summary — число scenario checkpoints, не coverage.

xUnit `PipesProcessTests` копирует fixture в test output и повторяет эти сценарии
через `dotnet EneritPipesProbe.dll` в Debug/Release. Это отдельный hosted launch
mode, не дополнительные варианты App/AddUnit/restart.

Контракт и breaking changes: [ENE1/ENE2](../../docs/enerit/PLAN.md#ene1-ene2-контракт--2026-10-11).
Peers должны использовать одинаковые sources/generator: native scalar layouts,
порядок method IDs, nullable-string encoding и remote ack не имеют version
negotiation. Frame limit1MiB, collection limit65 536. Cancellation локальная,
server получает connection-shutdown token. Reconnect не повторяет failed invocation
и может иметь несколько промежуточных соединений при teardown.

Этот proof не подтверждает MMF, App/AddUnit/controller/parent-exit/restart,
межверсионный ABI, backpressure, автоматический cleanup partial nested decode,
неограниченный shutdown произвольного handler или independent packages/performance.
Текущие результаты — [STATUS](../../docs/enerit/STATUS.md), команды — [HISTORY](../../docs/enerit/HISTORY.md#ene1-ene2-pipes-baseline--2026-10-11).
