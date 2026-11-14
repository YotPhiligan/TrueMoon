# Post-reap output drain и cleanup: диагностический срез ARCH2e.4b

2026-11-12. [Задача и границы](PLAN.md#arch2e4b-drain-diagnostics),
[датированные команды/результаты](HISTORY.md#arch2e4b-2026-11-12).
Диагностика расширена; причина исторического intermittent timeout не установлена.
Этот документ описывает наблюдения и сбор evidence, не новый механизм cleanup.

## Какие состояния теперь видны

`ProcessOutputCapture.Read(maximumCharacters)` возвращает прежний
`ProcessOutputSnapshot` с дополнительными init-свойствами. Конструктор DTO,
старые counters/tail semantics и `Limit` сохранены. `Read(0)` оставляет текст
пустым и сохраняет metadata; omissions не означают потерю данных самого capture.

| Поле | Значение и граница вывода |
| --- | --- |
| ReadStage / ReadStageUtc | Queued → AwaitingRead / Capturing → EndOfStream либо Failed; UTC последнего изменения стадии |
| EndOfStream | Реальный TextReader.ReadAsync вернул 0. Успешное ожидание или read failure сами по себе не приравниваются к EOF |
| FileStage / FileStageUtc | Состояние dedicated file worker: Disabled/Queued/Opening/WaitingForChunk/Writing/Closing/Completed и UTC перехода |
| QueuedFileChunks | Оставшиеся принятые chunks, 0..8; chunk, уже взятый worker для записи, не включается |
| FileQueueDrained | Producer завершил очередь и worker забрал все chunks. Последние write/close могут ещё выполняться |
| FileWorkerThreadId | Managed ID dedicated worker; 0 при Disabled или до его запуска |

`ReadCompletion`, `FileCompletion` и общий `Completion` остаются отдельными
tasks. `FileStage=Completed` вместе с FileError означает завершившийся file
worker с наблюдаемой потерей/ошибкой, а не успешную запись. Новые поля — мониторинг
нескольких конкурирующих участников; это не атомарный снимок всей ОС. UTC помогает
сопоставлять события, не является монотонными часами или доказательством причины.

`NativeRuntimeFactory.ReadDiagnostics` и `IProcessDiagnosticsSource.Read` сохраняют
PID/session/generation, Reaped/ResourcesReleased, stream observations. Дополнительно
`ProcessDiagnosticSnapshot.CleanupStage` различает ClosingControl, ProcessReap,
OutputDrain, DiagnosticReceiverDrain, ChildDispose, BuildPinRelease и Released;
CleanupFailureType хранит тип последней ошибки. У compiler observations доступна
стадия текущего output owner. Эти чтения не запускают IPC/flush/IO и не разрешают
вызовы контейнера из игровых циклов.

## Снимок ошибки и сохранение ownership

`OwnedProcessOutput.DrainAsync` использует прежние deadlines: Runtime 2 секунды,
compiler 5 секунд. При timeout сообщение содержит прежние read/file task states
и дополнительный JSON. Тот же JSON сохраняется строкой в
`Exception.Data["TrueMoon.ProcessOutputDrain"]`; при read/close failure он
прикрепляется к исходному исключению без замены его объекта и stack trace.

JSON содержит owner, PID, session/generation либо compiler JobId, время наблюдения,
начала/завершения drain, статус underlying drain task, стадию, число попыток
actual cleanup, признаки освобождения каждого standard stream, ThreadPool workers/
pending/available и оба progress snapshots. Output text и file error text в этом
снимке отсутствуют; bootstrap nonce, endpoint, command line и environment туда
не добавляются. Обычные stream snapshots по-прежнему могут содержать producer text
и ограниченные file failure details по существующему контракту.

Диагностика не выполняет IO и не берёт gate drain owner, в том числе когда
stream Dispose ещё не вернулся. Она не отменяет reader/file tasks и не закрывает
потоки по timeout. Повторный явный cleanup ждёт прежнюю незавершённую drain task;
после close failure повторно освобождаются только оставшиеся owners. Полученная
строка failure snapshot остаётся прежним evidence после последующего успеха.
Read failure и завершившийся file drain могут сосуществовать: ошибка чтения
сохраняется даже при уже закрытых standard streams.

## Что фиксировать при следующем повторении

1. Сохранить исходное исключение/stack/inner exceptions и точную команду,
   SDK/profile, SHA binaries, PID/session/generation. В AggregateException либо
   session cleanup error найти вложенный `TrueMoon.ProcessOutputDrain`.
   До retry сохранить memory observations через уже внедрённый process diagnostics
   source с нулевым окном текста, например `Read(8, 0)`.
2. Различить deadline harness (`WaitAsync` вокруг Stop/App.Dispose) и собственный
   post-reap output timeout. Для первого взять CleanupStage и Completed/Reaped;
   сам generic TimeoutException не доказывает, что завис output reader.
3. Сопоставить EOF, read/file/capture task states, queue count/drained, file worker
   stage/thread, written/dropped counters и UTC. При необходимости получить
   targeted stacks/dump у сохранившегося owner; отсутствие stack/dump нельзя
   заменить предположением о ThreadPool или успешным replay.
4. Отдельно записать фактический исход stop/reap/drain/dispose, оставшиеся owners
   и результат явного retry. Не перезаписывать первую ошибку последующим успехом.
   Retention сырых artifacts задаёт harness; failure evidence сохраняется.

| Наблюдение | Что уже различимо | Что ещё не доказано |
| --- | --- | --- |
| Reaped=true, EndOfStream=false, AwaitingRead | EOF ещё не наблюдался reader, даже если bytes уже попали в файл | Кто удерживает pipe либо почему read/continuation не завершились |
| EOF=true, file queue/drain ещё pending | Pipe вернул EOF; остаётся capture completion/file work | Причина задержки worker/IO/continuation без targeted evidence |
| FileQueueDrained=true, Closing | Все queued chunks взяты; close ещё не подтверждён | Успех записи/закрытия и завершение file task |
| Close failure, один released flag=false | Конкретный stream остаётся owned; другие release уже выполнены | Возможность и успех следующей попытки |
| Native CleanupStage=DiagnosticReceiverDrain | Output phase уже пройдена; ожидание принадлежит другому owner | Причина receiver/diagnostic pipeline задержки |
| Stage=Released и ResourcesReleased=true | Owner завершил известный порядок release | Отсутствие любых внешних/process-wide утечек или готовность других OS/RID |

## Проверки поведения

| Требование | Точный тест / evidence |
| --- | --- |
| Pending read отделён от EOF, queue/file drain; reader заимствован | `TrueMoon.Tests.ProcessOutputCaptureTests.Progress_DistinguishesPendingReadFromEofAndDrainedFileWithoutOwningReader` |
| Read failure не становится EOF; файл уже принятых bytes дренируется | `TrueMoon.Tests.ProcessOutputCaptureTests.ReaderFailure_FaultsCompletionAfterDrainingAlreadyCapturedFileChunks` |
| Timeout хранит корреляцию/время/пустой текст; streams retained, retry close-once, старый snapshot неизменен | `TrueMoon.Aether.Tests.OwnedProcessOutputTests.Drain_TimeoutRetainsStreamsAndRetryPersistsOutputBeforeClosingOnce` (stdin enabled/disabled) |
| Исходный read error сохраняет identity, cleanup attempts и release наблюдаемы | `TrueMoon.Aether.Tests.OwnedProcessOutputTests.Drain_ReadFailurePreservesOriginalErrorAndClosesEveryStreamAfterFileDrain` |
| Partial close outcome и повторное освобождение только оставшегося owner | `TrueMoon.Aether.Tests.OwnedProcessOutputTests.Drain_CloseFailureAttemptsOtherStreamsAndRetriesOnlyUnreleasedOwner` |
| Реальные graceful/exit/kill children, actual standard handles closed, Released/EOF | `TrueMoon.Aether.Tests.NativeRuntimeOutputTests.Stop_ReapsChildDrainsOutputAndClosesAllAccessedStandardStreams` |
| Source Debug/published Release flood, 6 generations/profile, >2Mi characters/stream и 8 bounded retained files | `EngineSessionProbe diagnostics <report-directory>`, report.Processes с нулевым text window |
| Новые public свойства Core, JSON roundtrip и exact fresh package payload | Независимый Core PackageReference consumer: Debug build + Release FDD publish, отдельные feed/cache/profile |

Проверки запускаются в общем .NET 11 профиле с runtime-async=on и xUnit 2/VSTest.
Точные TRX counts, команды baseline и границы source/package proof сохраняются
в [HISTORY](HISTORY.md#arch2e4b-2026-11-12), а raw artifacts — дополнительно в
ignored `TestResults/ARCH2e4b-20261112-150909/`. Это короткий диагностический срез,
не закрытие causal fix, GPU/physical/release/performance или всей ARCH2e.4b matrix.
