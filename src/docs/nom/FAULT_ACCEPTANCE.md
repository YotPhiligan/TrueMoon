# TrueMoon.Nom: ошибки IO и незакрытая fault acceptance

> NOM2 завершён2026-11-08: [текущий backend и проверки](CATALOG.md#nom2-catalog-2026-11-08).
> Vendor runners/downloads удалены; команды и результаты этого документа исторические.

> Исторический LiteDB срез. Последующим решением2026-11-07 принята полная замена
> собственной реализацией Nom: [решение](STORAGE_DESIGN.md#nom-own-store-2026-11-07),
> [активная очередь NOM0–NOM7](PLAN.md#nom-own-queue). Результаты ниже относятся
> только к старому backend; vendor fix/upgrade/isolation больше не активная задача.

<a id="nom-faults-2026-11-07"></a>
## AST1b.f — частично, есть подтверждённый blocker

**2026-11-07: исправлены write/cleanup/close paths; весь AST1b.f не завершён.**
Безопасный xUnit набор:86/86 каждый Debug/Release (57App +29fault cases), failed/skipped0.
Core regression:18groups каждый source Debug/published Release. Отдельные WAL probes
дали **8/8 реальных падений** pinned LiteDB5.0.21; AcceptancePassed=false, runner exit1.
Physical disk-full — отдельный environment gate: создание64MiB VHDX отклонено Windows.
Чистая unit regression не отменяет этих результатов и не означает storage readiness.

## Реализация и владение

- NomFileOperations — внутренняя per-factory зависимость для операций sink/native
  files. Test overrides не входят в public options/API, глобального mutable override нет.
- Ошибка write/flush/finalize не публикует часть пачки. Ошибка abort/cleanup больше
  не скрывает исходную ошибку: обе причины сохранены в AggregateException.
- Committed cleanup failure возвращает CleanupPending=true и удерживает единственную
  write reservation. Replay того же ticket повторяет cleanup без чтения replacement
  streams; новая reservation до успешной очистки не принимается. Параллельный replay
  не освобождает writer, который ещё завершает собственную cleanup IO.
- Unknown outcome + cleanup failure сохраняет ticket, исходный Unknown и cleanup cause.
  Stop освобождает owned staging, не меняя неоднозначный journal outcome. Abort/recovery
  всегда пытаются освободить staging даже при ошибке metadata WAL, чтобы не удерживать
  место, необходимое для восстановления. Recovery сначала валидирует весь journal.
- Owner thread после close failure остаётся жив и ждёт явного retry. Каждая drain
  attempt имеет собственный completion; timeout/cancel wait не освобождают live resources.
  Stop всех owners запрашивается до ожидания группы, без скрытого второго retry.

## Особенность LiteDB close

Pinned source391cc9318c5be6e56cb71b3ce14b1ed9cb324763:
LiteEngine.Close помечает engine disposed, собирает ошибки checkpoint/close в TryCatch;
LiteEngine.Dispose не проверяет возвращённый список. Сам по себе успешный вызов
LiteDatabase.Dispose недостаточен для наблюдения таких ошибок и подтверждения FD close.

NomCatalogConnection теперь явно владеет data/log/temp FileStreams. LiteDB получает
non-owning Stream adapters с записью ошибок native IO. Engine создаётся напрямую с
Collation.Binary, Upgrade=false, AutoRebuild=false; публичные BSON/native API не добавлены.
Actual data/log handles исключают другого raw writer, temp имеет DeleteOnClose.
Наблюдаемые ошибки IO из native close проверяются отдельно даже когда vendor Dispose
их проглотил. После уже потреблённого native close retry завершает оставшиеся owned
flush/close и root unlock; metadata transactions не проигрываются заново.
Все DB open/access/close остаются на одном owner thread.

Transient fault до/после native close, durable flush/actual file close/root unlock
сохраняет незавершённые handles/registry до успешного retry. Это не способ перехватить
произвольную ошибку финализатора vendor: ниже подтверждён отдельный процессный дефект.

## Проверки безопасных сценариев

| Проверка | Наблюдаемая гарантия |
| --- | --- |
| SinkFault_BeforePublication_AbortsWholeBatchAndKeepsOldContent | Create/write с0/partial/all bytes before throw, async/durable flush, first/second finalize: old generation/heads/bytes, durable aborted journal, borrowed inputs |
| AbortCleanupFailure_PreservesBothErrorsAndRetainsReservationUntilRetry | Обе exact causes, retained write users, отказ новой reservation, успешный cleanup/drain после снятия fault |
| CommittedCleanupFailure_ReplayRetriesCleanupWithoutReadingReplacementInput | Committed receipt/heads/generation при CleanupPending, retry без reread, освобождение reservation после cleanup |
| CommittedCleanupFailure_StopRetainsRootAndReopenKeepsCommittedOutcome | Реальный OS root lock при неуспешном stop, затем confirmed close/reopen без потери outcome |
| ConcurrentCommittedReplay_DoesNotReleaseOriginalPublisherDuringCleanup | Gated IO original writer, no second delete/early EndWrite, stop удерживает root до actual return |
| UnknownOutcomeAndCleanupFailure_KeepTicketAndOwnershipUntilCloseReopen | Ticket + original lost-ack Unknown + cleanup cause, retained users, close/reopen resolution |
| CloseFault_RetainsRootAndRegistryUntilSameThreadRetry | Native close before/after call, actual files flush/close и root unlock; реальные handles и тот же owner thread |
| NativeCheckpointIoFailure_SwallowedByLiteDbDispose_IsReportedAndWalRecovers | Native data write/flush/log SetLength close faults наблюдаются, retry не повторяет native mutation, reopen восстанавливает bytes |
| CatalogOpenFailure_ReleasesAlreadyOpenedFilesAndDoesNotConsumeCapacity | Частично открытые raw handles освобождены до следующей admission |
| ConcurrentCloseRetry_AfterFault_SharesOneOwnerAttemptUntilActualClose | Concurrent product/host retry не создаёт второй native close attempt |
| FailedStartupAndCloseFailure_PreserveBothCausesAndRetainedOwnerForLaterDrain | Startup+close causes, capacity retention, явный дальнейший drain |

Синтетический disk-full fault — IOException с HRESULT0x80070070 у конкретного IO
вызова. Это не physical disk-full. Тесты используют реальные ограниченные temp roots,
OS locks и FileStream handles, но не заполняют рабочий диск.

## Подтверждённый дефект WAL/finalizer

Четыре сценария выполняются **только в отдельных bounded child processes**:
commit-zero, commit-partial (1byte), commit-full (один whole native write перед throw)
и abort (asset sink failure + ошибка WAL фиксации abort). В каждом проверяются ticket,
ровно одна WAL fault, сохранённые причины и целиком old либо new state после reopen.
Потом принудительно запускаются finalizers для проверки process continuity.

В source Debug и published Release все4cases достигли atomic-outcome-verified:
generation1, aborted operation, только исходный asset и исходные bytes/hash. Затем
каждый процесс аварийно завершился с необработанным исключением:

```text
LiteDB.LiteException: discarded page must be writable
LiteDB.Engine.MemoryCache.DiscardPage
LiteDB.Engine.DiskService.DiscardDirtyPages
LiteDB.Engine.TransactionService.Dispose
LiteDB.Engine.TransactionService.Finalize
```

Наблюдение согласуется с pinned source: DiskService.WriteLogDisk переводит page в
readable state до записи stream; interrupted write может оставить active transaction.
TransactionService.Dispose затем требует writable dirty pages; finalizer вызывает
тот же Dispose без защиты, а state/reader cleanup и suppression происходят позже.
Обычный try/catch вокруг публичной операции не перехватывает finalizer-thread exception.
Новая Nom обвязка удержания файлов не устраняет этот vendor defect.

Это **воспроизведённый blocker**, не пропущенный тест и не environment gate.
Падения не объявлены expected-success assertions в xUnit. Первичная Attempt2 с WAL
faults внутри test host прервалась после45passed; опасные cases перенесены в child
runner без Skip. Child runner сохраняет reports/stdout/stderr, ждёт confirmed exit,
убивает tree при20s timeout и возвращает nonzero при любом non-passing outcome.
Все8финальных children reaped; RunnerCompleted=true, AcceptancePassed=false.

## Повторение и результаты

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug '-bl:TestResults/Nom-Faults-Debug-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release '-bl:TestResults/Nom-Faults-Release-{}.binlog'
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/MyFreshNomFaultCore
$proof = Get-Content TestResults/MyFreshNomFaultCore/verification.json -Raw | ConvertFrom-Json
& ./ManualTests/TrueMoon.Nom.Probe/VerifyWalFaults.ps1 -AssemblyPath (Join-Path $proof.RunDirectory 'publish/TrueMoon.Nom.Probe.dll') -EvidenceDirectory TestResults/MyFreshNomWalFaults
```

Windows x64, SDK10.0.403/runtime10.0.14, official LiteDB5.0.21/DLL5.0.21.0;
DLL SHA AE31AC6A93549217B9E8B81497D1EE831658ADE6DF9E1EE20F2838F22DE5E218
не менялся. Vendor package не обновлялся/патчился. CoreFinal:2profiles/5commands0/
3SDK binlogs/35frozen source/project/config inputs, drift0. Последние safe tests:
DebugReviewed/ReleaseReviewed86/0/0; solution-filter Debug86/0/0. Общая nonincremental solution Debug0errors/
1555existing warnings, delta0; новые Nom warnings0. Full unit suite/GPU не запускались,
прежние Aether failures не закрыты; symlink privilege gate остаётся.

Raw ignored evidence — TestResults/NomFaults-20261107: tests-*-reviewed.log/TRX,
CoreFinal, WalDebugFinal/WalReleaseFinal verification+4reports+stderr/stdout каждый,
solution-debug.log, solution-nom-tests.log/TRX, volume-capability.json и binlogs. Source-код runner и эта
versioned запись сохраняют scope/команду/результат без зависимости от ignored artifacts.
Physical VHD probe New-VHD64MiB вернул Hyper-V permission error, Administrator=false;
том не подключался/не форматировался. Это OS permission gate, не auto-review rejection.

## Очередь и готовность

AST1b.f software improvements проверены в указанном scope, **fault acceptance открыта**.
Следующий приоритет — решение native WAL blocker: проверенный vendor fix/версия либо
отдельный process boundary для catalog backend. Это ещё не выбранная архитектура и
не разрешение молча менять pinned dependency. Затем physical isolated-volume proof.
AST1c snapshots/pins/GC и AST2b delivery сохраняются в плане, но production readiness
из passing safe tests не выводится. [План](PLAN.md#nom-fault-queue), [статус](STATUS.md).
