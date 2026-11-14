# Игровой consume-путь Aether: играбельная сцена на фактическом surface

- ID: `TALES-20261110-ae80510f`
- Дата: 10.11.2026; автор/потребитель: TrueMoon.Tales (игра).
- Модуль: Aether (engine); capability: `engine.game-playable-loop`
  (заголовок для поиска дублей: game host без Editor, scene→World→systems→фактический
  оконный surface, Player-запуск по ENG7b).
- Исходное состояние: proposed, требуется разбор владельцем engine.
- Проверяемая версия пакетов: `0.2.5.2-alpha`
  (`src/Directory.Build.props`, чтение 10.11.2026; игра ещё не подключала пакеты —
  версия будет зафиксирована scaffold-задачей игры).

## Сценарий и наблюдение

TrueMoon.Tales — сюжетная игра по циклу «Сказки истинной луны» на выбранный
движок TrueMoon.Aether (решение владельца рабочей области 10.11.2026). Игра
планирует: загрузку канонической тестовой сцены в World, собственные игровые
ECS-системы, вывод в собственное окно приложения и Player-запуск без Editor.

Что проверено чтением 10.11.2026 ([STATUS](../../../engine/STATUS.md),
[PLAN](../../../engine/PLAN.md), [PACKAGE_BOUNDARIES](../../../engine/PACKAGE_BOUNDARIES.md),
[ManualTests/AetherPackageConsumer](../../../../ManualTests/AetherPackageConsumer/README.md)):

- Загрузка сцены (SceneWorldLoader/Codec/StartWorldAsync) и ECS (World, системы,
  transform, primitive extraction) реализованы и проверены headless/CPU и
  bounded native pixels (ARCH1e.3a/b, ARCH1e.4).
- Рендер-контракты f.0–f.3 (schemas, resource leases, owned snapshots) проверены
  в source/published/fresh-package CPU и Vulkan upload/readback scope.
- Игровой цикл на **фактическом оконном Vulkan surface** с Playing fence не
  реализован: STATUS 2026-11-09 фиксирует «Новых actual mesh surface
  pixels/Playing/device-loss proofs нет: это f.4»; следующий кодовый этап плана —
  ARCH1f.4. Это запрос готовности уже запланированной функции для прикладного
  потребителя, а не воспроизведённый дефект.
- Публичная точка подключения game-хоста (Player-режим ENG7b, `UseAether...` для
  приложения без Editor) для внешнего потребителя не зафиксирована как проверенный
  сценарий: ARCH2e проверяет Editor-потребление и runtime-запуск выбранного game
  artifact в child-процессе, но независимый игровой app на окне.

Сценарий игры ещё не выполнялся (игра не подключала пакеты): заявка фиксирует
требования и критерии приёмки потребителя до scaffold, чтобы владелец модуля учёл
их в очереди f.4 и последующих срезах. Открытая полная green-статус Aether
(2 native fixture restore non-green 09.11.2026) не смешивается с этим запросом.

## Требуемый результат

- Публичный путь game-хоста без Editor: `App.Builder` + расширение модуля Aether
  (Player-режим по ENG7b), в котором приложение: загружает выбранный scene
  artifact в World, регистрирует свои системы, получает кадры и выводит сцену
  в surface, принадлежащий окну приложения.
- Детерминированные Playing/stop semantics: start/stop/replace, suspend/resume и
  resize с определённым ownership surface/device; cleanup после Stop без утечек
  (owners0-счётчики в формате существующих профилей); failed startup/cleanup
  не оставляют половина-владеющих ресурсов.
- Доставка `TrueMoon.Aether` (и замыкания зависимостей) как пакета в свежий
  локальный feed потребителя без source lookup — формат proof ARCH2e.1, но для
  game-консьюмера; версия пакета и метаданные совпадают с собранным closure.
- Ввод окна достигает игровых систем по явному публичному контракту (роли Player
  по ENG7b); без скрытого резолва контейнера в кадровом цикле.
- Критерии приёмки: actual source Debug/published Release/fresh-package GPU pixels
  игрового consumer; reload/resize/failed-startup/Stop/Replace outcomes отдельно
  от headless; device-loss помечается отдельно, если не входит в принятый scope.

## Влияние на потребителя и временное решение

Игра не может заявить играбельную сборку до готовности пути. Временный план игры:
scaffold пакетами и headless-цикл (T1/T2) на уже проверенных ARCH1e API, окно не
заявляется. Блокируемые этапы: T3 (окно/рендер). [План игры](../../../../../../TrueMoon.Tales/docs/PLAN.md).

## Возможные варианты реализации (не согласованное API)

- f.4 как surface renderer + Playing fence с последующей тонкой App-обёрткой
  game-хоста; либо game session contract поверх f.3 snapshots. Выбор API, lifetime
  и границы — за владельцем модуля.
- Открытый вопрос приоритета для игры: профиль texture/sprite (ARCH1f.5) раньше
  объёмного освещения — визуальная подача цикла иллюстративная (2D-опоры),
  cube-mesh proof f.4 недостаточен для содержательного кадра игры. Просьба
  владельцу учесть при декомпозиции f.4→f.5; решение и очередь остаются за модулем.
- Отдельные будущие заявки игры (не в этой): production-импорт ассетов (ARCH1g g.1),
  аудио (ARCH1h), при необходимости — persistent content revisions (f.6).
