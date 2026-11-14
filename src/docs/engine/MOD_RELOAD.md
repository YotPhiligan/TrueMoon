# ARCH3.7: горячая замена систем со стабильными компонентами

`ModHost.CreateSystemSession(World)` создаёт App-owned `ModSystemSession`, который
заимствует существующий caller-owned World. `ReplaceAsync` подготавливает группу
в отдельном collectible ALC и заменяет весь её system graph между завершёнными
ticks. World, entity handles, typed pools, component values и номер следующего tick
сохраняются. Состояние систем переносится явно; скрытого restart/reset нет.

Это отдельный профиль поверх [ARCH3.6 manifest/loader](MODS.md). Обычный
`ModGroup.CreateWorld` сохраняет прежний lifecycle. Private модовые CLR-компоненты
и new-World migration используют [отдельный профиль ARCH3.8](MOD_MIGRATION.md);
автоматический file watcher и замена
произвольного чужого scheduler в этот API не входят.

## SDK и границы профиля

Новые contracts находятся в `TrueMoon.Aether.Abstractions.Mods`; SDK-only мод
ссылается только на Abstractions/Contracts. Runtime и Tooling для создания модовых
систем не требуются. Дополнительные stable game API assemblies задаёт host allowlist
и exact shared descriptors manifest, как в ARCH3.6.

- Каждый entry и локальный service реализует `IModReloadParticipant`:
  `QuiesceAsync` обратимо останавливает работу, `ResumeAsync` восстанавливает её
  после отказа replacement, включая частично выполненный quiesce.
- Каждая система реализует `IModReloadableSystem`: `CaptureState()` возвращает
  полный side-effect-free снимок, `RestoreState(snapshot)` восстанавливает
  частный candidate или явно отклоняет несовместимый version/payload.
- `ModSystemState` — sealed neutral object: положительная system-defined version
  и copied bytes. Runtime дополнительно копирует payload при capture и restore;
  живые Types/instances/delegates/exceptions не переносятся.
- Наборы system IDs обязаны совпадать точно. Неизвестный/пропавший/добавленный ID
  отклоняется; никаких implicit state drop/default reset. Порядок/phase/access
  graph candidate валидируется обычным `SystemGraph`.
- Все уже хранящиеся World component types и declared system access types должны
  быть shared stable CLR types, включая generic/element arguments. Collectible
  component types запрещены. Регистрация component importers через `AddComponent`
  в этом systems-only профиле также запрещена: registry/materialization принадлежат
  стабильному host, а изменения binding/type требуют отдельной migration policy.
- Максимум128systems,64KiB state на систему,4MiB на всю группу,256guarded callbacks
  на generation. Artifact/module/reference bounds остаются прежними. Превышение
  state budget отклоняет весь capture, не обрезает payload.

Entry/service persistent gameplay state должен находиться в явно captured systems
или в отдельно согласованном stable host storage. Их частные tasks/subscriptions
и prepared resources — transient lifecycle, не неявный дополнительный save-state.

## App, DI и ownership

```csharp
var app = App.Builder(_ => { }).Setup(context =>
{
    // Stable host APIs register here. Exact additional shared APIs are opt-in.
    context.UseAetherEcs();
    context.UseAetherMods();
}).Build();
await app.StartAsync();

// Composition root; gameplay services receive these dependencies by constructor.
var world = app.Services.Resolve<IFactory<World>>().Create();
var host = app.Services.Resolve<ModHost>();
var session = host.CreateSystemSession(world);
var initial = await session.ReplaceAsync(firstRequest); // Generation 1
session.Tick(fixedDelta);
var replacement = await session.ReplaceAsync(nextRequest);
```

Host владеет session, current/candidate/retired groups, factory products и failed
unpublished owners. Caller владеет World (либо SceneWorld, из которого он заимствован).
Host/session никогда не освобождает World и не очищает его данные. Успешно завершите
`session.StopAsync`/App cleanup **до** World/SceneWorld.Dispose и освобождения borrowed
dependencies. Early session Dispose допустим; App cleanup идемпотентен.

Прямое создание через зарегистрированный `IFactory<ModSystemSession, World>` —
low-level caller-owned путь: такой result сам по себе не становится owned объектом
ModHost/DI. Его потребитель обязан выполнить cleanup до App/dependencies disposal.
Штатный App-owned путь — `ModHost.CreateSystemSession`.

World должен быть idle/published/healthy и не иметь другого активного scheduler.
Session владеет всем replaceable graph; добавление subset поверх чужого scheduler
не выполняется. Нельзя конкурентно менять/освобождать borrowed World обходным
путём; owner сериализует такие операции с session. Все системы создаются через
внедрённые локальные factories. Фабрики/Types модов в долгоживущий App DI не попадают,
resolution на каждый кадр отсутствует.

## Подготовка, capture, commit и rollback

1. Validate/pin candidate artifacts, создать новый ALC, entries и services. `StartAsync`
   готовит частные ресурсы. Candidate callbacks ещё закрыты; ticks старой generation
   могут продолжаться во время этой подготовки.
2. Получить boundary gate после завершения активного tick. Создать detached scheduler
   и systems, не меняя World owner. Проверить exclusive factory results, включая
   запрет alias к текущим systems. Factory/capture/restore не могут записывать,
   удалять или проигрывать commands в borrowed World: действует preparation fence.
3. Закрыть old callback admission и дождаться in-flight callbacks. Выполнить quiesce
   entries/services reverse-order без terminal Stop/Dispose. Capture старых systems,
   проверить бюджеты/IDs и restore новых private instances.
4. Проверить cancellation непосредственно перед commit. Переключить World scheduler
   owner, current generation и tick sequence под boundary gate. В commit нет module
   callbacks, resolution или await. Старые callbacks retired/cleared, новые открыты.
5. Освободить boundary gate: новые ticks идут независимо от последующего old cleanup.
   Остановить/освободить retired module resources, очистить registrations/factories
   и запросить ALC unload. Failed disposal остаётся owned для явного retry.

Отказ/cancel до commit очищает candidate и вызывает Resume успешно/частично quiesced
старых participants, services-first затем entries. Guard admission открывается
только после полного успешного resume. Старые системы не создаются повторно;
World/state/tick sequence остаются прежними. Capture обязан быть read-only и полным:
Runtime не может откатить произвольную запись в собственные поля/внешний мир внутри
неправильного capture/startup кода.

При ошибке Resume возвращается `RollbackFailed`, `ResumeRequired=true`, ticks и
следующий replacement закрыты. `ResumeAsync()` повторяет только неуспешные resume
steps и открывает старую generation после успеха; скрытого restart нет.

Cancellation, пришедший после последней проверки перед commit, может дать успешно
применённую generation. После commit отмена не превращается в rollback; cleanup
должен завершиться кооперативно. App stop сначала закрывает admission и отменяет
pending preparation, затем ждёт replacement/tick/callback drain и release owners.

## Callback и command fences

Entry получает `IModGeneration` один раз через composition-only
`IModHostServices.GetRequired<IModGeneration>()`. `Guard(Action)` возвращает
**stable host delegate**. Передавайте его подписчику напрямую:

```csharp
var callback = generation.Guard(OnInput);
events.Subscribe(callback); // Keep this exact callback for Unsubscribe.
```

Оборачивание в модовую lambda `() => callback()` снова создаст внешнюю ссылку на
модовый метод/closure — так делать нельзя. Staged/quiesced/retired delegate ничего
не выполняет. Quiesce ждёт всех уже вошедших callbacks; rollback может открыть тот
же delegate снова. Retire очищает module target, поэтому даже удержанный host
inert delegate не удерживает старый ALC. Module Stop/Dispose снимает собственные
подписки и очищает caches/tasks; fence не заменяет корректный lifecycle.

Guard поддерживает синхронные callbacks. Нельзя запускать detached async work/async
void или reenter эту session из callback: quiesce ждёт callback, удерживая tick
boundary. Собственные async workers должны иметь обратимый drain/resume и terminal
Stop. Guard exports callback failure как neutral `ModOperationException` без
модового InnerException. Неправильные unfenced subscriptions/global side effects,
native mods и forced thread abort не поддерживаются; это не security sandbox.

SDK contexts/queries/command writers ограничены одним Execute invocation. Сохранённый
old writer не может записывать в новую generation. Candidate получает новый phase
command buffer; commit допускается только после полностью завершённого old tick
без pending commands. Отдельной очереди старых commands, переживающей commit, нет.

## Outcomes и cleanup

`ModReloadResult` содержит `Committed`, текущую `Generation`, основную `Diagnostic`,
отдельную `Cleanup` и weak `UnloadTicket` для rejected/retired context.

| Сценарий | Состояние |
| --- | --- |
| Validation/start/factory/capture/restore/cancel до commit | `Committed=false`; old graph resumed, source error сохранён |
| Ошибка resume old participants | `RollbackFailed`, `ResumeRequired`; явный `ResumeAsync` |
| Candidate cleanup failure | Основная rejection причина отдельно от `CleanupFailed`; failed owner остаётся у session |
| Успешный commit + old cleanup failure | `Committed=true`, новая generation работает, `CleanupFailed` виден отдельно |
| Retry cleanup | `RetryCleanupAsync()` освобождает rejected/retired owners, не меняя current World/graph |
| Terminal current Execute failure | Session закрывается; faulted World не перезапускается. Failure в World sanitizes к host exception, чтобы сохранённый World не удерживал mod exception/ALC |

Успешный unload request не равен collection. Проверяйте weak ticket после выхода
из module/loader stack frames; forced GC используется только в диагностике.
Shared DLL hashes требуют coherent rebuild SDK/host/mods. API additions сохраняют
manifest/API generation1; это не разрешает подмену exact shared bytes. Прежние
game/scene/project/control/frame/checkpoint formats не менялись.

## Проверки и продолжение

| Requirement | Exact test evidence |
| --- | --- |
| World/handles/data/tick/system-state continuity | `ModReloadTests.StableWorldAndStateSurviveCommit` |
| Tick/callback fences, expired commands | `ReloadWaitsForActiveTick`, `ReloadDrainsActiveGuardedCallbackBeforeCapture`, `StableWorldAndStateSurviveCommit` |
| Reject/cancel preserves last-good | `RejectedCandidateResumesLastGood`, `CancellationBeforeCommitPreservesStateAndReopensOldAdmission`, `CancellationAtCommitBoundaryHasUnambiguousOutcome` |
| Explicit rollback recovery | `ResumeFailureIsExplicitAndRetryableWithoutRestart` |
| Committed retirement and rejected-owner cleanup | `AppliedCommitReportsRetirementFailureWithoutRollback`, `RejectedCandidateOwnerRetainedForRetry`, `NewTicksContinueWhileRetiredCleanupIsPending` |
| Shared types/state IDs/budgets/ownership | `RejectUnsupportedReloadProfile`, `EmptyGraphRemainsEmptyAndAddingUncapturedSystemsIsRejected`, `GroupCaptureBudgetRejectsWithoutTruncatingOrResettingSystems`, `AnotherSessionCannotStealWorldOwnership` |
| Callback bounds/exception roots | `ModGenerationTests.GuardBudgetAndTerminalCloseAreExplicit`, `FailedCallbackExportsOnlyNeutralExceptionAndStillDrains` |
| App stop/both DI and actual unload | `HostStopClosesReloadAdmissionAndDrains`, `RetiredAndRejectedContextsCollectWithRetainedCallbacksAndDisposedSession` |

Адресные тесты: `ModReloadTests` и `ModGenerationTests` в Aether.Tests, прежние
ModLoader/ECS tests сохранены. Independent runner:
`ManualTests/ModConsumer/Verify.ps1 -Reload -EvidenceDirectory <fresh path>`.
Он проверяет source/publish D/R, fresh package-only две разные DLL и SDK-free host
на обоих DI; retired ALC обязан collect **при ещё живой current generation**.
Exact source hashes/profile/TRX/binlogs и границы фиксируются в
[ARCH37_BASELINE.json](ARCH37_BASELINE.json) и [HISTORY](HISTORY.md#arch37-2026-11-14).

Основание cooperative unload:
[официальное руководство .NET](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability).
Private component types, neutral component snapshots/migrators и commit нового
World реализованы в [отдельном owned-World профиле ARCH3.8](MOD_MIGRATION.md).
Полная архитектурная/OS/native/GPU/release приёмка и20cycles
во всех требуемых profiles остаются ARCH3.9/соответствующими gate владельцами.
