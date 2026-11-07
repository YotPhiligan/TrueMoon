# Загрузка ECS сцены в Runtime

**2026-11-06, ARCH1e.3b.** Initial-only Runtime profile поверх
[полного scene payload](ECS_LOADING.md). Один fresh game child получает полную
authoring сцену, подготавливает private World и только затем становится ready.
Результаты проверок — [HISTORY](HISTORY.md#ecs-runtime-loading-2026-11-06).

## Возможности и передача

`IEngineControl.SendAsync` сохраняет actual generated payload limit **64KiB**.
К `CommandKind` добавлены BeginSceneWorld/AppendSceneWorld/ActivateSceneWorld/
AbortSceneWorld; номера старых операций не меняются. Hello advertises
SceneWorldVersion1, SceneRegistryId, SceneMaximumBytes и SceneChunkBytes только
для `Ecs.Runtime.SceneWorldGameSession` с подключённым neutral codec. Legacy
IGameSession и EditPreview capability не объявляют. Это same-build протокол:
после изменения DTO host/runtime/game artifacts надо пересобрать вместе.

SessionId/generation/build/host/nonce и строгий sequence проверяются прежним
путём на каждом сообщении. Дополнительно transfer carries nonempty operation
GUID и exact child registry generation; GUID D format требует36characters без
whitespace. Begin фиксирует scene ID/revision, total envelope bytes и SHA256
всего envelope. Это integrity check локального trusted transport, не authentication.

Runtime допускает **один transfer на generation**, один staging buffer до
4MiB+44bytes. Chunk ровно32768bytes, кроме последнего остатка; offsets строго
contiguous, повтор/пропуск/oversize отклоняются до copy. После activation attempt
staging снимается при любом исходе; повтор Begin, даже после Abort/failure,
требует новый child. Invalid Append/stale Abort не меняет существующую staging.
Stop/Dispose снимают staging. Число chunks конечно, скрытого disk/shared-file
staging и расширения IPC resource budgets нет.

Activate проверяет complete length/outer SHA256, затем inner TMWL checksum,
strict authoring decode и соответствие scene ID/revision. Exact registry
bindings/conversion/resources проверяет прежний SceneWorldLoader. Ни staging,
ни decoded document сами по себе не означают active World/readiness.

## Игра и ownership

`SceneWorldGameSession` — явный optional base для ECS games. Constructor получает
borrowed SceneWorldLoader, RegistryId берёт у этого loader. Legacy Load(SceneSnapshot)
отвергается. LoadSceneWorld принимает полный SceneWorldData, владеет candidate
и публикует ActiveScene только после успешного import и final cancellation check.
PrepareSceneWorld готовит derived data/owned graph на private candidate до final
cancellation/publication. Returned IDisposable принадлежит base и освобождается до
World; failed factory clean up либо сохраняет SceneWorldPreparationException owner.
После initial publication user callbacks не вызываются; повторная initial load запрещена.
[Primitive profile](ECS_PRIMITIVES.md) использует hook для готовых transforms/frame0.

TickWorld получает borrowed SceneWorld и следующий tick. Успешный callback
продвигает TickNumber; failure делает World terminally Faulted. Прежние callback
writes не откатываются, дальнейшие mutations/ticks запрещены, inspection/Dispose
доступны. Это согласовано с execution profile, не transactional simulation.

Session owns active scene и retained SceneWorldLoadException.CleanupOwner от
failed preparation. DisposeGameResources сначала завершает owned schedulers/
системы, затем base освобождает World/leases. Successful cleanup не повторяется,
failed остаётся для retry; после начала Dispose Tick/Load блокируются. Reentrancy
отвергается. GameBuildLoader при failed game Dispose сохраняет session/ALC/artifact
pin до успешного retry, вместо потери owner и преждевременного unload.

RuntimeProtocol owns staging, borrows game/codec. Dispose сигнализирует cancellation
до ожидания command lock и drains cooperative importer; game не освобождает.
Runtime host сначала прекращает server admission и дожидается handlers/workers,
затем освобождает protocol и game. Token не может принудительно прервать arbitrary
user callback, который его игнорирует; parent сохраняет прежний kill/reap ownership.

## App и parent

UseAetherRuntime лениво регистрирует default IFactory<World> при отсутствии override
и IFactory<SceneWorldLoader,ComponentRegistry>. Explicit IGameSessionEntry.Create
в child собирает собственный immutable registry и получает registry-specific loader
из injected factory. Resolve остаётся в entry/composition root; import/tick его
не выполняют. App startup не создаёт Worlds. Registry provider dependencies borrowed.

Parent UseAether factory injects neutral codec и SceneResolutionService в
SessionSupervisor. StartWorldAsync(SceneWorldData[,GameBuildArtifact]) кодирует
и разрешает neutral data до launch, не загружает Game.Logic. Hello capability,
каждый offset/operation/registry ack, отсутствие early World, exact source revision,
initial entity/component counts и final World ID проверяются явно. Следующие
status/mode/tick/surface replies обязаны сохранять те же World/registry/source counts.
SessionStatus показывает последние acknowledged IDs/counts, не live contents после
runtime mutations. Ошибка/timeout/cancel запускает прежний stop/reap fence без replay.

FrameSession.StartWorldAsync с optional selected artifact использует тот же transfer,
surface и first-presentation lifecycle. CPU PatternColor может отражать ECS state;
до полного load и после Stop frames не выдаются. Existing primitive projection
сохраняется; [ECS extraction](ECS_PRIMITIVES.md) реализован в ARCH1e.4, новый
mesh/material GPU resource capability остаётся ARCH1f.

## Проверка и границы

`ManualTests/Game.Logic/WorldProbeGame.cs` — actual selected-game consumer:
exact schema, inherited values, nullable entity references, value-only pool и
фиксированный tick. Parent proof использует neutral schema/scene, не Game.Logic CLR.
`ManualTests/EcsRuntimeLoading/Verify.ps1` создаёт source Debug, published Release
и независимые parent/runtime/game consumers из новых NuGet packages в пустом cache.
Внешние dependencies копируются из exact existing cached nupkg в local feed;
сеть/глобальные настройки/пакетные версии checkout не меняются. Все raw outputs
и отдельные unique binlogs сохраняются. CPU pixels до/после Step доказывают чтение
и mutation World; новый child имеет новые World/registry IDs и подтверждённый cleanup.

Профиль не добавляет ECS checkpoint/Replace/live reload, передачу scheduler metadata,
async resource preparation, новые material/GPU bindings или миграцию Editor Play/preview.
Explicit Transform mapping доступен в [e.4](ECS_PRIMITIVES.md). ECS checkpoint explicitly Unsupported.
Старые64primitive и64KiB checkpoint ограничения сохраняются. Следующий этап —
ARCH1f mesh/material/resource contracts после реализованного e.4 primitive profile.
